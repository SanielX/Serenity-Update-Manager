using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HostGame
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
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

        [@HideInInspector]
        internal bool __started = false;
        internal CLRSettings __settings;

        public Transform iTransform => transform;
        public GameObject iGameObject => gameObject;

        public Type ScriptType { get; private set; }
        [HideInInspector]
        public int ExecutionIndex { get; private set; }

        private bool HasUpdates()
        {
            return (__settings.UsedCalls & Calls.Updates) != 0;
        }

        private void Awake()
        {
            ScriptType = GetType();
            ExecutionIndex = CLRScriptDataContainer.GetExecutionOrder(ScriptType);

            // Caching setup calls for every type helps avoid reflection
            bool cacheComponents;
            if (!CLRScriptDataContainer.TryGetDefaultSetup(ScriptType, out __settings, out cacheComponents)) 
            {
                __settings = Setup(out cacheComponents); // If no cached result, then do setup ourselves
            }

            if (cacheComponents)
                ComponentManager.TryRegisterComponents(gameObject);

            ComponentManager.AddInstance(this);
            ComponentManager.AddInstance(gameObject, throwIfExists: false);

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

        protected virtual CLRSettings Setup(out bool cacheComponents)
        {
            return DefaultSetupFunction(ScriptType, out cacheComponents);
        }

        public virtual void OnAwake() { }

        /// <summary>
        /// It's safe to use void Start, so OnStart is for consistency only
        /// </summary>
        public virtual void OnStart() { }

        /// <summary>
        /// Managed Start is called one frame after usual <see cref="OnStart"/> but before first <see cref="OnUpdate"/>.
        /// IMPORTANT: Method is only called if object has any of Update calls
        /// </summary>
        public virtual void OnManagedStart() { }
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

        internal static CLRSettings DefaultSetupFunction(Type scriptType, out bool cache)
        {
            Calls finalCalls = FindCallsByOverride(scriptType);
            cache = scriptType.GetCustomAttribute<DontCacheComponentsAttribute>() is null;

#if UNITY_EDITOR
            if (finalCalls.HasFlag(Calls.ManagedStart) &&
              ((finalCalls & Calls.Updates) == 0))
            {
                Debug.LogError("Method overrides OnManagedStart but doesn't have any Update method. ManagedStart won't be called");
            }
#endif

            return new CLRSettings(finalCalls);
        }

        internal static Calls FindCallsByOverride(Type type)
        {
            Calls finalCalls = 0;

            Add(nameof(OnManagedStart), Calls.ManagedStart);
            Add(nameof(OnPreUpdate), Calls.PreUpdate);
            Add(nameof(OnUpdate), Calls.Update);
            Add(nameof(OnFixedUpdate), Calls.FixedUpdate);
            Add(nameof(OnLateUpdate), Calls.LateUpdate);
            Add(nameof(OnEarlyUpdate), Calls.EarlyUpdate);

            void Add(string method, Calls call)
            {
                if (ComponentHelpers.HasOverride(typeof(CLRScript).GetMethod(method), type))
                    finalCalls |= call;
            }

            return finalCalls;
        }
    }
}