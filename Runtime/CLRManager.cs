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
    internal class CLRManager : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            var go = new GameObject();
            go.hideFlags = HideFlags.DontSave;
            go.name  = "CLR Manager";
            instance = go.AddComponent<CLRManager>();

            DontDestroyOnLoad(go);
        }

#if UNITY_EDITOR

        // This is for when AppDomainReload on playmode is disabled, on playmode CLRManager is intended to 
        // live through all of application lifetime so no sense in destroying it
        [UnityEditor.InitializeOnLoadMethod]
        static void InitDestroyInstance()
        {
            UnityEditor.EditorApplication.playModeStateChanged += (state) =>
            {
                if(state == UnityEditor.PlayModeStateChange.EnteredEditMode && Instance && Instance.gameObject)
                {
                    DestroyImmediate(Instance.gameObject);
                    instance = null;
                }
            };
        }
#endif

        private enum InitCommandType : byte 
        {
            Add,
            Remove
        }

        private readonly struct InitCommand
        {
            public InitCommand(CLRScript script, InitCommandType commandType)
            {
                this.script = script;
                this.initCommand = commandType;
            }

            public readonly CLRScript script;
            public readonly InitCommandType initCommand;
        }

        // This is huge hack imo, but otherwise we would get GC on every update
        private enum CallbackType
        {
            Update = 0,
            FixedUpdate, 
            LateUpdate,
            PreUpdate, 
            EarlyUpdate,

            Count
        };

        #region Profiler  

