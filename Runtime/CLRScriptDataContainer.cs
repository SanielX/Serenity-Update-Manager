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

        struct ScriptOrderData
        {
            public Type clrScriptType;
            public int  orderIndex;
            public Type[] runBefore;
            public Type[] runAfter;

            public override int GetHashCode() => clrScriptType.GetHashCode();
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
        private bool fresh; // This needs to run only once during livespan of an object

        static Type[] emptyTypeArray = new Type[0];
        // This should really be non blocking or something
        [ContextMenu("Get scripts order")]
        private void OnEnable()
        {
            if (fresh || EditorApplication.isPlayingOrWillChangePlaymode)
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

                // Somehow monoscript and type may be null. Unity moment
                if (!monoScript || type is null || type.IsAbstract ||
                    !type.IsSubclassOf(typeof(CLRScript)))
                    continue;

                int order = UnityEditor.MonoImporter.GetExecutionOrder(monoScript);

                monoScriptTypes[monoScriptsCapacity] = type;
                monoScriptExecutionOrders[monoScriptsCapacity] = order;
                ++monoScriptsCapacity;
            }

            // ConcurrentBag has too much bloat for such simple task really
            CLRSetupData[]    setupDataBag = new CLRSetupData[monoScriptsCapacity];
            ScriptOrderData[] orderDataBag = new ScriptOrderData[monoScriptsCapacity];
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

                var typesBefore = type.GetCustomAttributes<Run.BeforeAttribute>(inherit: true);
                var typesAfter  = type.GetCustomAttributes<Run.AfterAttribute> (inherit: true);

                orderDataBag[orderDataBagIndex] = new ScriptOrderData()
                {
                    clrScriptType = type,
                    orderIndex = order,

                    runBefore = typesBefore.Count() > 0 ?
                                typesBefore.Select((attr) => attr.targetType).ToArray() : emptyTypeArray,

                    runAfter = typesAfter.Count() > 0 ?
                               typesAfter.Select((attr) => attr.targetType).ToArray() : emptyTypeArray,
                };


                if (!ComponentHelpers.HasOverride(SetupMethodInfo, type))
                {
                    var settings      = CLRScript.DefaultSetupFunction(type);
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

            HashSet<Type> resolvedTypesSet = new HashSet<Type>(orderDataCount);
            for (int i = 0; i < orderDataCount; i++)
            {
                resolveTypeOrder(i);
            }

            int resolveTypeOrder(int index)
            {
                ref var data = ref orderDataBag[index];
                if (resolvedTypesSet.Contains(data.clrScriptType))
                    return data.orderIndex;

                // Mark as resolved before hand to avoid stack overflow
                resolvedTypesSet.Add(data.clrScriptType);
                if (data.runBefore.Length == 0 && data.runAfter.Length == 0) 
                    return data.orderIndex;

                // TODO: Make proper resolve for when class has
                // both Run.Before and Run.After attributes
                int resolvedOrder = 0;

                for (int i = 0; i < data.runBefore.Length; i++)
                {
                    int candidateIndex = indexOfOrder(data.runBefore[i]);
                    int candidate = resolveTypeOrder(candidateIndex) - 1;
                    if (resolvedOrder == 0 || candidate < resolvedOrder)
                        resolvedOrder = candidate;
                }

                for (int i = 0; i < data.runAfter.Length; i++)
                {
                    int candidateIndex = indexOfOrder(data.runAfter[i]);
                    int candidate = resolveTypeOrder(candidateIndex) + 1;
                    if (resolvedOrder == 0 || candidate > resolvedOrder)
                        resolvedOrder = candidate;
                }

                return (data.orderIndex = resolvedOrder);
            }

            int indexOfOrder(Type type)
            {
                for (int i = 0; i < orderDataCount; i++)
                {
                    if (type.IsAssignableFrom(orderDataBag[i].clrScriptType))
                        return i;
                }

                return -1;
            }

            m_ExecutionOrderData = new CLROrderData[orderDataCount];
            for (int i = 0; i < orderDataCount; i++)
            {
                ref var data = ref orderDataBag[i];
                m_ExecutionOrderData[i] = new CLROrderData()
                {
                    typeName = data.clrScriptType.FullName,
                    executionIndex = data.orderIndex
                };
            }
            Array.Sort(m_ExecutionOrderData, 0, orderDataCount,
                      CLROrderDataComparer.instance);

            // Don't waste space serializing scripts that do not have any interesting execution index to begin with
            ClearZeroOrderScripts(ref orderDataCount, ref m_ExecutionOrderData);

            // Resize in the very end to avoid GCAllocs
            Array.Resize(ref m_ExecutionOrderData, orderDataCount);
            Array.Resize(ref m_CLRSetupData, setupDataCount);

            Array.Copy(setupDataBag, m_CLRSetupData, setupDataCount); 
            AssetDatabase.SaveAssetIfDirty(this);
            fresh = true;
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
            if (pastZeroLength > 0)
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
