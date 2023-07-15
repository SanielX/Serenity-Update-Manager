using System;
using System.Collections.Generic;
using System.Reflection;

namespace Serenity
{
    /// <summary>
    /// Utility that allows caching/finding classess by their full name (e.g. UnityEngine.GameObject).
    /// Basically same as <see cref="UnityEditor.TypeCache"/> but for runtime
    /// </summary>
    public static class GlobalTypeCache
    {
        private static Dictionary<string, Type> CachedTypes = new Dictionary<string, Type>(256);
        private static Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        
        public static Func<string, Type> FindTypeDelegate { get; set; }
        
        /// <summary>
        /// Finds type in all assemblies by name.
        /// Caches result even if it was null.
        /// </summary>
        public static Type FindType(string fullName)
        {
            if(FindTypeDelegate is not null)
                return FindTypeDelegate(fullName);
            
            if (string.IsNullOrEmpty(fullName))
                return null;

            Type result;
            if (CachedTypes.TryGetValue(fullName, out result))
                return result;

            foreach (var assembly in assemblies)
            {
                result = assembly.GetType(fullName, false);
                
                if (result != null)
                {
                    CachedTypes[fullName] = result;
                    return result;
                }
            }

            CachedTypes[fullName] = null;
            return null;
        }

        internal static void CacheCLRScripts()
        {
            foreach (var asm in assemblies)
            {
                string fullName = asm.FullName;
                if (fullName.StartsWith("UnityEngine.") || fullName.StartsWith("UnityEditor.") || fullName.StartsWith("Unity."))
                    continue;

                foreach (var type in asm.GetTypes())
                {
                    if ((type.IsAbstract && type.IsSealed) || type.IsValueType || type.IsInterface)
                        continue;

                    if (type.IsSubclassOf(typeof(SRScript)))
                    {
                        CachedTypes[type.FullName] = type;
                    }
                }
            }
        }
    }
}
