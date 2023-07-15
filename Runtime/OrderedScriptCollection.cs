using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Serenity
{
    /// <summary>
    /// Stores object grouped by type and ordered by execution index (if object is a <see cref="SRScript"/>)
    /// </summary>
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks,        false)]
    internal class OrderedScriptCollection<T> where T : class
    {
        public OrderedScriptCollection(int capacity = 32)
        {
            typeToBucketMap   = new(capacity);
            buckets           = new BucketMetadata[capacity];
            scripts           = new LightList<T>  [capacity];
        }

        private Dictionary<Type, LightList<T>> typeToBucketMap;

        public int Capacity   => buckets.Length;
        public int Count      => _count;

        internal LightList<T>[] Data => scripts;

        private int       _count;

        struct BucketMetadata
        {
            public int  executionIndex;
            public Type type;
        }
        
        private BucketMetadata[] buckets;
        private LightList<T>[]   scripts;

        void InsertBucket(int index)
        {
#if UNITY_ASSERTIONS
            if (index < 0 || index >= _count)
                throw new System.IndexOutOfRangeException($"Index '{index}' is out of range [0;{_count})");
#endif
            EnsureCapacityForNumberOfElements(1);

            Array.Copy(buckets,           index, buckets, index + 1, _count - index);
            Array.Copy(scripts,           index, scripts, index + 1, _count - index);

            buckets          [index] = default;
            scripts          [index] = new(32);
            _count++;
        }

        void AddBucket()
        {
            EnsureCapacityForNumberOfElements(1);

            buckets          [_count] = default;
            scripts          [_count] = new(16);
            _count++;
        }

        void RemoveBucketAt(int index)
        {
            // Have to preserve ordering here
            _count--;
            Array.Copy(buckets, index + 1, buckets, index, _count - index);
            Array.Copy(scripts, index + 1, scripts, index, _count - index);
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
                    typeToBucketMap.Remove(buckets[i].type);
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

            var executionIndex = SerenityScriptDataContainer.GetExecutionOrder(type);

            for (int i = 0; i < _count; i++)
            {
                if (buckets[i].type == type)
                {
                    scripts[i].Add(script);
                    return;
                }

                if (buckets[i].executionIndex > executionIndex)
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

                scripts[insertionIndex].Add(script);
                buckets[insertionIndex].type = type;
                buckets[insertionIndex].executionIndex = executionIndex;
            }
        }

        public void Remove(T script)
        {
            var type = script.GetType();

            for (int i = 0; i < _count; i++)
            {
                if (buckets[i].type == type)
                    scripts[i].RemoveBySwapping(script);
            }
        }

        public void Resize(int newSize)
        {
            var newBuckets = new BucketMetadata[newSize];
            var newData    = new LightList<T>[newSize];

            Array.Copy(buckets, newBuckets,   buckets.Length);
            Array.Copy(scripts, newData, scripts.Length);

            buckets = newBuckets;
            scripts = newData;
        }
    }
}