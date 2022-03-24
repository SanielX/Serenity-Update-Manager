using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif

namespace HostGame
{
    internal class CLRScriptDataContainer : ScriptableObject
    {
        [System.Serializable]
        struct CLROrderData
        {
            public string typeName;
            public int executionIndex;
        }

        [System.Serializable]
        struct CLRSetupData
        {
            public string typeName;
            public string baseExecutionTypeName;

            public CLRSetupFlags flags;
        }

        class CLROrderDataComparer : IComparer<CLROrderData>
        {
            public static CLROrderDataComparer instance = new CLROrderDataComparer();

            public int Compare(CLROrderData x, CLROrderData y)
            {
                return x.executionIndex.CompareTo(y.executionIndex);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitCLRData()
        {
            var container = Resources.Load<CLRScriptDataContainer>(MAIN_CONT_NAME);
            GlobalTypeCache.CacheCLRScripts();
            container.RebuildDictionaries();

            Resources.UnloadAsset(container);
        }

        public const string MAIN_CONT_NAME = "CLR_Ex_Order";

        static MethodInfo SetupMethodInfo = typeof(CLRScript)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .First((info) => info.Name == "Setup" && info.IsVirtual && info.ReturnType == typeof(CLRSetupFlags));

        private static Dictionary<Type, int>          ExecutionOrder = new Dictionary<Type, int>();
        private static Dictionary<Type, CLRSetupData> SetupData = new Dictionary<Type, CLRSetupData>();

        // TODO: Make these disabled in inspector but still viewable
        [SerializeField] CLROrderData[] m_ExecutionOrderData;
        [SerializeField] CLRSetupData[] m_CLRSetupData;

#if UNITY_EDITOR
        // This should really be non blocking or something
        [ContextMenu("Get scripts order")]
        private void OnEnable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            UnityEditor.EditorUtility.SetDirty(this);

            MonoScript[] monoScripts        = MonoImporter.GetAllRuntimeMonoScripts();
            Type[] monoScriptTypes          = new Type[monoScripts.Length];
            int[] monoScriptExecutionOrders = new int[monoScripts.Length];

            int monoScriptsCapacity = 0;
            for (int i = 0; i < monoScripts.Length; i++)
            {
                var monoScript = monoScripts[i];
                Type type = monoScript.GetClass();
                if (!monoScript || type is null || !ComponentHelpers.IsInherited(type, typeof(CLRScript)))
                    continue;

                int order = UnityEditor.MonoImporter.GetExecutionOrder(monoScript);

                monoScriptTypes[monoScriptsCapacity] = type;
                monoScriptExecutionOrders[monoScriptsCapacity] = order;
                ++monoScriptsCapacity;
            }

            // ConcurrentBag has too much bloat for such simple task really
            CLRSetupData[] setupDataBag = new CLRSetupData[monoScriptsCapacity];
            CLROrderData[] orderDataBag = new CLROrderData[monoScriptsCapacity];
            int setupDataCount = 0;
            int orderDataCount = 0;

            // On avarage you'd have a lot of these so move as much as possible to parallel loop
            Parallel.For(0, monoScriptsCapacity, (i) =>
            {
                Type type = monoScriptTypes[i];
                if (type is null)
                    return;

                int order = monoScriptExecutionOrders[i];

                DefaultExecutionOrder executionAttr = type.GetCustomAttribute<DefaultExecutionOrder>();
                if (order == 0 && executionAttr != null)
                {
                    order = executionAttr.order;
                }

                var orderDataBagIndex = Interlocked.Increment(ref orderDataCount) - 1;

                orderDataBag[orderDataBagIndex] = new CLROrderData()
                {
                    typeName = type.FullName,
                    executionIndex = order
                };

                if (!ComponentHelpers.HasOverride(SetupMethodInfo, type))
                {
                    var settings = CLRScript.DefaultSetupFunction(type);
                    var executionType = CLRScript.GetExecutionOrderType(type);

                    var setupData = new CLRSetupData()
                    {
                        flags = settings,
                        typeName = type.FullName,
                        baseExecutionTypeName = executionType == type ? string.Empty : executionType.FullName,
                    };

                    var setupDataBagIndex = Interlocked.Increment(ref setupDataCount) - 1;
                    setupDataBag[setupDataBagIndex] = setupData;
                }
            });

            m_CLRSetupData       = setupDataBag;
            m_ExecutionOrderData = orderDataBag;

            RebuildDictionaries();

            Array.Sort(m_ExecutionOrderData, 0, orderDataCount,
                       CLROrderDataComparer.instance);

            bool rebuildNeeded = true;

            //FIXME: This is probably stupid and needs to be redone
            int sanityCheck = 0;
            while (rebuildNeeded)
            {
                rebuildNeeded = false;

                sanityCheck++;
                if (sanityCheck > 10_000)
                {
                    throw new System.Exception("You have created infinite loop by using Run.After/Before attributes");
                }

                for (int i = 0; i < orderDataCount; i++)
                {
                    ref CLROrderData data = ref m_ExecutionOrderData[i];
                    Type type = GlobalTypeCache.FindType(data.typeName);
                    var beforeAttr = type.GetCustomAttribute<Run.BeforeAttribute>(true);
                    var afterAttr  = type.GetCustomAttribute<Run.AfterAttribute>(true);
                    int myOrder    = ExecutionOrder[type];

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
                    Array.Sort(m_ExecutionOrderData, 0, orderDataCount, 
                               CLROrderDataComparer.instance);
                    RebuildDictionaries();
                }
            }

            // Don't waste space serializing scripts that do not have any interesting execution index to begin with
            ClearZeroOrderScripts(ref orderDataCount, ref m_ExecutionOrderData);

            // Resize in the very end to avoid GCAllocs
            Array.Resize(ref m_ExecutionOrderData, orderDataCount);
            Array.Resize(ref m_CLRSetupData,       setupDataCount);
        }

