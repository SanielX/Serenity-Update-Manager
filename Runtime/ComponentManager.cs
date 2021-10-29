using System;
using System.Collections.Generic;
using UnityEngine;
using IDObjectDictionary = System.Collections.Generic.Dictionary<int, UnityEngine.Object>;
using IDComponentDictionary = System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<System.Type, System.Collections.Generic.List<object>>>;

#nullable enable
namespace HostGame
{
    public static class ComponentManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            componentsStorage = new IDComponentDictionary();
            intanceIDStorage  = new IDObjectDictionary();
        }

        private static IDComponentDictionary componentsStorage = new IDComponentDictionary();
        private static IDObjectDictionary intanceIDStorage = new IDObjectDictionary();

        public static void AddObjectInstance(UnityEngine.Object obj, bool throwIfExists = true)
        {
            if (!throwIfExists && intanceIDStorage.ContainsKey(obj.GetInstanceID()))
                return;

            intanceIDStorage.Add(obj.GetInstanceID(), obj);
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
            if (intanceIDStorage.TryGetValue(id, out var instance))
            {
                // Check if object was destroyed before returning it
                // and if it was, remove it from dictionary
                if (instance)
                {
                    return instance;
                }
                else
                {
                    intanceIDStorage.Remove(id);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Will cache components only if this game object is not cached already
        /// </summary>
        public static Dictionary<Type, List<object>> TryRegisterComponents(GameObject go)
        {
            if (componentsStorage.TryGetValue(go.GetInstanceID(), out var dic))
                return dic;

            var dict = ComponentHelpers.GetAllComponents(go);
            componentsStorage[go.GetInstanceID()] = dict;
            return dict;
        }

        public static void UnregisterComponents(GameObject go)
        {
            componentsStorage.Remove(go.GetInstanceID());
        }

        /// <summary>
        /// This call doesn't care if the component was deleted (or the object itself)
        /// </summary>
        public static T? GetUnsafe<T>(GameObject go) where T : class
        {
            if (componentsStorage.TryGetValue(go.GetInstanceID(), out var typeToComponents) &&
                 typeToComponents.TryGetValue(typeof(T), out var list))
            {
                if (list.Count > 0)
                    return (T)list[0];
            }

            return null;
        }

        public static T[]? GetAll<T>(GameObject go) where T : class
        {
            if (componentsStorage.TryGetValue(go.GetInstanceID(), out var typeToComponents) &&
                 typeToComponents.TryGetValue(typeof(T), out var list) &&
                 list.Count > 0)
            {
                T[] result = new T[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    result[i] = (T)list[i];
                }

                return result;
            }

            return null;
        }

        public static int GetAllNonAlloc<T>(ref T[] arr, GameObject go) where T : class
        {
            if (componentsStorage.TryGetValue(go.GetInstanceID(), out var typeToComponents) &&
                typeToComponents.TryGetValue(typeof(T), out var list)
                && list.Count > 0)
            {
                int min = Mathf.Min(arr.Length, list.Count);
                for (int i = 0; i < min; i++)
                {
                    arr[i] = (T)list[i];
                }

                return min;
            }

            return 0;
        }
    }
}