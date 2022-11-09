using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable enable
namespace HostGame
{
    internal static class ComponentHelpers 
    {
        public static List<Type> GetAllCasts(Type t)
        {
            List<Type> allCasts = new List<Type>(4);
            allCasts.Add(t);
            allCasts.AddRange(t.GetInterfaces());

            t = t.BaseType;
            while (t != null)
            {
                allCasts.Add(t);
                t = t.BaseType;

                if (t == typeof(Component))
                {
                    allCasts.Add(t);
                    break;
                }
            }

            return allCasts;
        }

        public static Dictionary<Type, List<object>> GetAllComponents(GameObject go)
        {
            var typeToComponents = new Dictionary<Type, List<object>>();
            var allComponents = go.GetComponents<Component>();

            foreach (var comp in allComponents)
            {
                if(!comp)
                {
                    Debug.LogError($"Component on game object {go.name} is null. Check for missing scripts", go);
                    continue;
                }

                List<Type> myTypes = GetAllCasts(comp.GetType());

                foreach (var type in myTypes)
                {
                    if (!typeToComponents.TryGetValue(type, out _))
                    {
                        typeToComponents[type] = new List<object>();
                    }

                    typeToComponents[type].Add(comp);
                }
            }

            return typeToComponents;
        }

        public static bool HasOverride(MethodInfo baseMethod, Type type)
        {
            while (type != baseMethod.ReflectedType)
            {
                var methods = type.GetMethods(BindingFlags.Instance     |
                                              BindingFlags.DeclaredOnly |
                                              BindingFlags.Public       |
                                              BindingFlags.NonPublic);

                for(int i = 0; i < methods.Length; i++)
                {
                    if (methods[i].GetBaseDefinition() == baseMethod)
                        return true;
                }

                type = type.BaseType;
            }
            return false;
        }
    }

    public static class ComponentExtensions
    {
        /// <summary>
        /// This will get component from global cache which is not guaranteed to return actual value. 
        /// Much more performant than GetComponent
        /// If CLRScript has caching enabled components will be available after Awake call from Unity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [System.Obsolete("Use 'GetComponent' instead")]
        public static T? GetUnsafe<T>(this Component cmp) where T : class
        {
            return cmp.GetComponent<T>();
        }
    }
}
