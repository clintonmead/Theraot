#if NET20 || NET30

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Dynamic.Utils;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Theraot.Collections.ThreadSafe;
using AstUtils = System.Linq.Expressions.Utils;

namespace System.Dynamic
{
    /// <summary>
    /// Represents an object with members that can be dynamically added and removed at runtime.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class ExpandoObject : IDynamicMetaObjectProvider, IDictionary<string, object>, INotifyPropertyChanged
    {
        internal const int AmbiguousMatchFound = -2;

        // The value is used to indicate there exists ambiguous match in the Expando object
        internal const int NoMatch = -1;

        internal static readonly object Uninitialized = new object();

        internal readonly object LockObject;

        private static readonly MethodInfo _expandoCheckVersion =
            typeof(RuntimeOps).GetMethod(nameof(RuntimeOps.ExpandoCheckVersion));

        private static readonly MethodInfo _expandoPromoteClass =
            typeof(RuntimeOps).GetMethod(nameof(RuntimeOps.ExpandoPromoteClass));

        private static readonly MethodInfo _expandoTryDeleteValue =
            typeof(RuntimeOps).GetMethod(nameof(RuntimeOps.ExpandoTryDeleteValue));

        private static readonly MethodInfo _expandoTryGetValue =
                                                                    typeof(RuntimeOps).GetMethod(nameof(RuntimeOps.ExpandoTryGetValue));

        private static readonly MethodInfo _expandoTrySetValue =
            typeof(RuntimeOps).GetMethod(nameof(RuntimeOps.ExpandoTrySetValue));

        private readonly IEvent<PropertyChangedEventArgs> _propertyChanged;
        private int _count;
        private ExpandoData _data;                                    // the data currently being held by the Expando object
                                                                      // the count of available members

        // A marker object used to identify that a value is uninitialized.

        // The value is used to indicate there is no matching member
        /// <summary>
        /// Creates a new ExpandoObject with no members.
        /// </summary>
        public ExpandoObject()
        {
            _data = ExpandoData.Empty;
            _propertyChanged = new StrongEvent<PropertyChangedEventArgs>();
            LockObject = new object();
        }

        #region Get/Set/Delete Helpers

        /// <summary>
        /// Exposes the ExpandoClass which we've associated with this
        /// Expando object.  Used for type checks in rules.
        /// </summary>
        internal ExpandoClass Class => _data.Class;

        /// <summary>
        /// Returns true if the member at the specified index has been deleted,
        /// otherwise false. Call this function holding the lock.
        /// </summary>
        internal bool IsDeletedMember(int index)
        {
            lock (LockObject)
            {
                var data = _data;
                Debug.Assert(index >= 0 && index <= data.Length);

                if (index == data.Length)
                {
                    // The member is a newly added by SetMemberBinder and not in data yet
                    return false;
                }

                return data[index] == Uninitialized;
            }
        }

        /// <summary>
        /// Internal helper to promote a class.  Called from our RuntimeOps helper.  This
        /// version simply doesn't expose the ExpandoData object which is a private
        /// data structure.
        /// </summary>
        internal void PromoteClass(object oldClass, object newClass)
        {
            lock (LockObject)
            {
                PromoteClassCore((ExpandoClass)oldClass, (ExpandoClass)newClass);
            }
        }

        /// <summary>
        /// Deletes the data stored for the specified class at the specified index.
        /// </summary>
        internal bool TryDeleteValue(object indexClass, int index, string name, bool ignoreCase, object deleteValue)
        {
            string propertyName;
            lock (LockObject)
            {
                var data = _data;
                if (data.Class != indexClass || ignoreCase)
                {
                    // the class has changed or we are doing a case-insensitive search,
                    // we need to get the correct index.  If there is no associated index
                    // we simply can't have the value and we return false.
                    index = data.Class.GetValueIndex(name, ignoreCase, this);
                    if (index == AmbiguousMatchFound)
                    {
                        throw Error.AmbiguousMatchInExpandoObject(name);
                    }
                }
                if (index == NoMatch)
                {
                    return false;
                }

                object oldValue = data[index];
                if (oldValue == Uninitialized)
                {
                    return false;
                }

                // Make sure the value matches, if requested.
                //
                // It's a shame we have to call Equals with the lock held but
                // there doesn't seem to be a good way around that, and
                // ConcurrentDictionary in mscorlib does the same thing.
                if (deleteValue != Uninitialized && !Equals(oldValue, deleteValue))
                {
                    return false;
                }
                data[index] = Uninitialized;
                propertyName = data.Class.Keys[index];
                // Deleting an available member decreases the count of available members
                _count--;
            }

            // Notify property changed outside the lock
            _propertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));

