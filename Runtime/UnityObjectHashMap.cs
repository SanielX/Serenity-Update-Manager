using System;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Serenity
{
    /// <summary>
    /// Stores unity object as key:InstanceID -> value:Object pairs.
    /// </summary>
    /// 
    /// Is faster than System.Dictionary but just barely
    public class UnityObjectHashMap
    {
        public UnityObjectHashMap(int capacity = 256)
        {
            capacity = Mathf.NextPowerOfTwo(capacity);
            _values  = new KeyValuePair[capacity];

            _capacityMask = capacity - 1;
        }

        private int _count;
        private int _capacityMask;

        struct KeyValuePair
        {
            public int    Key;
            public Object Value;
        }

        private KeyValuePair[] _values;
        
        public int Count => _count;

        public void Clear()
        {
            _count = 0;

            Array.Clear(_values, 0, _values.Length);
        }

        public Object GetValue(int instanceID)
        {
            var index = instanceID & _capacityMask;

            for (int i = index; i < _values.Length; i++)
            {
                if (_values[i].Key == instanceID)
                    return _values[i].Value;
            }

            return null;
        }

        public bool Add(Object value)
        {
#if UNITY_ASSERTIONS
            if (value is null)
                throw new System.ArgumentNullException(nameof(value));
#endif

            int key   = value.GetInstanceID();
            return Add(key, value);
        }

        private bool Add(int key, Object value)
        {
            var index = key & _capacityMask;
            _count++;

            for (int i = index; i < _values.Length; i++)
            {
                if (_values[i].Key == key)
                    return false;

                if (_values[i].Key == 0)
                {
                    _values[i].Key   = key;
                    _values[i].Value = value;
                    return true;
                }
            }

            Grow();
            Add(key, value);
            return true;
        }

        public bool Remove(Object obj) => Remove(obj.GetInstanceID());

        public bool Remove(int key)
        {
#if UNITY_ASSERTIONS
            if (key == 0)
                throw new System.ArgumentException(nameof(key));
#endif

            var index = key & _capacityMask;

            for (int i = index; i < _values.Length; i++)
            {
                if (_values[i].Key == key)
                {
                    _values[i].Key   = 0;
                    _values[i].Value = null;
                    _count--;

                    return true;
                }
            }

            return false;
        }

        void Grow()
        {
            var capacity  = _values.Length * 2;

            var oldValues = _values;

            _capacityMask = capacity - 1;
            _values       = new KeyValuePair[capacity];

            for (int i = 0; i < oldValues.Length; i++)
            {
                if (oldValues[i].Value is not null)
                    Add(oldValues[i].Key, oldValues[i].Value);
            }
        }
    }
}