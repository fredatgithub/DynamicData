﻿using System;
using System.Linq;

namespace DynamicData.Kernel
{
    internal static class CacheEx
    {
        public static IChangeSet<TObject, TKey> AsInitialUpdates<TObject, TKey>(this ICache<TObject,TKey> source,
            Func<TObject, bool> filter = null)
        {
            var filtered = filter == null ? source.KeyValues : source.KeyValues.Where(kv => filter(kv.Value));
            var initialItems =filtered.Select(i => (Change<TObject, TKey>) new Change<TObject, TKey>(ChangeReason.Add, i.Key, i.Value));
            return new ChangeSet<TObject, TKey>(initialItems);
        }
    }
}