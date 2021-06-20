using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace HostGame
{
 //   [CreateAssetMenu(menuName = "HG/Execution Order Cntr")]
    class CLRExecutionOrderContainer : ScriptableObject
    {
        [System.Serializable]
        public class MonoOrderData
        {
            public string typeName;
            public int executionIndex;
        }

        public const string MAIN_CONT_NAME = "CLR_Ex_Order";
        private static CLRExecutionOrderContainer container;
        public static CLRExecutionOrderContainer Container
        {
            get
            {
                if (!container)
                {
                    container = Resources.Load<CLRExecutionOrderContainer>(MAIN_CONT_NAME);
                    container.OnEnable();
                }

                return container;
            }
        }

        private Dictionary<string, Type> cachedTypes = new Dictionary<string, Type>();
        private Dictionary<Type, int> ExOrder = new Dictionary<Type, int>();
        public List<MonoOrderData> data;

        [ContextMenu("Get scripts order")]
        private void OnEnable()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            UnityEditor.EditorUtility.SetDirty(this);

            data = new List<MonoOrderData>();
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

                data.Add(new MonoOrderData()
                {
                    typeName = type.FullName,
                    executionIndex = order
                });
            }
#endif

            RebuildDictionary();
            data.Sort((x, y) => x.executionIndex.CompareTo(y.executionIndex));

#if UNITY_EDITOR
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

                foreach (var data in this.data)
                {
                    Type type = FindType(data.typeName);
                    var beforeAttr = type.GetCustomAttribute<Run.BeforeAttribute>(true);
                    var afterAttr = type.GetCustomAttribute<Run.AfterAttribute>(true);
                    int myOrder = ExOrder[type];

                    if (beforeAttr != null)
                    {
                        int targetOrder = ExOrder[beforeAttr.targetType];

                        if (targetOrder <= myOrder)
                        {
                            data.executionIndex = targetOrder - 1;
                            rebuildNeeded = true;
                        }
                    }

                    if (afterAttr != null)
                    {
                        int targetOrder = ExOrder[afterAttr.targetType];

                        if (myOrder <= targetOrder)
                        {
                            data.executionIndex = targetOrder + 1;
                            rebuildNeeded = true;
                        }
                    }
                }

                if (rebuildNeeded)
                {
                    data.Sort((x, y) => x.executionIndex.CompareTo(y.executionIndex));
                    RebuildDictionary();
                }
            }

            for (int i = 0; i < data.Count; i++)
            {
                MonoOrderData data = this.data[i];
                if(data.executionIndex == 0)
                {
                    this.data.RemoveAt(i);
                    i--;
                }
            }
#endif
        }

        private void RebuildDictionary()
        {
            foreach (var script in data)
            {
                ExOrder[FindType(script.typeName)] = script.executionIndex;
            }
        }

        public int GetExecutionOrder(Type type)
        {
            if (ExOrder.TryGetValue(type, out int val))
                return val;
            else
                return 0;
        }

        static readonly Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        Type FindType(string name)
        {
            Type result;
            if (cachedTypes.TryGetValue(name, out result))
                return result;

            foreach (var assembly in assemblies)
            {
                result = assembly.GetType(name, false);
                if (result != null)
                {
                    cachedTypes[name] = result;
                    break;
                }
            }
            return result;
        }
    }
}
