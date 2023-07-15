using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Serenity
{
    internal static class ComponentHelpers 
    {
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
