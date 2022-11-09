using System;
using System.Collections.Generic;
using UnityEngine;
using IDObjectDictionary = System.Collections.Generic.Dictionary<int, UnityEngine.Object>;

#nullable enable
namespace HostGame
{
    public static class ComponentManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            intanceIDStorage = new(2048);
        }

        private static UnityObjectHashMap intanceIDStorage = new(2048);

        public static void AddObjectInstance(UnityEngine.Object obj)
        {
            intanceIDStorage.Add(obj);
        }

        public static void RemoveInstance(UnityEngine.Object obj)
        {
            intanceIDStorage.Remove(obj.GetInstanceID());
        }

        /// <summary>
        /// Will return null if object was destroyed or never added
        /// </summary>
        public static UnityEngine.Object? GetByInstanceID(int id)
        {
            var obj = intanceIDStorage.GetValue(id);
            return obj;
        }
    }
}