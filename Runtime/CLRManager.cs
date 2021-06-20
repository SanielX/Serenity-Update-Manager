using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.Profiling;

using Debug = UnityEngine.Debug;

namespace HostGame
{
    [Serializable]
    class Bucket
    {
        public Type ScriptType;
        public int ExecutionIndex;
        public List<CLRScript> ScriptList;
    }

    class CLRManager : MonoBehaviour
    {
#if UNITY_EDITOR 
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            if (instance != null)
            {
                Destroy(instance);
            }

            instance = null;
        }
#endif 

        readonly struct InitCommand
        {
            public InitCommand(CLRScript script, bool addOrRemove)
            {
                this.script = script;
                this.addOrRemove = addOrRemove;
            }

            public readonly CLRScript script;
            public readonly bool addOrRemove;
        }

        // This is huge hack, but otherwise we would get GC on every update
        private enum CallbackType
        {
            ManagedStart = 0, Update = 1,
            FixedUpdate = 2, LateUpdate = 3,
            PreUpdate = 4, EarlyUpdate = 5
        };

#if ENABLE_PROFILER
        private Dictionary<Type, string[]> profileStrings = new Dictionary<Type, string[]>();

        private void AddTypeToProfileStrings(Type t, out string[] result)
        {
            string[] strings = new string[6];
            strings[0] = $"{t.Name}.OnManagedStart()";
            strings[1] = $"{t.Name}.OnUpdate()";
            strings[2] = $"{t.Name}.OnFixedUpdate()";
            strings[3] = $"{t.Name}.OnLateUpdate()";
            strings[4] = $"{t.Name}.OnPreUpdate()";
            strings[5] = $"{t.Name}.OnEarlyUpdate()";

            profileStrings[t] = strings;
            result = strings;
        }

#endif

        private string GetProfileString(Type t, CallbackType callback)
        {
#if ENABLE_PROFILER
            if (!profileStrings.TryGetValue(t, out var strings))
                AddTypeToProfileStrings(t, out strings);

            return strings[(int)callback];
#else
            return null;
#endif
        }

        [Conditional("ENABLE_PROFILER")]
        private void BeginProfileSample(Type t, CallbackType type)
        {
            Profiler.BeginSample(GetProfileString(t, type));
        }

        [Conditional("ENABLE_PROFILER")]
        private void EndProfileSample()
        {
            Profiler.EndSample();
        }

        private static CLRManager instance;

        public static CLRManager Instance
        {
            get
            {
                if (!instance)
                {
                    var go = new GameObject();
                    go.hideFlags = HideFlags.DontSave;
                    go.name = "CLR Manager";
                    instance = go.AddComponent<CLRManager>();
#if UNITY_EDITOR
                    if (Application.isPlaying)
#endif
                        DontDestroyOnLoad(go);
                }

                return instance;
            }
        }

        private Queue<InitCommand> initQueue = new Queue<InitCommand>(64);

        private int zeroPriorityIndexUpd = 0;
        private Dictionary<Type, Bucket> managedTypeToBucket = new Dictionary<Type, Bucket>(8);
        [SerializeField] List<Bucket> managedScripts = new List<Bucket>(8);

        private int zeroPriorityIndexLt = 0;
        private Dictionary<Type, Bucket> managedLateTypeToBucket = new Dictionary<Type, Bucket>(4);
        [SerializeField] List<Bucket> managedLateScripts = new List<Bucket>(4);

        private int zeroPriorityIndexFx = 0;
        private Dictionary<Type, Bucket> managedFixedTypeToBucket = new Dictionary<Type, Bucket>(4);
        [SerializeField] List<Bucket> managedFixedScripts = new List<Bucket>(4);

        private int zeroPriorityIndexPr = 0;
        private Dictionary<Type, Bucket> managedPreTypeToBucket = new Dictionary<Type, Bucket>(1);
        [SerializeField] List<Bucket> managedPreScripts = new List<Bucket>(1);

        private int zeroPriorityIndexEr = 0;
        private Dictionary<Type, Bucket> managedEarlyTypeToBucket = new Dictionary<Type, Bucket>(1);
        [SerializeField] List<Bucket> managedEarlyScripts = new List<Bucket>(1);

        internal static void Add(CLRScript script)
        {
            Instance.initQueue.Enqueue(new InitCommand(script, true));
        }

        internal static void Remove(CLRScript script)
        {
            Instance.initQueue.Enqueue(new InitCommand(script, false));
        }

        private void EarlyUpdate()
        {
            // Replicating OnEnable/Disable calls in order they came
            while (initQueue.Count > 0)
            {
                var command = initQueue.Dequeue();
                CLRScript clrScript = command.script;

                if (clrScript == null)
                    continue;

                if (command.addOrRemove == false)
                {
                    RemoveScript(clrScript);
                    continue;
                }

                AddScript(clrScript);

                if (clrScript.__started || (clrScript.__settings.UsedCalls & Calls.ManagedStart) == 0)
                    continue;

                BeginProfileSample(clrScript.ScriptType, CallbackType.ManagedStart);

                clrScript.OnManagedStart();
                clrScript.__started = true;

                EndProfileSample();
            }

            UpdateList(managedPreScripts, CallbackType.EarlyUpdate);
        }

