﻿using System;
using System.Collections.Generic;

namespace Theraot.Collections.ThreadSafe
{
    public interface IBucket<T> : IEnumerable<T>
    {
        int Capacity
        {
            get;
        }

        int Count
        {
            get;
        }

        void CopyTo(T[] array, int arrayIndex);

        bool Exchange(int index, T item, out T previous);

        bool Insert(int index, T item);

        bool Insert(int index, T item, out T previous);

        bool RemoveAt(int index);

        bool RemoveAt(int index, out T previous);

        bool RemoveValueAt(int index, T value, out T previous);

        void Set(int index, T item, out bool isNew);

        bool TryGet(int index, out T value);

        bool Update(int index, T item, T comparisonItem, IEqualityComparer<T> comparer, out T previous, out bool isEmpty);

        bool Update(int index, T item, Predicate<T> check, out T previous, out bool isEmpty);

        bool Update(int index, Func<T, T> itemUpdateFactory, out bool isEmpty);

        IEnumerable<T> Where(Predicate<T> predicate);
    }
}