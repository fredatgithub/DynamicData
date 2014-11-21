﻿using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;

namespace DynamicData.Operators
{
    internal class FilteredIndexCalulator<TObject, TKey>
    {
        
        public IList<Change<TObject, TKey>> Calculate(IKeyValueCollection<TObject, TKey> currentItems,
            IKeyValueCollection<TObject, TKey> previousItems,IChangeSet<TObject, TKey> sourceUpdates)
        {
            {
                List<KeyValuePair<TKey,TObject>> previousList = previousItems.ToList();
                var keyComparer =new KeyComparer<TObject, TKey>();
                
                var removes = previousItems.Except(currentItems,keyComparer).ToList();
                var adds = currentItems.Except(previousItems,keyComparer).ToList();
                var inbothKeys = previousItems.Intersect(currentItems, keyComparer)
                                .Select(x => x.Key).ToHashSet();

                var result = new List<Change<TObject, TKey>>();
                foreach (var remove in removes)
                {
                    var index = previousList.BinarySearch(remove, currentItems.Comparer);
                    previousList.RemoveAt(index);
                    result.Add(new Change<TObject, TKey>(ChangeReason.Remove, remove.Key, remove.Value, index));
                }

                foreach (var add in adds)
                {
                    //find new insert position
                    int index = previousList.BinarySearch(add, currentItems.Comparer);
                    int insertIndex = ~index;
                    previousList.Insert(insertIndex, add);
                    result.Add(new Change<TObject, TKey>(ChangeReason.Add, add.Key, add.Value, insertIndex));
                }


                //Adds and removes ahave been accounted for 
                //so check whether anything in the remaining change set have been moved ot updated
                var remainingItems = sourceUpdates
                    .EmptyIfNull()
                    .Where(u =>inbothKeys.Contains(u.Key) 
                            &&  (u.Reason == ChangeReason.Update
                                || u.Reason == ChangeReason.Moved
                                || u.Reason == ChangeReason.Evaluate))
                    .ToList();


                foreach (var change in remainingItems)
                {
                    if (change.Reason == ChangeReason.Update)
                    {
                        var current = new KeyValuePair<TKey,TObject>(change.Key, change.Current);
                        var previous = new KeyValuePair<TKey,TObject>(change.Key, change.Previous.Value);

                        //remove from the actual index
                        var removeIndex = previousList.IndexOf(previous);
                        previousList.RemoveAt(removeIndex);

                        //insert into the desired index
                        int desiredIndex = previousList.BinarySearch(current, currentItems.Comparer);
                        int insertIndex = ~desiredIndex;
                        previousList.Insert(insertIndex, current);

                        result.Add(new Change<TObject, TKey>(ChangeReason.Update, current.Key, current.Value,
                            previous.Value, insertIndex, removeIndex));

                    }
                    else if (change.Reason == ChangeReason.Moved )
                    {
                        //TODO:  We have the index already, would be more efficient to calculate new position from the original index
                        var current = new KeyValuePair<TKey,TObject>(change.Key, change.Current);

                        var previousindex = previousList.IndexOf(current);
                        int desiredIndex = previousList.BinarySearch(current, currentItems.Comparer);
                        int insertIndex = ~desiredIndex;

                        //this should never be the case, but check anyway
                        if (previousindex == insertIndex)
                        {
                            continue;
                        }

                        previousList.RemoveAt(previousindex);
                        previousList.Insert(insertIndex, current);
                        result.Add(new Change<TObject, TKey>(current.Key, current.Value, insertIndex, previousindex));
                    }
                    else
                    {

                        //TODO: re-evaluate to check whether item should be moved
                        result.Add(change);
                    }

                }

                //Alternative to evaluate is to check order
                var evaluates = remainingItems.Where(c => c.Reason == ChangeReason.Evaluate)
                    .OrderByDescending(x => new KeyValuePair<TKey,TObject>(x.Key, x.Current), currentItems.Comparer)
                    .ToList();

                    //calculate moves.  Very expensive operation
                    //TODO: Try and make this better
                    foreach (var u in evaluates)
                    {
                        var current = new KeyValuePair<TKey,TObject>(u.Key, u.Current);
                        var old = previousList.IndexOf(current);
                        int newposition = GetInsertPositionLinear(previousList, current, currentItems.Comparer);

                        if (old < newposition)
                        {
                            newposition--;
                        }

                        if (old == newposition)
                        {
                            continue;
                        }

                        previousList.RemoveAt(old);
                        previousList.Insert(newposition, current);
                        result.Add(new Change<TObject, TKey>(u.Key, u.Current, newposition, old));
                    }
                

                return result;
            }
             
        }


        private int GetInsertPositionLinear(IList<KeyValuePair<TKey,TObject>> list, KeyValuePair<TKey,TObject> item,
            IComparer<KeyValuePair<TKey,TObject>> comparer)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (comparer.Compare(item, list[i]) < 0)
                {
                    return i;
                }
            }
            return list.Count;
        }

    }

}
