using System;
using System.Collections.Generic;
using UnityEngine;

namespace HostGame
{
    /// <summary>
    /// Stores object grouped by type and ordered by execution index (if object is a <see cref="CLRScript"/>)
    /// </summary>
    public class OrderedScriptCollection<T> where T : class
    {
        public OrderedScriptCollection(int capacity = 32)
        {
            typeToBucketMap   = new(capacity);
            executionIndicies = new int    [capacity];
            scriptTypes       = new Type   [capacity];
            scripts           = new List<T>[capacity];
        }

        private Dictionary<Type, List<T>> typeToBucketMap;

        public int Capacity   => executionIndicies.Length;
        public int Count      => _count;

        public List<T>[] Data => scripts;

        private int       _count;
        
        private int[]     executionIndicies;
        private Type[]    scriptTypes;
        private List<T>[] scripts;

        void InsertBucket(int index)
        {
#if UNITY_ASSERTIONS
            if (index < 0 || index >= _count)
                throw new System.IndexOutOfRangeException($"Index '{index}' is out of range [0;{_count})");
#endif
            EnsureCapacityForNumberOfElements(1);

            Array.Copy(executionIndicies, index, executionIndicies, index + 1, _count - index);
            Array.Copy(scriptTypes,       index, scriptTypes,       index + 1, _count - index);
            Array.Copy(scripts,           index, scripts,           index + 1, _count - index);

            executionIndicies[index] = 0;
            scriptTypes      [index] = null;
            scripts          [index] = new(16);
            _count++;
        }

        void AddBucket()
        {
            EnsureCapacityForNumberOfElements(1);

            executionIndicies[_count] = 0;
            scriptTypes      [_count] = null;
            scripts          [_count] = new(16);
            _count++;
        }

        void RemoveBucketAt(int index)
        {
            Array.Copy(executionIndicies, index + 1, executionIndicies, index, _count - index - 1);
            Array.Copy(scriptTypes,       index + 1, scriptTypes,       index, _count - index - 1);
            Array.Copy(scripts,           index + 1, scripts,           index, _count - index - 1);
            _count--;
        }

        void EnsureCapacityForNumberOfElements(int num)
        {
            if (Count + num > Capacity)
                Resize(Mathf.NextPowerOfTwo(Capacity));
        }

        public void ClearUnusedSlots()
        {
            var oldCount = _count;
            for (int i = 0; i < _count; i++)
            {
                if(scripts[i].Count == 0)
                {
                    RemoveBucketAt(i);
                    typeToBucketMap.Remove(scriptTypes[i]);
                }
            }

            Array.Fill(scripts, null, _count, oldCount - _count);
        }

        public void Add(T script)
        {
            var type = script.GetType();
            if (_count > 64 && typeToBucketMap.TryGetValue(type, out var scriptList))
            {
                scriptList.Add(script);
                return;
            }

            var executionIndex = CLRScriptDataContainer.GetExecutionOrder(type);

            for (int i = 0; i < _count; i++)
            {
                if (scriptTypes[i] == type)
                {
                    scripts[i].Add(script);
                    return;
                }

                if (executionIndicies[i] > executionIndex)
                {
                    InsertBucket(i);
                    AddScriptAtIndex(script, type, executionIndex, i);
                    return;
                }
            }

            AddBucket();
            int insertionIndex = _count - 1;
            
            AddScriptAtIndex(script, type, executionIndex, insertionIndex);

            void AddScriptAtIndex(T script, Type type, int executionIndex, int insertionIndex)
            {
                typeToBucketMap[type] = scripts[insertionIndex];

                scripts          [insertionIndex].Add(script);
                scriptTypes      [insertionIndex] = type;
                executionIndicies[insertionIndex] = executionIndex;
            }
        }

        public void Remove(T script)
        {
            var type = script.GetType();

            for (int i = 0; i < _count; i++)
            {
                if (scriptTypes[i] == type)
                    scripts[i].Remove(script);
            }
        }

        public void Resize(int newSize)
        {
            var newIndicies = new int    [newSize];
            var newTypes    = new Type   [newSize];
            var newBuckets  = new List<T>[newSize];

            Array.Copy(executionIndicies, newIndicies, executionIndicies.Length);
            Array.Copy(scriptTypes,       newTypes,    scriptTypes.Length);
            Array.Copy(scripts,           newBuckets,  scripts.Length);

            executionIndicies = newIndicies;
            scriptTypes       = newTypes;
            scripts           = newBuckets;
        }
    }
}