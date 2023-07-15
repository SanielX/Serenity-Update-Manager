using System;
using Unity.IL2CPP.CompilerServices;

namespace Serenity
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    internal class LightList<T> where T : class
    {
        public LightList(int capacity = 64)
        {
            _array = new T[capacity];
        }

        private int _count;
        private T[] _array;

        public int Capacity => _array.Length;

        public int Count    => _count;

        public ref T GetArrayRef() => ref _array[0]; // When .NET7 comes replace this with MemoryMarshal.GetArrayDataReference
                                                     // It basically allows to remove bound checks in both mono and IL2CPP

        public T GetItemWithoutChecks(int index) => _array[index];

        public void Add(T item)
        {
            EnsureCapacityForItem(1);
            _array[_count++] = item;
        }

        public int IndexOf(T item)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_array[i] == item)
                    return i;
            }

            return -1;
        }

        /// <inheritdoc cref="RemoveAtBySwapping(int)"/>
        public void RemoveBySwapping(T item)
        {
            var index = IndexOf(item);
            if (index >= 0)
                RemoveAtBySwapping(index);
        }

        /// <summary>
        /// Removes item from list very fast but doesn't preserve ordering
        /// </summary>
        public void RemoveAtBySwapping(int index)
        {
            // [8, 2, 42, 7] _count = 4
            // removing at index 3
            // will assign 7 to itself
            // then clear it because _count is now equals to index
            //
            // if removing at index 0
            // then we'll first assign 7 to [0] position getting list like this
            // [7, 2, 42, 7]
            // then we can set 7 to 0, because reference is stored there and we don't want to hold this pointer any longer
            _array[index]  = _array[--_count];
            _array[_count] = default;
        }

        void EnsureCapacityForItem(int itemCount)
        {
            if (_count + itemCount > Capacity)
                Resize(Capacity * 2);
        }

        void Resize(int newSize)
        {
            var oldArray = _array;

            _array = new T[newSize];

            Array.Copy(oldArray, _array, _count);
        }
    }
}