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

        public static bool IsInherited(Type type, Type from)
        {
            while (type != null)
            {
                if (type.BaseType == from)
                    return true;

                type = type.BaseType;
            }

            return false;
        }
    }

    public static class ComponentExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? GetUnsafe<T>(this GameObject go) where T : class
        {
            return ComponentManager.GetUnsafe<T>(go);
        }

        /// <summary>
        /// This will get component from global cache which is not guaranteed to return actual value. 
        /// Much more performant than GetComponent
        /// If CLRScript has caching enabled components will be available after Awake call from Unity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? GetUnsafe<T>(this Component cmp) where T : class
        {
            return ComponentManager.GetUnsafe<T>(cmp.gameObject);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetUnsafe<T>(this Component cmp, out T? result) where T : class
        {
            result = GetUnsafe<T>(cmp);
            return result != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetUnsafe<T>(this GameObject go, out T? result) where T : class
        {
            result = GetUnsafe<T>(go);
            return result != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[]? GetAllUnsafe<T>(this Component cmp) where T : class
        {
            return ComponentManager.GetAll<T>(cmp.gameObject);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetAllUnsafeNonAlloc<T>(this Component cmp, ref T[] writeTo) where T : class
        {
            return ComponentManager.GetAllNonAlloc<T>(ref writeTo, cmp.gameObject);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetAllUnsafeNonAlloc<T>(this GameObject go, ref T[] writeTo) where T : class
        {
            return ComponentManager.GetAllNonAlloc<T>(ref writeTo, go);
        } 
    }
}
