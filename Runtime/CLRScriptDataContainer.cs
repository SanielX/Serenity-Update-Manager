using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace HostGame
{
 //   [CreateAssetMenu(menuName = "HG/Execution Order Cntr")]
    class CLRScriptDataContainer : ScriptableObject
    {
        [System.Serializable]
        public class MonoOrderData
        {
            public string typeName;
            public int executionIndex;
        }

        [System.Serializable]
        public class CLRSetupData
        {
            public string typeName;
            public Calls usedCalls;
            public bool  noSafetyChecks;
            public bool  cacheComponents;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitCLRData()
        {
            var container = Resources.Load<CLRScriptDataContainer>(MAIN_CONT_NAME);
            container.RebuildDictionaries();
        }

        public const string MAIN_CONT_NAME = "CLR_Ex_Order";

        static MethodInfo SetupMethodInfo = typeof(CLRScript)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .First((info) => info.Name == "Setup" && info.IsVirtual && info.ReturnType == typeof(CLRSettings));

        private static Dictionary<string, Type>       CachedTypes = new Dictionary<string, Type>();
        private static Dictionary<Type, int>          ExecutionOrder     = new Dictionary<Type, int>();
        private static Dictionary<Type, CLRSetupData> SetupData   = new Dictionary<Type, CLRSetupData>();

        public List<MonoOrderData> executionOrderData;
        public List<CLRSetupData> clrSetups;

        [ContextMenu("Get scripts order")]
        private void OnEnable()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            UnityEditor.EditorUtility.SetDirty(this);

            executionOrderData ??= new List<MonoOrderData>();
            clrSetups ??= new List<CLRSetupData>();

            executionOrderData.Clear();
            clrSetups.Clear();

            foreach (var monoScript in UnityEditor.MonoImporter.GetAllRuntimeMonoScripts())
            {
                Type type = monoScript.GetClass();
                if (!monoScript || type == null || !ComponentHelpers.IsInherited(type, typeof(CLRScript)))
                    continue;

                DefaultExecutionOrder executionAttr = type.GetCustomAttribute<DefaultExecutionOrder>();
                int order = UnityEditor.MonoImporter.GetExecutionOrder(monoScript);
                if (order == 0 && executionAttr != null)
                {
                    order = executionAttr.order;
                }

                executionOrderData.Add(new MonoOrderData()
                {
                    typeName = type.FullName,
                    executionIndex = order
                });

                if(!ComponentHelpers.HasOverride(SetupMethodInfo, type))
                {
                    var settings = CLRScript.DefaultSetupFunction(type, out var cache);

                    var setupData = new CLRSetupData()
                    {
                        typeName = type.FullName,
                        usedCalls = settings.UsedCalls,
                        cacheComponents = cache,
                        noSafetyChecks = settings.NoSafetyChecks
                    };

                    clrSetups.Add(setupData);
                }
            }
#endif

            RebuildDictionaries();

#if UNITY_EDITOR
            executionOrderData.Sort((x, y) => x.executionIndex.CompareTo(y.executionIndex));
            bool rebuildNeeded = true;

            int sanityCheck = 0;
            while (rebuildNeeded)
            {
                rebuildNeeded = false;

                sanityCheck++;
                if (sanityCheck > 10_000)
                {
                    throw new System.Exception("You have created infinite loop by using Run.After/Before attributes");
                }

                foreach (var data in this.executionOrderData)
                {
                    Type type = FindType(data.typeName);
                    var beforeAttr = type.GetCustomAttribute<Run.BeforeAttribute>(true);
                    var afterAttr = type.GetCustomAttribute<Run.AfterAttribute>(true);
                    int myOrder = ExecutionOrder[type];

                    if (beforeAttr != null)
                    {
                        int targetOrder = ExecutionOrder[beforeAttr.targetType];

                        if (targetOrder <= myOrder)
                        {
                            data.executionIndex = targetOrder - 1;
                            rebuildNeeded = true;
                        }
                    }

                    if (afterAttr != null)
                    {
                        int targetOrder = ExecutionOrder[afterAttr.targetType];

                        if (myOrder <= targetOrder)
                        {
                            data.executionIndex = targetOrder + 1;
                            rebuildNeeded = true;
                        }
                    }
                }

                if (rebuildNeeded)
                {
                    executionOrderData.Sort((x, y) => x.executionIndex.CompareTo(y.executionIndex));
                    RebuildDictionaries();
                }
            }

            for (int i = 0; i < executionOrderData.Count; i++)
            {
                MonoOrderData data = this.executionOrderData[i];
                if(data.executionIndex == 0)
                {
                    this.executionOrderData.RemoveAt(i);
                    i--;
                }
            }
#endif
        }

        private void RebuildDictionaries()
        {
            ExecutionOrder.Clear();
            SetupData.Clear();

            foreach (var script in executionOrderData)
            {
                ExecutionOrder[FindType(script.typeName)] = script.executionIndex;
            }

            foreach(var scriptData in clrSetups)
            {
                SetupData[FindType(scriptData.typeName)] = scriptData;
            }
        }

        public static int GetExecutionOrder(Type type)
        {
            if (ExecutionOrder.TryGetValue(type, out int val))
                return val;
            else
                return 0;
        }

        public static bool TryGetDefaultSetup(Type type, out CLRSettings settings, out bool cacheComponents)
        {
            if(SetupData.TryGetValue(type, out var data))
            {
                settings = new CLRSettings(data.usedCalls, data.noSafetyChecks);
                cacheComponents = data.cacheComponents;
                return true;
            }

            settings = default;
            cacheComponents = false;
            return false;
        }

        static readonly Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        static Type FindType(string name)
        {
            Type result;
            if (CachedTypes.TryGetValue(name, out result))
                return result;

            foreach (var assembly in assemblies)
            {
                result = assembly.GetType(name, false);
                if (result != null)
                {
                    CachedTypes[name] = result;
                    break;
                }
            }
            return result;
        }
    }
}