            return true;
        }

        /// <summary>
        /// Try to get the data stored for the specified class at the specified index.  If the
        /// class has changed a full lookup for the slot will be performed and the correct
        /// value will be retrieved.
        /// </summary>
        internal bool TryGetValue(object indexClass, int index, string name, bool ignoreCase, out object value)
        {
            // read the data now.  The data is immutable so we get a consistent view.
            // If there's a concurrent writer they will replace data and it just appears
            // that we won the race
            var data = _data;
            if (data.Class != indexClass || ignoreCase)
            {
                /* Re-search for the index matching the name here if
                 *  1) the class has changed, we need to get the correct index and return
                 *  the value there.
                 *  2) the search is case insensitive:
                 *      a. the member specified by index may be deleted, but there might be other
                 *      members matching the name if the binder is case insensitive.
                 *      b. the member that exactly matches the name didn't exist before and exists now,
                 *      need to find the exact match.
                 */
                index = data.Class.GetValueIndex(name, ignoreCase, this);
                if (index == AmbiguousMatchFound)
                {
                    throw Error.AmbiguousMatchInExpandoObject(name);
                }
            }

            if (index == NoMatch)
            {
                value = null;
                return false;
            }

            // Capture the value into a temp, so it doesn't get mutated after we check
            // for Uninitialized.
            object temp = data[index];
            if (temp == Uninitialized)
            {
                value = null;
                return false;
            }

            // index is now known to be correct
            value = temp;
            return true;
        }

        /// <summary>
        /// Sets the data for the specified class at the specified index.  If the class has
        /// changed then a full look for the slot will be performed.  If the new class does
        /// not have the provided slot then the Expando's class will change. Only case sensitive
        /// setter is supported in ExpandoObject.
        /// </summary>
        internal void TrySetValue(object indexClass, int index, object value, string name, bool ignoreCase, bool add)
        {
            string propertyName;

            lock (LockObject)
            {
                var data = _data;
                if (data.Class != indexClass || ignoreCase)
                {
                    // The class has changed or we are doing a case-insensitive search,
                    // we need to get the correct index and set the value there.  If we
                    // don't have the value then we need to promote the class - that
                    // should only happen when we have multiple concurrent writers.
                    index = data.Class.GetValueIndex(name, ignoreCase, this);
                    if (index == AmbiguousMatchFound)
                    {
                        throw Error.AmbiguousMatchInExpandoObject(name);
                    }
                    if (index == NoMatch)
                    {
                        // Before creating a new class with the new member, need to check
                        // if there is the exact same member but is deleted. We should reuse
                        // the class if there is such a member.
                        int exactMatch = ignoreCase ?
                            data.Class.GetValueIndexCaseSensitive(name, LockObject) :
                            index;
                        if (exactMatch != NoMatch)
                        {
                            Debug.Assert(data[exactMatch] == Uninitialized);
                            index = exactMatch;
                        }
                        else
                        {
                            ExpandoClass newClass = data.Class.FindNewClass(name, LockObject);
                            _data = PromoteClassCore(data.Class, newClass);
                            // After the class promotion, there must be an exact match,
                            // so we can do case-sensitive search here.
                            index = data.Class.GetValueIndexCaseSensitive(name, LockObject);
                            Debug.Assert(index != NoMatch);
                        }
                    }
                }

                // Setting an uninitialized member increases the count of available members
                var oldValue = data[index];
                if (oldValue == Uninitialized)
                {
                    _count++;
                }
                else if (add)
                {
                    throw Error.SameKeyExistsInExpando(name);
                }
                data[index] = value;
                propertyName = data.Class.Keys[index];
            }

            // Notify property changed outside the lock
            _propertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Promotes the class from the old type to the new type and returns the new
        /// ExpandoData object.
        /// </summary>
        private ExpandoData PromoteClassCore(ExpandoClass oldClass, ExpandoClass newClass)
        {
            lock (LockObject)
            {
                var data = _data;
                Debug.Assert(oldClass != newClass);

                if (data.Class == oldClass)
                {
                    _data = data.UpdateClass(newClass);
                }

                return data;
            }
        }

        #endregion Get/Set/Delete Helpers

        #region IDynamicMetaObjectProvider Members

        DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
        {
            return new MetaExpando(parameter, this);
        }

        #endregion IDynamicMetaObjectProvider Members

        #region Helper methods

        private bool ExpandoContainsKey(string key)
        {
            lock (LockObject)
            {
                return _data.Class.GetValueIndexCaseSensitive(key, LockObject) >= 0;
            }
        }

        private void TryAddMember(string key, object value)
        {
            ContractUtils.RequiresNotNull(key, nameof(key));
            // Pass null to the class, which forces lookup.
            TrySetValue(null, -1, value, key, ignoreCase: false, add: true);
        }

        private bool TryGetValueForKey(string key, out object value)
        {
            // Pass null to the class, which forces lookup.
            return TryGetValue(null, -1, key, ignoreCase: false, value: out value);
        }

        [DebuggerTypeProxy(typeof(KeyCollectionDebugView))]
        [DebuggerDisplay("Count = {Count}")]
        private class KeyCollection : ICollection<string>
        {
            private readonly ExpandoObject _expando;
            private readonly int _expandoCount;
            private readonly ExpandoData _expandoData;
            private readonly int _expandoVersion;

            internal KeyCollection(ExpandoObject expando)
            {
                lock (expando.LockObject)
                {
                    _expando = expando;
                    _expandoVersion = expando._data.Version;
                    _expandoCount = expando._count;
                    _expandoData = expando._data;
                }
            }

            private void CheckVersion()
            {
                if (_expando._data.Version != _expandoVersion || _expandoData != _expando._data)
                {
                    //the underlying expando object has changed
                    throw Error.CollectionModifiedWhileEnumerating();
                }
            }

            #region ICollection<string> Members

            public int Count
            {
                get
                {
                    CheckVersion();
                    return _expandoCount;
                }
            }

            public bool IsReadOnly => true;

            public void Add(string item)
            {
                throw Error.CollectionReadOnly();
            }

            public void Clear()
            {
                throw Error.CollectionReadOnly();
            }

            public bool Contains(string item)
            {
                lock (_expando.LockObject)
                {
                    CheckVersion();
                    return _expando.ExpandoContainsKey(item);
                }
            }

            public void CopyTo(string[] array, int arrayIndex)
            {
                ContractUtils.RequiresNotNull(array, nameof(array));
                ContractUtils.RequiresArrayRange(array, arrayIndex, _expandoCount, nameof(arrayIndex), nameof(Count));
                lock (_expando.LockObject)
                {
                    CheckVersion();
                    ExpandoData data = _expando._data;
                    for (int i = 0; i < data.Class.Keys.Length; i++)
                    {
                        if (data[i] != Uninitialized)
                        {
                            array[arrayIndex++] = data.Class.Keys[i];
                        }
                    }
                }
            }

            public bool Remove(string item)
            {
                throw Error.CollectionReadOnly();
            }

            #endregion ICollection<string> Members

            #region IEnumerable<string> Members

            public IEnumerator<string> GetEnumerator()
            {
                for (int i = 0, n = _expandoData.Class.Keys.Length; i < n; i++)
                {
                    CheckVersion();
                    if (_expandoData[i] != Uninitialized)
                    {
                        yield return _expandoData.Class.Keys[i];
                    }
                }
            }

            #endregion IEnumerable<string> Members

            #region IEnumerable Members

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            #endregion IEnumerable Members
        }

        // We create a non-generic type for the debug view for each different collection type
        // that uses DebuggerTypeProxy, instead of defining a generic debug view type and
        // using different instantiations. The reason for this is that support for generics
        // with using DebuggerTypeProxy is limited. For C#, DebuggerTypeProxy supports only
        // open types (from MSDN https://docs.microsoft.com/en-us/visualstudio/debugger/using-debuggertypeproxy-attribute).
        private sealed class KeyCollectionDebugView
        {
            private readonly ICollection<string> _collection;

            public KeyCollectionDebugView(ICollection<string> collection)
            {
                ContractUtils.RequiresNotNull(collection, nameof(collection));
                _collection = collection;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            // ReSharper disable once UnusedMember.Local
            public string[] Items
            {
                get
                {
                    string[] items = new string[_collection.Count];
                    _collection.CopyTo(items, 0);
                    return items;
                }
            }
        }

        [DebuggerTypeProxy(typeof(ValueCollectionDebugView))]
        [DebuggerDisplay("Count = {Count}")]
        private class ValueCollection : ICollection<object>
        {
            private readonly ExpandoObject _expando;
            private readonly int _expandoCount;
            private readonly ExpandoData _expandoData;
            private readonly int _expandoVersion;

            internal ValueCollection(ExpandoObject expando)
            {
                lock (expando.LockObject)
                {
                    _expando = expando;
                    _expandoVersion = expando._data.Version;
                    _expandoCount = expando._count;
                    _expandoData = expando._data;
                }
            }

            private void CheckVersion()
            {
                if (_expando._data.Version != _expandoVersion || _expandoData != _expando._data)
                {
                    //the underlying expando object has changed
                    throw Error.CollectionModifiedWhileEnumerating();
                }
            }

            #region ICollection<string> Members

            public int Count
            {
                get
                {
                    CheckVersion();
                    return _expandoCount;
                }
            }

            public bool IsReadOnly => true;

            public void Add(object item)
            {
                throw Error.CollectionReadOnly();
            }

            public void Clear()
            {
                throw Error.CollectionReadOnly();
            }

            public bool Contains(object item)
            {
                lock (_expando.LockObject)
                {
                    CheckVersion();

                    ExpandoData data = _expando._data;
                    for (int i = 0; i < data.Class.Keys.Length; i++)
                    {
                        // See comment in TryDeleteValue; it's okay to call
                        // object.Equals with the lock held.
                        if (Equals(data[i], item))
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }

            public void CopyTo(object[] array, int arrayIndex)
            {
                ContractUtils.RequiresNotNull(array, nameof(array));
                ContractUtils.RequiresArrayRange(array, arrayIndex, _expandoCount, nameof(arrayIndex), nameof(Count));
                lock (_expando.LockObject)
                {
                    CheckVersion();
                    ExpandoData data = _expando._data;
                    for (int i = 0; i < data.Class.Keys.Length; i++)
                    {
                        if (data[i] != Uninitialized)
                        {
                            array[arrayIndex++] = data[i];
                        }
                    }
                }
            }

            public bool Remove(object item)
            {
                throw Error.CollectionReadOnly();
            }

            #endregion ICollection<string> Members

            #region IEnumerable<string> Members

            public IEnumerator<object> GetEnumerator()
            {
                ExpandoData data = _expando._data;
                for (int i = 0; i < data.Class.Keys.Length; i++)
                {
                    CheckVersion();
                    // Capture the value into a temp so we don't inadvertently
                    // return Uninitialized.
                    object temp = data[i];
                    if (temp != Uninitialized)
                    {
                        yield return temp;
                    }
                }
            }

            #endregion IEnumerable<string> Members

            #region IEnumerable Members

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            #endregion IEnumerable Members
        }

        // We create a non-generic type for the debug view for each different collection type
        // that uses DebuggerTypeProxy, instead of defining a generic debug view type and
        // using different instantiations. The reason for this is that support for generics
        // with using DebuggerTypeProxy is limited. For C#, DebuggerTypeProxy supports only
        // open types (from MSDN https://docs.microsoft.com/en-us/visualstudio/debugger/using-debuggertypeproxy-attribute).
        private sealed class ValueCollectionDebugView
        {
            private readonly ICollection<object> _collection;

            public ValueCollectionDebugView(ICollection<object> collection)
            {
                ContractUtils.RequiresNotNull(collection, nameof(collection));
                _collection = collection;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            // ReSharper disable once UnusedMember.Local
            public object[] Items
            {
                get
                {
                    object[] items = new object[_collection.Count];
                    _collection.CopyTo(items, 0);
                    return items;
                }
            }
        }

        #endregion Helper methods

        #region IDictionary<string, object> Members

        ICollection<string> IDictionary<string, object>.Keys => new KeyCollection(this);

        ICollection<object> IDictionary<string, object>.Values => new ValueCollection(this);

        object IDictionary<string, object>.this[string key]
        {
            get
            {
                if (!TryGetValueForKey(key, out var value))
                {
                    throw Error.KeyDoesNotExistInExpando(key);
                }
                return value;
            }
            set
            {
                ContractUtils.RequiresNotNull(key, nameof(key));
                // Pass null to the class, which forces lookup.
                TrySetValue(null, -1, value, key, ignoreCase: false, add: false);
            }
        }

        void IDictionary<string, object>.Add(string key, object value)
        {
            TryAddMember(key, value);
        }

        bool IDictionary<string, object>.ContainsKey(string key)
        {
            ContractUtils.RequiresNotNull(key, nameof(key));

            ExpandoData data = _data;
            int index = data.Class.GetValueIndexCaseSensitive(key, LockObject);
            return index >= 0 && data[index] != Uninitialized;
        }

        bool IDictionary<string, object>.Remove(string key)
        {
            ContractUtils.RequiresNotNull(key, nameof(key));
            // Pass null to the class, which forces lookup.
            return TryDeleteValue(null, -1, key, ignoreCase: false, deleteValue: Uninitialized);
        }

        bool IDictionary<string, object>.TryGetValue(string key, out object value)
        {
            return TryGetValueForKey(key, out value);
        }

        #endregion IDictionary<string, object> Members

        #region ICollection<KeyValuePair<string, object>> Members

        int ICollection<KeyValuePair<string, object>>.Count => _count;

        bool ICollection<KeyValuePair<string, object>>.IsReadOnly => false;

        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
        {
            TryAddMember(item.Key, item.Value);
        }

        void ICollection<KeyValuePair<string, object>>.Clear()
        {
            // We remove both class and data!
            ExpandoData data;
            lock (LockObject)
            {
                data = _data;
                _data = ExpandoData.Empty;
                _count = 0;
            }

            // Notify property changed for all properties.
            for (int i = 0, n = data.Class.Keys.Length; i < n; i++)
            {
                if (data[i] != Uninitialized)
                {
                    _propertyChanged.Invoke(this, new PropertyChangedEventArgs(data.Class.Keys[i]));
                }
            }
        }

        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
        {
            if (!TryGetValueForKey(item.Key, out var value))
            {
                return false;
            }

            return Equals(value, item.Value);
        }

        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            ContractUtils.RequiresNotNull(array, nameof(array));

            // We want this to be atomic and not throw, though we must do the range checks inside this lock.
            lock (LockObject)
            {
                ContractUtils.RequiresArrayRange(array, arrayIndex, _count, nameof(arrayIndex), nameof(ICollection<KeyValuePair<string, object>>.Count));
                foreach (KeyValuePair<string, object> item in this)
                {
                    array[arrayIndex++] = item;
                }
            }
        }

        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
        {
            return TryDeleteValue(null, -1, item.Key, ignoreCase: false, deleteValue: item.Value);
        }

        #endregion ICollection<KeyValuePair<string, object>> Members

        #region IEnumerable<KeyValuePair<string, object>> Member

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            ExpandoData data = _data;
            return GetExpandoEnumerator(data, data.Version);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            ExpandoData data = _data;
            return GetExpandoEnumerator(data, data.Version);
        }

        // Note: takes the data and version as parameters so they will be
        // captured before the first call to MoveNext().
        private IEnumerator<KeyValuePair<string, object>> GetExpandoEnumerator(ExpandoData data, int version)
        {
            for (int i = 0; i < data.Class.Keys.Length; i++)
            {
                if (_data.Version != version || data != _data)
                {
                    // The underlying expando object has changed:
                    // 1) the version of the expando data changed
                    // 2) the data object is changed
                    throw Error.CollectionModifiedWhileEnumerating();
                }
                // Capture the value into a temp so we don't inadvertently
                // return Uninitialized.
                object temp = data[i];
                if (temp != Uninitialized)
                {
                    yield return new KeyValuePair<string, object>(data.Class.Keys[i], temp);
                }
            }
        }

        #endregion IEnumerable<KeyValuePair<string, object>> Member

        #region MetaExpando

        private class MetaExpando : DynamicMetaObject
        {
            public MetaExpando(Expression expression, ExpandoObject value)
                : base(expression, BindingRestrictions.Empty, value)
            {
            }

            private new ExpandoObject Value => (ExpandoObject)base.Value;

            public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder)
            {
                ContractUtils.RequiresNotNull(binder, nameof(binder));

                int index = Value.Class.GetValueIndex(binder.Name, binder.IgnoreCase, Value);

                Expression tryDelete = Expression.Call(
                    _expandoTryDeleteValue,
                    GetLimitedSelf(),
                    Expression.Constant(Value.Class, typeof(object)),
                    AstUtils.Constant(index),
                    Expression.Constant(binder.Name),
                    AstUtils.Constant(binder.IgnoreCase)
                );
                DynamicMetaObject fallback = binder.FallbackDeleteMember(this);

                DynamicMetaObject target = new DynamicMetaObject(
                    Expression.IfThen(Expression.Not(tryDelete), fallback.Expression),
                    fallback.Restrictions
                );

                return AddDynamicTestAndDefer(binder, Value.Class, null, target);
            }

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
            {
                ContractUtils.RequiresNotNull(binder, nameof(binder));
                return BindGetOrInvokeMember(
                    binder,
                    binder.Name,
                    binder.IgnoreCase,
                    binder.FallbackGetMember(this),
                    null
                );
            }

            public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
            {
                ContractUtils.RequiresNotNull(binder, nameof(binder));
                return BindGetOrInvokeMember(
                    binder,
                    binder.Name,
                    binder.IgnoreCase,
                    binder.FallbackInvokeMember(this, args),
                    value => binder.FallbackInvoke(value, args, null)
                );
            }

            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
            {
                ContractUtils.RequiresNotNull(binder, nameof(binder));
                ContractUtils.RequiresNotNull(value, nameof(value));

                ExpandoClass originalClass = GetClassEnsureIndex(binder.Name, binder.IgnoreCase, Value, out var @class, out var index);

                return AddDynamicTestAndDefer(
                    binder,
                    @class,
                    originalClass,
                    new DynamicMetaObject(
                        Expression.Call(
                            _expandoTrySetValue,
                            GetLimitedSelf(),
                            Expression.Constant(@class, typeof(object)),
                            AstUtils.Constant(index),
                            Expression.Convert(value.Expression, typeof(object)),
                            Expression.Constant(binder.Name),
                            AstUtils.Constant(binder.IgnoreCase)
                        ),
                        BindingRestrictions.Empty
                    )
                );
            }

            public override IEnumerable<string> GetDynamicMemberNames()
            {
                var expandoData = Value._data;
                var @class = expandoData.Class;
                for (int i = 0; i < @class.Keys.Length; i++)
                {
                    object val = expandoData[i];
                    if (val != Uninitialized)
                    {
                        yield return @class.Keys[i];
                    }
                }
            }

            /// <summary>
            /// Adds a dynamic test which checks if the version has changed.  The test is only necessary for
            /// performance as the methods will do the correct thing if called with an incorrect version.
            /// </summary>
            private DynamicMetaObject AddDynamicTestAndDefer(DynamicMetaObjectBinder binder, ExpandoClass @class, ExpandoClass originalClass, DynamicMetaObject succeeds)
            {
                Expression ifTestSucceeds = succeeds.Expression;
                if (originalClass != null)
                {
                    // we are accessing a member which has not yet been defined on this class.
                    // We force a class promotion after the type check.  If the class changes the
                    // promotion will fail and the set/delete will do a full lookup using the new
                    // class to discover the name.
                    Debug.Assert(originalClass != @class);

                    ifTestSucceeds = Expression.Block(
                        Expression.Call(
                            null,
                            _expandoPromoteClass,
                            GetLimitedSelf(),
                            Expression.Constant(originalClass, typeof(object)),
                            Expression.Constant(@class, typeof(object))
                        ),
                        succeeds.Expression
                    );
                }

                return new DynamicMetaObject(
                    Expression.Condition(
                        Expression.Call(
                            null,
                            _expandoCheckVersion,
                            GetLimitedSelf(),
                            Expression.Constant(originalClass ?? @class, typeof(object))
                        ),
                        ifTestSucceeds,
                        binder.GetUpdateExpression(ifTestSucceeds.Type)
                    ),
                    GetRestrictions().Merge(succeeds.Restrictions)
                );
            }

            private DynamicMetaObject BindGetOrInvokeMember(DynamicMetaObjectBinder binder, string name, bool ignoreCase, DynamicMetaObject fallback, Func<DynamicMetaObject, DynamicMetaObject> fallbackInvoke)
            {
                ExpandoClass @class = Value.Class;

                //try to find the member, including the deleted members
                int index = @class.GetValueIndex(name, ignoreCase, Value);

                ParameterExpression value = Expression.Parameter(typeof(object), "value");

                Expression tryGetValue = Expression.Call(
                    _expandoTryGetValue,
                    GetLimitedSelf(),
                    Expression.Constant(@class, typeof(object)),
                    AstUtils.Constant(index),
                    Expression.Constant(name),
                    AstUtils.Constant(ignoreCase),
                    value
                );

                var result = new DynamicMetaObject(value, BindingRestrictions.Empty);
                if (fallbackInvoke != null)
                {
                    result = fallbackInvoke(result);
                }

                result = new DynamicMetaObject(
                    Expression.Block(
                        new TrueReadOnlyCollection<ParameterExpression>(value),
                        new TrueReadOnlyCollection<Expression>(
                            Expression.Condition(
                                tryGetValue,
                                result.Expression,
                                fallback.Expression,
                                typeof(object)
                            )
                        )
                    ),
                    result.Restrictions.Merge(fallback.Restrictions)
                );

                return AddDynamicTestAndDefer(binder, Value.Class, null, result);
            }

            /// <summary>
            /// Gets the class and the index associated with the given name.  Does not update the expando object.  Instead
            /// this returns both the original and desired new class.  A rule is created which includes the test for the
            /// original class, the promotion to the new class, and the set/delete based on the class post-promotion.
            /// </summary>
            private ExpandoClass GetClassEnsureIndex(string name, bool caseInsensitive, ExpandoObject obj, out ExpandoClass @class, out int index)
            {
                ExpandoClass originalClass = Value.Class;

                index = originalClass.GetValueIndex(name, caseInsensitive, obj);
                if (index == AmbiguousMatchFound)
                {
                    @class = originalClass;
                    return null;
                }
                if (index == NoMatch)
                {
                    // go ahead and find a new class now...
                    ExpandoClass newClass = originalClass.FindNewClass(name, obj.LockObject);

                    @class = newClass;
                    index = newClass.GetValueIndexCaseSensitive(name, obj.LockObject);

                    Debug.Assert(index != NoMatch);
                    return originalClass;
                }

                @class = originalClass;
                return null;
            }

            /// <summary>
            /// Returns our Expression converted to our known LimitType
            /// </summary>
            private Expression GetLimitedSelf()
            {
                if (TypeUtils.AreEquivalent(Expression.Type, LimitType))
                {
                    return Expression;
                }
                return Expression.Convert(Expression, LimitType);
            }

            /// <summary>
            /// Returns a Restrictions object which includes our current restrictions merged
            /// with a restriction limiting our type
            /// </summary>
            private BindingRestrictions GetRestrictions()
            {
                Debug.Assert(Restrictions == BindingRestrictions.Empty, "We don't merge, restrictions are always empty");

                return BindingRestrictions.GetTypeRestriction(this);
            }
        }

        #endregion MetaExpando

        #region ExpandoData

        /// <summary>
        /// Stores the class and the data associated with the class as one atomic
        /// pair.  This enables us to do a class check in a thread safe manner w/o
        /// requiring locks.
        /// </summary>
        private class ExpandoData
        {
            internal static readonly ExpandoData Empty = new ExpandoData();

            /// <summary>
            /// the dynamically assigned class associated with the Expando object
            /// </summary>
            internal readonly ExpandoClass Class;

            /// <summary>
            /// data stored in the expando object, key names are stored in the class.
            ///
            /// Expando._data must be locked when mutating the value.  Otherwise a copy of it
            /// could be made and lose values.
            /// </summary>
            private readonly object[] _dataArray;

            /// <summary>
            /// the version of the ExpandoObject that tracks set and delete operations
            /// </summary>
            private int _version;

            /// <summary>
            /// Constructs a new ExpandoData object with the specified class and data.
            /// </summary>
            private ExpandoData(ExpandoClass @class, object[] data, int version)
            {
                Class = @class;
                _dataArray = data;
                _version = version;
            }

            /// <summary>
            /// Constructs an empty ExpandoData object with the empty class and no data.
            /// </summary>
            private ExpandoData()
            {
                Class = ExpandoClass.Empty;
                _dataArray = ArrayReservoir<object>.EmptyArray;
            }

            internal int Length => _dataArray.Length;

            internal int Version => _version;

            /// <summary>
            /// Indexer for getting/setting the data
            /// </summary>
            internal object this[int index]
            {
                get => _dataArray[index];
                set
                {
                    //when the array is updated, version increases, even the new value is the same
                    //as previous. Dictionary type has the same behavior.
                    _version++;
                    _dataArray[index] = value;
                }
            }

            /// <summary>
            /// Update the associated class and increases the storage for the data array if needed.
            /// </summary>
            internal ExpandoData UpdateClass(ExpandoClass newClass)
            {
                if (_dataArray.Length >= newClass.Keys.Length)
                {
                    // we have extra space in our buffer, just initialize it to Uninitialized.
                    this[newClass.Keys.Length - 1] = Uninitialized;
                    return new ExpandoData(newClass, _dataArray, _version);
                }

                // we've grown too much - we need a new object array
                int oldLength = _dataArray.Length;
                object[] arr = new object[GetAlignedSize(newClass.Keys.Length)];
                Array.Copy(_dataArray, 0, arr, 0, _dataArray.Length);
                ExpandoData newData = new ExpandoData(newClass, arr, _version) { [oldLength] = Uninitialized };
                return newData;
            }

            private static int GetAlignedSize(int len)
            {
                // the alignment of the array for storage of values (must be a power of two)
                const int DataArrayAlignment = 8;

                // round up and then mask off lower bits
                return (len + (DataArrayAlignment - 1)) & (~(DataArrayAlignment - 1));
            }
        }

        #endregion ExpandoData

        #region INotifyPropertyChanged

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add => _propertyChanged.Add(value.Method, value.Target);
            remove => _propertyChanged.Remove(value.Method, value.Target);
        }

        #endregion INotifyPropertyChanged
    }
}

