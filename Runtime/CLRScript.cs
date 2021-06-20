using System;
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
        // TODO: Unique identifier for each CLRScript
        // [SerializeField]
        // [HideInInspector]
        // private byte[] guid;

        [HideInInspector]
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
            ExecutionIndex = CLRExecutionOrderContainer.Container.GetExecutionOrder(ScriptType);
            __settings = Setup(out bool cacheComponents);
            if (cacheComponents)
                ComponentManager.TryRegisterComponents(gameObject);

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

            if (HasUpdates())
                CLRManager.Remove(this);
        }

        public virtual CLRSettings Setup(out bool cacheComponents)
        {
            Calls finalCalls = 0;

            Type myType = GetType();
            Add(nameof(OnManagedStart), Calls.ManagedStart);
            Add(nameof(OnPreUpdate),    Calls.PreUpdate);
            Add(nameof(OnUpdate),       Calls.Update);
            Add(nameof(OnFixedUpdate),  Calls.FixedUpdate);
            Add(nameof(OnLateUpdate),   Calls.LateUpdate);
            Add(nameof(OnEarlyUpdate),  Calls.EarlyUpdate);

#if CLR_NO_CACHE
            cacheComponents = false;
#elif  CLR_ALWAYS_CACHE
            cacheComponents = true;
#else
            cacheComponents = myType.GetCustomAttribute<DontCacheComponentsAttribute>() is null;
#endif

#if UNITY_EDITOR
            if (finalCalls.HasFlag(Calls.ManagedStart) &&
              ((finalCalls & Calls.Updates) == 0))
            {
                Debug.LogError("Method overrides OnManagedStart but doesn't have any Update method. ManagedStart won't be called", this);
            }
#endif

            return new CLRSettings(finalCalls);

            void Add(string method, Calls call)
            {
                if (ComponentHelpers.HasOverride(typeof(CLRScript).GetMethod(method), myType))
                    finalCalls |= call;
            }
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
    }
}