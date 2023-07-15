using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Runtime.CompilerServices;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Serenity
{
    internal class SerenityScriptDataContainer : ScriptableObject
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

            public UpdateSetupFlags flags;
        }

        struct ScriptOrderData
        {
            public Type clrScriptType;
            public int  orderIndex;
            public Type[] runBefore;
            public Type[] runAfter;

            public override int GetHashCode() => clrScriptType.GetHashCode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitCLRData()
        {
            var container = Resources.Load<SerenityScriptDataContainer>("_Local/" + MAIN_CONT_NAME);
            GlobalTypeCache.CacheCLRScripts();
            container.RebuildDictionaries();

            Resources.UnloadAsset(container);
        }

        public const string MAIN_CONT_NAME = "Serenity_UpdateManager_Data";

        private static Dictionary<int, int>           ExecutionOrder = new(); // Stored as type hashcode to execution index
                                                                              // All CLRScript derived classess are forever cached, therefore their System.Object hashcode will never
                                                                              // be invalid (since it is based on instance creation order)
        private static Dictionary<int, CLRSetupData>  SetupData      = new();

        // TODO: Make these disabled in inspector but still viewable
        [SerializeField] CLROrderData[] m_ExecutionOrderData = System.Array.Empty<CLROrderData>();
        [SerializeField] CLRSetupData[] m_CLRSetupData       = System.Array.Empty<CLRSetupData>();

#if UNITY_EDITOR
        private static bool initializedCache;
        private static readonly MethodInfo SetupMethodInfo_Editor = typeof(SRScript)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .First((info) => info.Name == "Setup" && info.IsVirtual && info.ReturnType == typeof(UpdateSetupFlags));

        class CLROrderDataComparer : IComparer<CLROrderData>
        {
            public static CLROrderDataComparer instance = new CLROrderDataComparer();

            public int Compare(CLROrderData x, CLROrderData y)
            {
                return x.executionIndex.CompareTo(y.executionIndex);
            }
        }
         
        [UnityEditor.InitializeOnLoadMethod]
        static void InitCLRDataObject()
        {
            var container = Resources.Load<SerenityScriptDataContainer>("_Local/" + MAIN_CONT_NAME);
            if(!container)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                
                if (!AssetDatabase.IsValidFolder("Assets/Resources/_Local"))
                    AssetDatabase.CreateFolder("Assets/Resources", "_Local");

                container = ScriptableObject.CreateInstance<SerenityScriptDataContainer>();
                container.name = MAIN_CONT_NAME;

                AssetDatabase.CreateAsset(container, "Assets/Resources/_Local/" + MAIN_CONT_NAME + ".asset");
            }

            if(container)
                container.CacheCLRData();
        }

        static Type[] emptyTypeArray = new Type[0];
        // This should really be non blocking or something
        [ContextMenu("Get scripts order")]
        private void CacheCLRData()
        {
            if (initializedCache) return;

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
                    !type.IsSubclassOf(typeof(SRScript)))
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


                if (!ComponentHelpers.HasOverride(SetupMethodInfo_Editor, type))
                {
                    var settings      = SRScript.DefaultSetupFunction(type);

                    var setupData = new CLRSetupData()
                    {
                        flags = settings,
                        typeName = type.FullName,
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

            var oldOrderData = (CLROrderData[])m_ExecutionOrderData.Clone();
            var oldSetupData = (CLRSetupData[])m_CLRSetupData.Clone();

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
            Array.Resize(ref m_CLRSetupData,       setupDataCount);

            Array.Copy(setupDataBag, m_CLRSetupData, setupDataCount);

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this); 
            initializedCache = true;
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

                ExecutionOrder.Add(RuntimeHelpers.GetHashCode(foundType), script.executionIndex);
            }

            for (int i = 0; i < m_CLRSetupData.Length; i++)
            {
                CLRSetupData scriptData = m_CLRSetupData[i];
                var foundType = GlobalTypeCache.FindType(scriptData.typeName);
                if (foundType is null)
                {
                    continue;
                }

                SetupData.Add(RuntimeHelpers.GetHashCode(foundType), scriptData);
            }
        }

        public static int GetExecutionOrder(Type type)
        {
            if (ExecutionOrder.TryGetValue(RuntimeHelpers.GetHashCode(type), out int val))
                return val;
            else
                return 0;
        }

        public static bool TryGetDefaultSetup(Type type, out UpdateSetupFlags settings)
        {
            if (SetupData.TryGetValue(RuntimeHelpers.GetHashCode(type), out var data))
            {
                settings = data.flags;
                return true;
            }

            settings = default;
            return false;
        }
    }
}
