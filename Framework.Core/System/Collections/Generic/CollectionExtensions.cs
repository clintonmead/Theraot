﻿#if NET20 || NET30 || NET35 || NET40 || NET45 || NET46 || NET47 || NETCOREAPP1_0 || NETCOREAPP1_1 || NETSTANDARD1_5 || NETSTANDARD1_6

namespace System.Collections.Generic
{
    public static class CollectionExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }
            return dictionary.TryGetValue(key, out var value) ? value : default;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }
            try
            {
                dictionary.Add(key, value);
                return true;
            }
            catch (ArgumentException ex)
            {
                GC.KeepAlive(ex);
                return false;
            }
        }

        public static bool Remove<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, out TValue value)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }
            if (dictionary.TryGetValue(key, out value) && dictionary.Remove(key))
            {
                return true;
            }
            value = default;
            return false;
        }
    }
}

#endif