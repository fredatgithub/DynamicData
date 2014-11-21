using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Operators
{
    internal sealed class ManagedGroup<TObject, TKey, TGroupKey> : IDisposable, IGroup<TObject, TKey, TGroupKey>
    {
        private readonly TGroupKey _groupKey;
        private readonly IntermediateCache<TObject, TKey> _cache = new IntermediateCache<TObject, TKey>();

        public ManagedGroup(TGroupKey groupKey)
        {
            _groupKey = groupKey;
        }
        
        internal void Update(Action<IIntermediateUpdater<TObject, TKey>> updateAction)
        {
            _cache.BatchUpdate(updateAction);
        }

        internal int Count
        {
            get { return _cache.Count; }
        }

        internal IChangeSet<TObject, TKey>  GetInitialUpdates()
        {
            return _cache.GetInitialUpdates();
        }

        public TGroupKey Key
        {
            get { return _groupKey; }
        }

        public IObservableCache<TObject, TKey> Cache
        {
            get { return _cache; }
        }



        #region Equality members

        private bool Equals(ManagedGroup<TObject, TKey, TGroupKey> other)
        {
            return EqualityComparer<TGroupKey>.Default.Equals(_groupKey, other._groupKey);
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>; otherwise, false.
        /// </returns>
        /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>. </param>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ManagedGroup<TObject, TKey, TGroupKey> && Equals((ManagedGroup<TObject, TKey, TGroupKey>)obj);
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        public override int GetHashCode()
        {
            return EqualityComparer<TGroupKey>.Default.GetHashCode(_groupKey);
        }

        #endregion



        public  void Dispose()
        {
           _cache.Dispose();
        }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        public override string ToString()
        {
            return string.Format("Group: {0}", Key);
        }
    }
}