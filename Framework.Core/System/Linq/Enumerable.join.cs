﻿#if LESSTHAN_NET35

#pragma warning disable RECS0017 // Possible compare of value type with 'null'

using System.Collections.Generic;

namespace System.Linq
{
    public static partial class Enumerable
    {
        public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey>? comparer)
        {
            if (outer == null)
            {
                throw new ArgumentNullException(nameof(outer));
            }
            if (inner == null)
            {
                throw new ArgumentNullException(nameof(inner));
            }
            if (outerKeySelector == null)
            {
                throw new ArgumentNullException(nameof(outerKeySelector));
            }
            if (innerKeySelector == null)
            {
                throw new ArgumentNullException(nameof(innerKeySelector));
            }
            if (resultSelector == null)
            {
                throw new ArgumentNullException(nameof(resultSelector));
            }
            if (comparer == null)
            {
                return CreateJoinIterator(EqualityComparer<TKey>.Default);
            }
            return CreateJoinIterator(comparer);

            IEnumerable<TResult> CreateJoinIterator(IEqualityComparer<TKey> notNullComparer)
            {
                var innerKeys = ToLookup(inner, innerKeySelector, notNullComparer);

                foreach (var element in outer)
                {
                    var outerKey = outerKeySelector(element);
                    if (outerKey == null || !innerKeys.Contains(outerKey))
                    {
                        continue;
                    }

                    foreach (var innerElement in innerKeys[outerKey])
                    {
                        yield return resultSelector(element, innerElement);
                    }
                }
            }
        }

        public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector)
        {
            return outer.Join(inner, outerKeySelector, innerKeySelector, resultSelector, null);
        }
    }
}

#endif