        private void PreUpdate()
        {
            UpdateList(managedPreScripts, CallbackType.PreUpdate);
        }

        private void Update()
        {
            UpdateList(managedScripts, CallbackType.Update);
        }

        // Life would be easier with macros huh?
        private void LateUpdate()
        {
            UpdateList(managedLateScripts, CallbackType.LateUpdate);
        }

        private void FixedUpdate()
        {
            UpdateList(managedFixedScripts, CallbackType.FixedUpdate);
        }

        // Funny enough, compiler should inline this method and remove switch!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void UpdateList(List<Bucket> buckets, CallbackType callback)
        {
            for (int i = 0; i < buckets.Count; i++)
            {
                for (int j = 0; j < buckets[i].ScriptList.Count; j++)
                {
                    CLRScript clrScript = buckets[i].ScriptList[j];

                    if (IsScriptActive(clrScript))
                    {
                        BeginProfileSample(clrScript.ScriptType, CallbackType.FixedUpdate);

                        switch (callback)
                        {
                            case CallbackType.Update:
                                clrScript.OnUpdate();
                                break;
                            case CallbackType.FixedUpdate:
                                clrScript.OnFixedUpdate();
                                break;
                            case CallbackType.LateUpdate:
                                clrScript.OnLateUpdate();
                                break;
                            case CallbackType.PreUpdate:
                                clrScript.OnPreUpdate();
                                break;
                            case CallbackType.EarlyUpdate:
                                clrScript.OnEarlyUpdate();
                                break;
                        }

                        EndProfileSample();
                    }
                }
            }
        }

#if UNITY_EDITOR

        private void OnApplicationQuit()
        {
            DestroyImmediate(gameObject);
        }

#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsScriptActive(CLRScript s)
        {
            return s != null && (s.__settings.NoSafetyChecks || (s.enabled && s.gameObject && s.gameObject.activeInHierarchy));
        }

        #region Script List Handeling

        private void AddScript(CLRScript script)
        {
            // Could be multithreaded actually
            Calls usedCalls = script.__settings.UsedCalls;
            if (usedCalls.HasFlag(Calls.Update))
                AddScriptToBucket(script, managedTypeToBucket, managedScripts, ref zeroPriorityIndexUpd);

            if (usedCalls.HasFlag(Calls.LateUpdate))
                AddScriptToBucket(script, managedLateTypeToBucket, managedLateScripts, ref zeroPriorityIndexLt);

            if (usedCalls.HasFlag(Calls.FixedUpdate))
                AddScriptToBucket(script, managedFixedTypeToBucket, managedFixedScripts, ref zeroPriorityIndexFx);

            if (usedCalls.HasFlag(Calls.PreUpdate))
                AddScriptToBucket(script, managedPreTypeToBucket, managedPreScripts, ref zeroPriorityIndexPr);

            if (usedCalls.HasFlag(Calls.EarlyUpdate))
                AddScriptToBucket(script, managedEarlyTypeToBucket, managedEarlyScripts, ref zeroPriorityIndexEr);
        }

        internal void RemoveScript(CLRScript script)
        {
            Calls usedCalls = script.__settings.UsedCalls;
            RemoveScript(script, usedCalls);
        }

        internal void RemoveScript(CLRScript script, Calls callsToRemove)
        {
            if (callsToRemove.HasFlag(Calls.Update))
                RemoveScriptFromList(script, managedTypeToBucket, ref zeroPriorityIndexUpd);

            if (callsToRemove.HasFlag(Calls.LateUpdate))
                RemoveScriptFromList(script, managedLateTypeToBucket, ref zeroPriorityIndexLt);

            if (callsToRemove.HasFlag(Calls.FixedUpdate))
                RemoveScriptFromList(script, managedFixedTypeToBucket, ref zeroPriorityIndexFx);

            if (callsToRemove.HasFlag(Calls.PreUpdate))
                RemoveScriptFromList(script, managedPreTypeToBucket, ref zeroPriorityIndexPr);

            if (callsToRemove.HasFlag(Calls.EarlyUpdate))
                RemoveScriptFromList(script, managedEarlyTypeToBucket, ref zeroPriorityIndexEr);
        }

        private void RemoveScriptFromList(CLRScript script, Dictionary<Type, Bucket> dict, ref int zeroPointIndex)
        {
            if (!dict.TryGetValue(script.ScriptType, out var bucket))
            {
                Debug.LogError($"Script {script} can not be removed because it was never added", script);
                return;
            }

            int index = bucket.ScriptList.IndexOf(script);
            if (index == -1)
                return;

            if (zeroPointIndex > 0 && index <= zeroPointIndex)
                zeroPointIndex--;

            bucket.ScriptList.RemoveAt(index);
        }