namespace System.Runtime.CompilerServices
{
    //
    // Note: these helpers are kept as simple wrappers so they have a better
    // chance of being inlined.
    //
    public static partial class RuntimeOps
    {
        /// <summary>
        /// Checks the version of the expando object.
        /// </summary>
        /// <param name="expando">The expando object.</param>
        /// <param name="version">The version to check.</param>
        /// <returns>true if the version is equal; otherwise, false.</returns>
        // [Obsolete("do not use this method", error: true), EditorBrowsable(EditorBrowsableState.Never)]
        public static bool ExpandoCheckVersion(ExpandoObject expando, object version)
        {
            return expando.Class == version;
        }

        /// <summary>
        /// Promotes an expando object from one class to a new class.
        /// </summary>
        /// <param name="expando">The expando object.</param>
        /// <param name="oldClass">The old class of the expando object.</param>
        /// <param name="newClass">The new class of the expando object.</param>
        // [Obsolete("do not use this method", error: true), EditorBrowsable(EditorBrowsableState.Never)]
        public static void ExpandoPromoteClass(ExpandoObject expando, object oldClass, object newClass)
        {
            expando.PromoteClass(oldClass, newClass);
        }

        /// <summary>
        /// Deletes the value of an item in an expando object.
        /// </summary>
        /// <param name="expando">The expando object.</param>
        /// <param name="indexClass">The class of the expando object.</param>
        /// <param name="index">The index of the member.</param>
        /// <param name="name">The name of the member.</param>
        /// <param name="ignoreCase">true if the name should be matched ignoring case; false otherwise.</param>
        /// <returns>true if the item was successfully removed; otherwise, false.</returns>
        // [Obsolete("do not use this method", error: true), EditorBrowsable(EditorBrowsableState.Never)]
        public static bool ExpandoTryDeleteValue(ExpandoObject expando, object indexClass, int index, string name, bool ignoreCase)
        {
            return expando.TryDeleteValue(indexClass, index, name, ignoreCase, ExpandoObject.Uninitialized);
        }