#if ENABLE_PROFILER
        private Dictionary<Type, string[]> profileStrings = new Dictionary<Type, string[]>();

        private void AddTypeToProfileStrings(Type t, out string[] result)
        {
            string[] strings = new string[5];
            strings[0] = $"{t.Name}.OnUpdate()";
            strings[1] = $"{t.Name}.OnFixedUpdate()";
            strings[2] = $"{t.Name}.OnLateUpdate()";
            strings[3] = $"{t.Name}.OnPreUpdate()";
            strings[4] = $"{t.Name}.OnEarlyUpdate()";

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
        private void BeginProfileSample(CLRScript target, CallbackType type)
        {
            Profiler.BeginSample(GetProfileString(target.GetType(), type), target);
        }

        [Conditional("ENABLE_PROFILER")]
        private void EndProfileSample()
        {
            Profiler.EndSample();
        }

        #endregion

        private static CLRManager instance;

        public static CLRManager Instance
        {
            get
            {
                return instance;
            }
        }

        private bool mayNeedGC = false;  // sets to true after any script was removed from any list
                                         // this allows to run deletion of buckets only when it may have sense, instead of just every N frame
        private Queue<InitCommand> initQueue = new Queue<InitCommand>(256);


        private OrderedScriptCollection<CLRScript> managedScriptsCollection = new(256);
        private OrderedScriptCollection<CLRScript> fixedScriptsCollection   = new(256);
        private OrderedScriptCollection<CLRScript> lateScriptsCollection    = new();
        private OrderedScriptCollection<CLRScript> preScriptsCollection     = new();
        private OrderedScriptCollection<CLRScript> earlyScriptsCollection   = new();

        private List<CLRScript> guiScripts = new(16);

        internal static void Add(CLRScript script)
        {
            // TODO: Support for ExecuteAlways code?
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
                return;
#endif

#if UNITY_ASSERTIONS
            if(!script)
            {
                Debug.LogError("CLRScript was destroyed but you are still trying to add to to update list", script);
                return;
            }
#endif

            Instance.initQueue.Enqueue(new InitCommand(script, InitCommandType.Add));
        }

        internal static void Remove(CLRScript script)
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
                return;
#endif 
            Instance.initQueue.Enqueue(new InitCommand(script, InitCommandType.Remove));
        }

        private void EarlyUpdate()
        {
            // Replicating OnEnable/Disable calls in order they came
            if(!CLRManagerSettings.BlockInitializationQueue)
            while (initQueue.Count > 0)
            {
                var command = initQueue.Dequeue();
                CLRScript clrScript = command.script;

                if (clrScript is null) // Use real null check, not unity check
                    continue;

                if (command.initCommand == InitCommandType.Remove) // remove even if destroyed                
                    RemoveScript(clrScript, clrScript.__setupFlags);
                else if (clrScript) // add if not destroyed
                    AddScript(clrScript);
            }

            if (mayNeedGC &&
               (CLRManagerSettings.BucketGCFrequency >= 0 &&
                (Time.frameCount & (CLRManagerSettings.BucketGCFrequency - 1)) == 0))
            {
                managedScriptsCollection.ClearUnusedSlots();
                fixedScriptsCollection  .ClearUnusedSlots();
                lateScriptsCollection   .ClearUnusedSlots();
                preScriptsCollection    .ClearUnusedSlots();
                earlyScriptsCollection  .ClearUnusedSlots();
                mayNeedGC = false;
            }

            UpdateList(earlyScriptsCollection, CallbackType.EarlyUpdate);
        }

        private void PreUpdate()
        {
            UpdateList(preScriptsCollection, CallbackType.PreUpdate);
        }

        private void Update()
        {
            UpdateList(managedScriptsCollection, CallbackType.Update);
        }

        private void LateUpdate()
        {
            UpdateList(lateScriptsCollection, CallbackType.LateUpdate);
        }

        private void FixedUpdate()
        {
            UpdateList(fixedScriptsCollection, CallbackType.FixedUpdate);
        }

        // Funny enough, compiler should inline this method and remove switch!
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateList(OrderedScriptCollection<CLRScript> collection, CallbackType callback)
        {
            var buckets = collection.Data;
            for (int i = 0; i < collection.Count; i++)
            {
                for (int j = 0; j < buckets[i].Count; j++)
                {
                    CLRScript clrScript = buckets[i][j];  // I'd still like to make Span out of list and avoid bound check tho

                    if (IsScriptActive(clrScript))
                    {
#if UNITY_ASSERTIONS
                        try
                        {
#endif
                            BeginProfileSample(clrScript, callback);

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

#if UNITY_ASSERTIONS
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogException(e, clrScript);
                        }
#endif 
                        }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsScriptActive(CLRScript s)
        {
            return (s.__setupFlags & CLRSetupFlags.NoSafetyChecks) != 0 || 
                   (s.isActiveAndEnabled); // This thing will check everything about whether object is destroyed and other properties!
        }

        #region Script List Handling

        private void AddScript(CLRScript script)
        {
            CLRSetupFlags usedFlags = script.__setupFlags;
            if (usedFlags.Has(CLRSetupFlags.Update))
                managedScriptsCollection.Add(script);

            if (usedFlags.Has(CLRSetupFlags.LateUpdate))
                lateScriptsCollection.Add(script);

            if (usedFlags.Has(CLRSetupFlags.FixedUpdate))
                fixedScriptsCollection.Add(script);

            if (usedFlags.Has(CLRSetupFlags.PreUpdate))
                preScriptsCollection.Add(script);

            if (usedFlags.Has(CLRSetupFlags.EarlyUpdate))
                earlyScriptsCollection.Add(script);

            if (usedFlags.Has(CLRSetupFlags.DIMGUIDraw))
                guiScripts.Add(script);
        }

        private void RemoveScript(CLRScript script, CLRSetupFlags usedFlags)
        {
            if (usedFlags.Has(CLRSetupFlags.Update))
                managedScriptsCollection.Remove(script);

            if (usedFlags.Has(CLRSetupFlags.LateUpdate))
                lateScriptsCollection.Remove(script);

            if (usedFlags.Has(CLRSetupFlags.FixedUpdate))
                fixedScriptsCollection.Remove(script);

            if (usedFlags.Has(CLRSetupFlags.PreUpdate))
                preScriptsCollection.Remove(script);

            if (usedFlags.Has(CLRSetupFlags.EarlyUpdate))
                earlyScriptsCollection.Remove(script);

            if (usedFlags.Has(CLRSetupFlags.DIMGUIDraw))
                guiScripts.Remove(script);

            mayNeedGC = true; 
        }

        #endregion Script List Handeling

        #region Player Loop Modification

        private struct CLRPreUpdate { }

        private struct CLREarlyUpdate { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterCustomUpdates()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();

            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                ref PlayerLoopSystem rootSubsystems = ref loop.subSystemList[i];

                if (rootSubsystems.type == typeof(EarlyUpdate))
                {
                    ref var subSystems = ref loop.subSystemList[i].subSystemList;
#if UNITY_EDITOR  // Protection against DomainReload off
                    if (findSubsystem(subSystems, typeof(CLREarlyUpdate)))
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
                    if (findSubsystem(subSystems, typeof(CLRPreUpdate)))
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

            static bool findSubsystem(PlayerLoopSystem[] subSystems, Type systemType)
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
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif
            if (Instance && Instance.enabled)
                Instance.PreUpdate();
        }

        private static void EarlyUpdateCallback()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif

            if(Instance && Instance.enabled)
                Instance.EarlyUpdate();
        }

        internal static void DIMGUIUpdateCallback()
        {
            CLRManager _instance = Instance;
            for (int i = 0; i < _instance.guiScripts.Count; i++)
            {
#if UNITY_ASSERTIONS
                try
                {
                    if (IsScriptActive(_instance.guiScripts[i]))
                        _instance.guiScripts[i].DrawDIMGUI();
                }
                catch(Exception e)
                {
                    Debug.LogException(e);
                }
#else 

                    if (IsScriptActive(_instance.guiScripts[i]))
                        _instance.guiScripts[i].DrawDIMGUI();
#endif
            }
        }

        #endregion Player Loop Modification
    }
}