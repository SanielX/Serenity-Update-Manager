using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HostGame
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class DontCacheComponentsAttribute : Attribute { }

    /// <summary>
    /// CLR scripts are run on special manager, override methods instead of adding magic unity methods
    /// </summary>
    public abstract class CLRScript : MonoBehaviour, IGOComponent
    {
        // TODO: Persistent unique identifier for each CLRScript
        // [SerializeField]
        // [HideInInspector]
        // private byte[] guid;

        internal CLRSetupFlags __setupFlags;

        public Transform iTransform => transform;
        public GameObject iGameObject => gameObject;

        public Type ScriptType { get; private set; }
        [HideInInspector]
        public int ExecutionIndex { get; private set; }

        private void Awake()
        {
            ScriptType = GetType();
            ExecutionIndex = CLRScriptDataContainer.GetExecutionOrder(ScriptType);

            // Caching setup calls for every type helps avoid reflection
            if (!CLRScriptDataContainer.TryGetDefaultSetup(ScriptType, out __setupFlags)) 
            {
                __setupFlags = Setup(); // If no cached result, then do setup ourselves
            }

            if (!__setupFlags.HasFlag(CLRSetupFlags.DontCacheComponents))
                ComponentManager.TryRegisterComponents(gameObject);

            ComponentManager.AddObjectInstance(this);
            ComponentManager.AddObjectInstance(gameObject, throwIfExists: false);

            OnAwake();
        }

        private void Start()
        {
            OnStart();
        }

        private void OnEnable()
        {
            if (HasUpdates())
                CLRManager.Add(this);

            OnEnabled();
        }

        private void OnDisable()
        {
            if (HasUpdates())
                CLRManager.Remove(this);

            OnDisabled();
        }

        private void OnDestroy()
        {
            OnDestroyed();

            ComponentManager.UnregisterComponents(gameObject);
            ComponentManager.RemoveInstance(this);
            ComponentManager.RemoveInstance(gameObject);
        }

        protected virtual CLRSetupFlags Setup()
        {
            return DefaultSetupFunction(ScriptType);
        }

        private bool HasUpdates()
        {
            return (__setupFlags & CLRSetupFlags.Updates) != 0;
        }

        public virtual void OnAwake() { }

        /// <summary>
        /// It's safe to use void Start, so OnStart is for consistency only
        /// </summary>
        public virtual void OnStart() { }

        public virtual void OnEnabled() { }
        public virtual void OnDisabled() { }

        public virtual void OnPreUpdate() { }
        public virtual void OnUpdate() { }
        public virtual void OnLateUpdate() { }
        /// <summary>
        /// Called before <see cref="OnFixedUpdate"/> every frame
        /// </summary>
        public virtual void OnEarlyUpdate() { }

        /// <summary>
        /// Called before <see cref="OnUpdate"/> but not every frame
        /// </summary>
        public virtual void OnFixedUpdate() { }

        public virtual void OnDestroyed() { }

        internal static CLRSetupFlags DefaultSetupFunction(Type scriptType)
        {
            CLRSetupFlags finalFlags = FindCallsByOverride(scriptType);
            if (scriptType.GetCustomAttribute<DontCacheComponentsAttribute>() != null ||
                scriptType.Assembly.GetCustomAttribute<DontCacheComponentsAttribute>() != null)
                finalFlags |= CLRSetupFlags.DontCacheComponents;

            return finalFlags;
        }

        internal static CLRSetupFlags FindCallsByOverride(Type type)
        {
            CLRSetupFlags finalCalls = 0;

            Add(nameof(OnPreUpdate),   CLRSetupFlags.PreUpdate);
            Add(nameof(OnUpdate),      CLRSetupFlags.Update);
            Add(nameof(OnFixedUpdate), CLRSetupFlags.FixedUpdate);
            Add(nameof(OnLateUpdate),  CLRSetupFlags.LateUpdate);
            Add(nameof(OnEarlyUpdate), CLRSetupFlags.EarlyUpdate);

            void Add(string method, CLRSetupFlags call)
            {
                if (ComponentHelpers.HasOverride(typeof(CLRScript).GetMethod(method), type))
                    finalCalls |= call;
            }

            return finalCalls;
        }
    }
}