        static void AddScriptToBucket(CLRScript script, Dictionary<Type, Bucket> dict, List<Bucket> bucketList, ref int index)
        {
            if (dict.TryGetValue(script.ScriptType, out var bucket))
            {
                bucket.ScriptList.Add(script);
                return;
            }

            bucket = new Bucket()
            {
                ScriptType = script.ScriptType,
                ExecutionIndex = script.ExecutionIndex,
                ScriptList = new List<CLRScript>(16)
            };
            bucket.ScriptList.Add(script);

            dict[script.ScriptType] = bucket;
            AddBucketToList(bucket, bucketList, ref index);
        }

        private static void AddBucketToList(Bucket script, List<Bucket> scriptsList, ref int startIndex)
        {
            if (scriptsList.Count == 0)
            {
                scriptsList.Add(script);

                if (script.ExecutionIndex < 0)
                    startIndex++;
                return;
            }

            int seekIndex = script.ExecutionIndex;
            System.Type scriptType = script.ScriptType;

            int i = Mathf.Max(0, startIndex - 1);
            int sanityCheck = 0;
            int maxIterations = scriptsList.Count * 2;
            while (sanityCheck < maxIterations)
            {
                sanityCheck++;
                if (sanityCheck > maxIterations)
                {
                    Debug.LogError("Something is wrong");
                    break;
                }

                var currentScript = scriptsList[i];

                if (seekIndex >= currentScript.ExecutionIndex)
                {
                    var nextScript = i + 1 < scriptsList.Count ? scriptsList[i + 1] : null;

                    if (nextScript == null)
                    {
                        scriptsList.Add(script);
                        ++i;
                        break;
                    }
                    else if (nextScript.ScriptType != scriptType)
                    {
                        if (nextScript.ExecutionIndex <= seekIndex)
                        {
                            ++i;
                            continue;
                        }
                        else
                        {
                            scriptsList.Insert(++i, script);
                            break;
                        }
                    }
                    else
                    {
                        scriptsList.Insert(++i, script);
                        break;
                    }
                }

                if (seekIndex < currentScript.ExecutionIndex)
                {
                    var prevScript = i > 0 ? scriptsList[i - 1] : null;

                    if (prevScript == null)
                    {
                        scriptsList.Insert(0, script);
                        break;
                    }
                    else if (prevScript.ScriptType != scriptType)
                    {
                        if (prevScript.ExecutionIndex < seekIndex)
                        {
                            scriptsList.Insert(--i, script);
                            break;
                        }
                        else
                        {
                            --i;
                            continue;
                        }
                    }
                    else
                    {
                        scriptsList.Insert(--i, script);
                        break;
                    }
                }
            }

            if (i < startIndex)
                startIndex++;
        }

        #endregion Script List Handeling

        #region Player Loop Modification

        private struct CLRPreUpdate { }
        private struct CLREarlyUpdate { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterEarlyUpdate()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();

            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                ref PlayerLoopSystem rootSubsystems = ref loop.subSystemList[i];

                if (rootSubsystems.type == typeof(EarlyUpdate))
                {
                    ref var subSystems = ref loop.subSystemList[i].subSystemList;
#if UNITY_EDITOR  // Protection against DomainReload off
                    if (FindSubsystem(subSystems, typeof(CLREarlyUpdate)))
                        continue;
#endif 

                    Array.Resize(ref subSystems, rootSubsystems.subSystemList.Length + 1);
                    int index = subSystems.Length - 1;

                    // And injecting system at the end of default loop
                    subSystems[index] = new PlayerLoopSystem()
                    {
                        type = typeof(CLREarlyUpdate),
                        updateDelegate = EarlyUpdateCallback
                    };
                }

                if (rootSubsystems.type == typeof(PreUpdate))
                {
                    ref var subSystems = ref loop.subSystemList[i].subSystemList;
#if UNITY_EDITOR
                    if (FindSubsystem(subSystems, typeof(CLRPreUpdate)))
                        continue;
#endif 
                    Array.Resize(ref subSystems, rootSubsystems.subSystemList.Length + 1);
                    int index = subSystems.Length - 1;

                    subSystems[index] = new PlayerLoopSystem()
                    {
                        type = typeof(CLRPreUpdate),
                        updateDelegate = PreUpdateCallback
                    };
                }

            }

            PlayerLoop.SetPlayerLoop(loop);

            static bool FindSubsystem(PlayerLoopSystem[] subSystems, Type systemType)
            {
                foreach (var subs in subSystems)
                {
                    if (subs.type == systemType)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private static void PreUpdateCallback()
        {
            Instance.PreUpdate();
        }

        private static void EarlyUpdateCallback()
        {
            Instance.EarlyUpdate();
        }

        #endregion Player Loop Modification
    }
}