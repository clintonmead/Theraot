// Needed for NET40

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Theraot.Collections
{
    [DebuggerNonUserCode]
    public class ProgressiveList<T> : ProgressiveCollection<T>, IList<T>
    {
        private readonly IList<T> _cache;

        public ProgressiveList(IEnumerable<T> wrapped)
            : this(wrapped, new List<T>(), null)
        {
            // Empty
        }

        public ProgressiveList(IObservable<T> wrapped)
            : this(wrapped, new List<T>(), null)
        {
            // Empty
        }

        public ProgressiveList(IEnumerable<T> wrapped, IEqualityComparer<T> comparer)
            : this(wrapped, new List<T>(), comparer)
        {
            // Empty
        }

        public ProgressiveList(IObservable<T> wrapped, IEqualityComparer<T> comparer)
            : this(wrapped, new List<T>(), comparer)
        {
            // Empty
        }

        protected ProgressiveList(IEnumerable<T> wrapped, IList<T> cache, IEqualityComparer<T> comparer)
            : base(wrapped, cache, comparer)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            Cache = new ExtendedReadOnlyList<T>(_cache);
        }

        protected ProgressiveList(IObservable<T> wrapped, IList<T> cache, IEqualityComparer<T> comparer)
            : base(wrapped, cache, comparer)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            Cache = new ExtendedReadOnlyList<T>(_cache);
        }

        public new IReadOnlyList<T> Cache { get; }

        public T this[int index]
        {
            get
            {
                if (index >= _cache.Count)
                {
                    Progressor.While(() => _cache.Count < index + 1).Consume();
                }
                return _cache[index];
            }
        }

        T IList<T>.this[int index]
        {
            get => this[index];

            set => throw new NotSupportedException();
        }

        public new static ProgressiveList<T> Create<TList>(IEnumerable<T> wrapped, IEqualityComparer<T> comparer)
            where TList : IList<T>, new()
        {
            return new ProgressiveList<T>(wrapped, new TList(), comparer);
        }

        public new static ProgressiveList<T> Create<TList>(IObservable<T> wrapped, IEqualityComparer<T> comparer)
            where TList : IList<T>, new()
        {
            return new ProgressiveList<T>(wrapped, new TList(), comparer);
        }

        void ICollection<T>.Add(T item)
        {
            throw new NotSupportedException();
        }

        void ICollection<T>.Clear()
        {
            throw new NotSupportedException();
        }

        public int IndexOf(T item)
        {
            var cacheIndex = _cache.IndexOf(item, Comparer);
            if (cacheIndex >= 0)
            {
                return cacheIndex;
            }
            var index = _cache.Count - 1;
            var found = false;
            Progressor.While
            (
                input =>
                {
                    index++;
                    if (Comparer.Equals(input, item))
                    {
                        found = true;
                        return false;
                    }
                    return true;
                }
            ).Consume();
            if (found)
            {
                return index;
            }
            return -1;
        }

        void IList<T>.Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        void IList<T>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        protected override bool CacheContains(T item)
        {
            return _cache.Contains(item, Comparer);
        }
    }
}