        private static void ClearZeroOrderScripts(ref int orderDataLength, ref CLROrderData[] executionOrderData)
        {
            // Execution order data is expected to be sorted so in the middle you'll get pack
            // of types with 0 execution order index, we can just remove all of them at once
            int executionIndexZeroStart = -1, executionIndexZeroCount = -1;

            for (int i = 0; i < orderDataLength; i++)
            {
                ref CLROrderData data = ref executionOrderData[i];
                if (data.executionIndex == 0)
                {
                    if (executionIndexZeroStart < 0)
                    {
                        executionIndexZeroStart = i;
                        executionIndexZeroCount++;
                    }

                    executionIndexZeroCount++;
                }
                else if (executionIndexZeroCount >= 0)
                    break;
            }

            if (executionIndexZeroCount <= 0)
                return;

            int pastZeroIndex = (executionIndexZeroStart + executionIndexZeroCount);
            
            int pastZeroLength = orderDataLength - pastZeroIndex;
            if(pastZeroLength > 0)
                Array.Copy(executionOrderData, pastZeroIndex,
                           executionOrderData, executionIndexZeroStart, pastZeroLength);

            orderDataLength -= executionIndexZeroCount;
        }
#endif

        private void RebuildDictionaries()
        {
            ExecutionOrder.Clear();
            SetupData.Clear();

            for (int i = 0; i < m_ExecutionOrderData.Length; i++)
            {
                ref CLROrderData script = ref m_ExecutionOrderData[i];
                var foundType = GlobalTypeCache.FindType(script.typeName);
                if (foundType is null)
                {
                    continue;
                }

                ExecutionOrder[foundType] = script.executionIndex;
            }

            for (int i = 0; i < m_CLRSetupData.Length; i++)
            {
                ref CLRSetupData scriptData = ref m_CLRSetupData[i];
                var foundType = GlobalTypeCache.FindType(scriptData.typeName);
                if (foundType is null)
                {
                    continue;
                }

                SetupData[foundType] = scriptData;
            }
        }

        public static int GetExecutionOrder(Type type)
        {
            if (ExecutionOrder.TryGetValue(type, out int val))
                return val;
            else
                return 0;
        }

        public static bool TryGetDefaultSetup(Type type, out CLRSetupFlags settings)
        {
            if (SetupData.TryGetValue(type, out var data))
            {
                settings = data.flags;
                return true;
            }

            settings = CLRScript.DefaultSetupFunction(type);
            return false;
        }

        public static Type GetBaseExecutionType(Type clrScriptType)
        {
            if (SetupData.TryGetValue(clrScriptType, out var data))
            {
                if (string.IsNullOrEmpty(data.baseExecutionTypeName))
                    return clrScriptType;

                var baseType = GlobalTypeCache.FindType(data.baseExecutionTypeName);
                if (baseType != null)
                    return baseType;
            }

            return clrScriptType;
        }
    }
}
