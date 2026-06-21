using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


/// <summary>
/// When you declare an EventWeightedList, you can trust anything with a different weight will fire in correct order. Bigger number = fires later.
/// Anything with the same weight may fire before or after others of the same weight.
/// </summary>
/// <typeparam name="TArg"></typeparam>
public class EventWeightedList<TArg>
{
    private WeightedList<Action<TArg>> weightedList;

    protected List<int> removeIndexList = new();
    protected List<(Action<TArg> item, EventOrder weight)> addEntryList = new();
    protected List<(Action<TArg> item, EventOrder weight)> addUniqueEntryList = new();

    bool inLoop = false;

    public EventWeightedList()
    {
        weightedList = new WeightedList<Action<TArg>>();
    }

    public void Invoke(TArg arg)
    {
        // Clean up any previously requested removes
        ProcessRemoveList();
        ProcessAddUniqueList();
        ProcessAddList();

        inLoop = true;
        // the list is safe to iterate through now
        if (weightedList.HasItems)
        {
            foreach(var _event in weightedList.itemEnumerables)
            {
                try
                {
                    _event.Invoke(arg);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed iterating EventWeightedList\r\n" + ex.StackTrace);
                }
            }
        }

        inLoop = false;
    }
    /// <summary>
    /// Will only add the callback if it's not already been added.
    /// </summary>
    /// <param name="newDelegate"></param>
    /// <param name="eventOrderIndex"></param>
    public void AddUnique(Action<TArg> newDelegate, EventOrder eventOrderIndex)
    {
        if (inLoop)
        {
            CacheAddUnique(newDelegate, eventOrderIndex);
        }
        else
        {
            weightedList.AddUnique(newDelegate, (int)eventOrderIndex);
        }
    }
    public void Add(Action<TArg> newDelegate, EventOrder eventOrderIndex)
    {
        if (inLoop)
        {
            CacheAdd(newDelegate, eventOrderIndex);
        }
        else
        {
            weightedList.Add(newDelegate,(int)eventOrderIndex);
        }
    }
    public void Remove(Action<TArg> oldDelegate)
    {
        if (inLoop)
        {
            CacheRemove(oldDelegate);
        }
        else
        {
            weightedList.Remove(oldDelegate);
        }
    }


    #region Safety-related adding / removing

    private void CacheAddUnique(Action<TArg> newDelegate, EventOrder eventOrderIndex)
    {
        addUniqueEntryList.Add((newDelegate, eventOrderIndex));
    }

    /// <summary>
    /// Caches the add call so we don't throw a "Collection Modified" enumeration error.
    /// </summary>
    /// <param name="item"></param>
    /// <param name="weight"></param>
    private void CacheAdd(Action<TArg>item, EventOrder weight)
    {
        addEntryList.Add((item, weight));
    }

    /// <summary>
    /// Caches the remove call so we don't throw a "Collection Modified" enumeration error.
    /// </summary>
    /// <param name="item"></param>
    private void CacheRemove(Action<TArg> item)
    {
        int index = weightedList.IndexOf(item);

        if(index != -1 && removeIndexList.Contains(index) == false)
        {
            removeIndexList.Add(index);
        }
    }

    private void ProcessAddUniqueList()
    {
        if(addUniqueEntryList.Count < 1)
        {
            return;
        }

        foreach(var entry in addUniqueEntryList)
        {
            weightedList.AddUnique(entry.item, (int)entry.weight);
        }

        addUniqueEntryList.Clear();
    }

    private void ProcessAddList()
    {
        if(addEntryList.Count < 1)
        {
            return;
        }

        foreach (var entry in addEntryList)
        {
            weightedList.Add(entry.item, (int)entry.weight);
        }

        addEntryList.Clear();
    }

    private void ProcessRemoveList()
    {
        if(removeIndexList.Count < 1)
        {
            return;
        }
        // We need to process from back to front, so we don't disturb the list order
        removeIndexList.Sort((int a, int b) => { return a - b; });

        foreach(var index in removeIndexList)
        {
            weightedList.RemoveAt(index);
        }

        removeIndexList.Clear();
    }

    #endregion
    public void Clear()
    {
        if (inLoop)
        {
            addEntryList.Clear();
            removeIndexList.Clear();
            int length = weightedList.Count;
            for(var i = 0; i < length; i++)
            {
                removeIndexList.Add(i);
            }
            return;
        }

        weightedList.Clear();
        removeIndexList.Clear();
        addEntryList.Clear();
        addUniqueEntryList.Clear();
    }
}

public class WeightedList<T>
{
    protected List<int> weights = new();
    protected List<T> items = new();

    public bool HasItems => items.Count > 0;
    public int Count => items.Count;
    public IEnumerable<T> itemEnumerables => items.AsReadOnly();

    public bool Contains(T item)
    {
        return items.Contains(item);
    }

    public int IndexOf(T item)
    {
        return items.IndexOf(item);
    }

    // There's currently a bug where something might get added multiple times.
    public void AddUnique(T item, int weight)
    {
        if (items.Contains(item)) 
        {
            return;
        }

        Add(item, weight);
    }

    public void Add(T item, int weight)
    {
        int weightsCount = weights.Count;
        
        if(weightsCount == 0 || weight > weights[weightsCount-1])
        {
            weights.Add(weight);
            items.Add(item);
            return;
        }

        for(var i = 0; i < weightsCount; i++)
        {
            if(weights[i] >= weight)
            {
                AddItem(i, weight, item);
                return;
            }
        }

        Debug.LogError($"Attempted to add an element but couldn't find a valid position to do it!");
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddItem(int index, int _weight, T _item) 
        {
            weights.Insert(index, _weight);
            items.Insert(index, _item);
        }
    }

    public void Remove(T item)
    {
        int indexOf = items.IndexOf(item);

        if(indexOf != -1)
        {
            items.RemoveAt(indexOf);
            weights.RemoveAt(indexOf);
        }
    }

    public void RemoveAt(int index)
    {
        if(index < 0 || index >= items.Count)
        {
            return;
        }

        items.RemoveAt(index);
        weights.RemoveAt(index);
    }

    public void Clear()
    {
        weights.Clear();
        items.Clear();
    }
}