        /// <summary>
        /// Gets the value of an item in an expando object.
        /// </summary>
        /// <param name="expando">The expando object.</param>
        /// <param name="indexClass">The class of the expando object.</param>
        /// <param name="index">The index of the member.</param>
        /// <param name="name">The name of the member.</param>
        /// <param name="ignoreCase">true if the name should be matched ignoring case; false otherwise.</param>
        /// <param name="value">The out parameter containing the value of the member.</param>
        /// <returns>True if the member exists in the expando object, otherwise false.</returns>
        // [Obsolete("do not use this method", error: true), EditorBrowsable(EditorBrowsableState.Never)]
        public static bool ExpandoTryGetValue(ExpandoObject expando, object indexClass, int index, string name, bool ignoreCase, out object value)
        {
            return expando.TryGetValue(indexClass, index, name, ignoreCase, out value);
        }

        /// <summary>
        /// Sets the value of an item in an expando object.
        /// </summary>
        /// <param name="expando">The expando object.</param>
        /// <param name="indexClass">The class of the expando object.</param>
        /// <param name="index">The index of the member.</param>
        /// <param name="value">The value of the member.</param>
        /// <param name="name">The name of the member.</param>
        /// <param name="ignoreCase">true if the name should be matched ignoring case; false otherwise.</param>
        /// <returns>
        /// Returns the index for the set member.
        /// </returns>
        // [Obsolete("do not use this method", error: true), EditorBrowsable(EditorBrowsableState.Never)]
        public static object ExpandoTrySetValue(ExpandoObject expando, object indexClass, int index, object value, string name, bool ignoreCase)
        {
            expando.TrySetValue(indexClass, index, value, name, ignoreCase, false);
            return value;
        }
    }
}

#endif