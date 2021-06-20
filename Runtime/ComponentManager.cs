using System;
using System.Collections.Generic;
using UnityEngine;

namespace HostGame
{
    public static class ComponentManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            componentsStorage = new Dictionary<GameObject, Dictionary<Type, List<object>>>();
        }

        private static
            Dictionary<GameObject, Dictionary<Type, List<object>>> componentsStorage = new Dictionary<GameObject, Dictionary<Type, List<object>>>();

        /// <summary>
        /// Will cache components only if this game object is not cached already
        /// </summary>
        public static Dictionary<Type, List<object>> TryRegisterComponents(GameObject go)
        {
            if (componentsStorage.TryGetValue(go, out var dic))
                return dic;

            var dict = ComponentHelpers.CacheComponents(go);
            componentsStorage[go] = dict;
            return dict;
        }

        public static void UnregisterComponents(GameObject go)
        {
            componentsStorage.Remove(go);
        }

        /// <summary>
        /// This call doesn't care if the component was deleted (or the object itself)
        /// </summary>
        public static T GetUnsafe<T>(GameObject go) where T : class
        {
            if (componentsStorage.TryGetValue(go, out var typeToComponents) &&
                 typeToComponents.TryGetValue(typeof(T), out var list))
            {
                if (list.Count > 0)
                    return (T)list[0];
            }

            return null;
        }

        public static T[] GetAll<T>(GameObject go) where T : class
        {
            if (componentsStorage.TryGetValue(go, out var typeToComponents) &&
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
            if (componentsStorage.TryGetValue(go, out var typeToComponents) &&
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