#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.Remoting.Messaging;
using DynamicData.Kernel;
using DynamicData.Operators;

#endregion

namespace DynamicData
{

    /// <summary>
    /// The entry point for the dynamic data sub system
    /// </summary>
    public static class ObservableCache
    {
        /// <summary>
        /// Populate a cache from an obserable stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observable">The observable.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// keySelector
        /// </exception>
        public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<IEnumerable<TObject>> observable, Func<TObject, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (keySelector == null) throw new ArgumentNullException("keySelector");

            return observable.Subscribe(source.AddOrUpdate);
        }
        /// <summary>
        /// Populate a cache from an obserable stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observable">The observable.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// keySelector
        /// </exception>
        public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<TObject> observable, Func<TObject, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (keySelector == null) throw new ArgumentNullException("keySelector");

            return observable.Subscribe(source.AddOrUpdate);
        }


        #region Size / time limiters


        /// <summary>
        /// Limits the number of records in the cache to the size specified.  When the size is reached
        /// the oldest items are removed from the cache
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="sizeLimit">The size limit.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        /// <exception cref="System.ArgumentException">Size limit must be greater than zero</exception>
        public static IObservable<IEnumerable<KeyValuePair<TKey,TObject>>> LimitSizeTo<TObject, TKey>(this ISourceCache<TObject, TKey> source,
            int sizeLimit, IScheduler scheduler=null)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (sizeLimit<=0) throw new ArgumentException("Size limit must be greater than zero");

            return Observable.Create<IEnumerable<KeyValuePair<TKey,TObject>>>(observer =>
            {
                scheduler = scheduler ?? Scheduler.Default;

                var autoRemover = source.Connect()
                        .FinallySafe(observer.OnCompleted)
                        .Transform((t, v) => new ExpirableItem<TObject, TKey>(t, v, DateTime.Now))
                        .AsObservableCache();


                var sizeChecker = autoRemover.Connect()
                    .ObserveOn(scheduler)
                    .Select(changes =>
                            {
                                var itemstoexpire = autoRemover.KeyValues
                                    .OrderByDescending(exp => exp.Value.ExpireAt)
                                    .Skip(sizeLimit)
                                    .Select(exp => new KeyValuePair<TKey,TObject>(exp.Key, exp.Value.Value))
                                    .ToList();

                                return itemstoexpire;
                            })
                    .Subscribe(toRemove =>
                    {
                        try
                        {
                            //remove from cache and notify which items have been auto removed
                            source.Remove(toRemove.Select(kv => kv.Key));
                            observer.OnNext(toRemove.Select(kv => new KeyValuePair<TKey,TObject>(kv.Key, kv.Value))
                                .ToList());
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                        }
                    });
    

                return Disposable.Create(() =>
                {
                    sizeChecker.Dispose();
                    autoRemover.Dispose();
                });

            });
        }



        /// <summary>
        /// Automatically removes items from the cache after the time specified by
        /// the time selector elapses. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed</param>
        /// <param name="scheduler">The scheduler to perform the work on.</param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        public static IObservable<IEnumerable<KeyValuePair<TKey,TObject>>> AutoRemove<TObject, TKey>(this ISourceCache<TObject, TKey> source,
            Func<TObject, TimeSpan?> timeSelector, IScheduler scheduler = null)
        {
            return source.AutoRemove(timeSelector, null, scheduler);
        }

        /// <summary>
        /// Automatically removes items from the cache after the time specified by
        /// the time selector elapses. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed</param>
        /// <param name="interval">A polling interval.  Since multiple timer subscriptions can be expensive,
        /// it may be worth setting the interval .
        /// </param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        public static IObservable<IEnumerable<KeyValuePair<TKey,TObject>>> AutoRemove<TObject, TKey>(this ISourceCache<TObject, TKey> source,
            Func<TObject, TimeSpan?> timeSelector,TimeSpan? interval=null)
        {
            return AutoRemove(source, timeSelector, interval,  Scheduler.Default);
        }

        /// <summary>
        /// Automatically removes items from the cache after the time specified by
        /// the time selector elapses. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed</param>
        /// <param name="pollingInterval">A polling interval.  Since multiple timer subscriptions can be expensive,
        /// it may be worth setting the interval.
        /// </param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        public static IObservable<IEnumerable<KeyValuePair<TKey,TObject>>> AutoRemove<TObject, TKey>(this ISourceCache<TObject, TKey> source,
            Func<TObject, TimeSpan?> timeSelector, TimeSpan? pollingInterval, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (timeSelector == null) throw new ArgumentNullException("timeSelector");

            return Observable.Create<IEnumerable<KeyValuePair<TKey,TObject>>>(observer =>
            {
                scheduler = scheduler ?? Scheduler.Default;
                var autoRemover = source.Connect()
                
                    .ForAutoRemove(timeSelector, pollingInterval, scheduler)
                    .FinallySafe(observer.OnCompleted)
                    .Subscribe(toRemove =>
                    {
                        try
                        {
                            //remove from cache and notify which items have been auto removed
                            source.Remove(toRemove.Select(kv => kv.Key));
                            observer.OnNext(toRemove.Select(kv => new KeyValuePair<TKey,TObject>(kv.Key, kv.Value))
                                .ToList());
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                        }
                    });

                return Disposable.Create(() =>
                {

                   // removalSubscripion.Dispose();
                    autoRemover.Dispose();
                });

            });
        }

        #endregion

        #region Convenient update methods

        /// <summary>
        /// Adds or updates the cache with the specified item.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater=>updater.AddOrUpdate(item));
        }

        /// <summary>
        /// <summary>
        /// Adds or updates the cache with the specified items.
        /// </summary>
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater => updater.AddOrUpdate(items));
        }

        /// <summary>
        /// Removes the specified item from the cache. 
        /// 
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater => updater.Remove(item));
        }

        /// <summary>
        /// Removes the specified key from the cache.
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater => updater.Remove(key));
        }

        /// <summary>
        /// Removes the specified items from the cache. 
        /// 
        /// Any items not contained in the cache are ignored
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater => updater.Remove(items));
        }

        /// <summary>
        /// Removes the specified keys from the cache. 
        /// 
        /// Any keys not contained in the cache are ignored
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keys">The keys.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater => updater.Remove(keys));
        }


        /// <summary>
        /// Clears all data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Clear<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater => updater.Clear());
        }

        /// <summary>
        /// Signal observers to re-evaluate the specified item.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Evaluate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater => updater.Evaluate(item));
        }

        /// <summary>
        /// Signal observers to re-evaluate the specified items.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Evaluate<TObject, TKey>(this ISourceCache<TObject, TKey> source,  IEnumerable<TObject> items)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater => updater.Evaluate(items));
        }


        /// <summary>
        /// Removes the specified key from the cache.
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater => updater.Remove(key));
        }


        /// <summary>
        /// Removes the specified keys from the cache. 
        /// 
        /// Any keys not contained in the cache are ignored
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keys">The keys.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, IEnumerable<TKey> keys)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater => updater.Remove(keys));
        }



        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Clear<TObject, TKey>(this IIntermediateCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException("source");
            source.BatchUpdate(updater => updater.Clear());
        }
        #endregion

    }
}