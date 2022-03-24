using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;

namespace HostGame
{
    /// <summary>
    /// Can be put on class or assembly so that type or all types from certain assembly 
    /// do not try to ever cache components
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class DontCacheComponentsAttribute : Attribute { }

    /// <summary>
    /// Use to set base class by which CLRManager will group scripts.
    /// For example you may have class A with implemented OnUpdate and multiple classess inherited from it
    /// that do not override OnUpdate so you want to still group them toghether
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ExecutionGroupBaseClassAttribute : Attribute { }

    /// <summary>
    /// Allows to manually subscribe CLRScript to updates using <see cref="CLRScript.TryEnableUpdates"/>, 
    /// <see cref="CLRScript.TryDisableUpdates"/> 
    /// or <see cref="CLRScript.EnableUpdates"/> and <see cref="CLRScript.DisableUpdates"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ManuallySubscribeToUpdatesAttribute : Attribute { }

    /// <summary>
    /// CLR scripts are run on special manager, override methods instead of adding magic unity methods
    /// </summary>
    public abstract class CLRScript : MonoBehaviour, IGOComponent
    {
        [HideInInspector] internal CLRSetupFlags __setupFlags;
                          internal Type  __executionOrderType; // used to determine the bucket script goes to
        
        public Transform  iTransform => transform;
        public GameObject iGameObject => gameObject;

        
        private void Awake()
        {
            var type = GetType();
            __executionOrderType = CLRScriptDataContainer.GetBaseExecutionType(type);
            
            // Caching setup calls for every type helps avoid reflection
            if (!CLRScriptDataContainer.TryGetDefaultSetup(type, out __setupFlags)) 
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
            if (ShouldAutoSubscribe())
                CLRManager.Add(this);

            OnEnabled();
        }

        private void OnDisable()
        {
            if (ShouldAutoSubscribe())
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
            return DefaultSetupFunction(GetType());
        }

        private bool ShouldAutoSubscribe()
        {
            return (__setupFlags & CLRSetupFlags.Updates) != 0 &&
                   (__setupFlags & CLRSetupFlags.ManualUpdatesSubscriptionControl) == 0;
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

        protected bool TryEnableUpdates()
        {
            Assert.IsTrue((__setupFlags & CLRSetupFlags.ManualUpdatesSubscriptionControl) != 0, "To Enable/Disable updates script must have ManualUpdateSubscriptionControl enabled");

            if (!CLRManager.IsScriptSubscribed(this))
            {
                CLRManager.Add(this);
                return true;
            }

            return false;
        }

        protected bool TryDisableUpdates()
        {
            Assert.IsTrue((__setupFlags & CLRSetupFlags.ManualUpdatesSubscriptionControl) != 0, "To Enable/Disable updates script must have ManualUpdateSubscriptionControl enabled");

            if (CLRManager.IsScriptSubscribed(this))
            {
                CLRManager.Remove(this);
                return true;
            }

            return false;
        }

        protected void EnableUpdates()
        {
            Assert.IsTrue((__setupFlags & CLRSetupFlags.ManualUpdatesSubscriptionControl) != 0, 
                          "To Enable/Disable updates script must have ManualUpdateSubscriptionControl enabled");
            Assert.IsFalse(CLRManager.IsScriptSubscribed(this), "Script can not be subscribed twice");
            
            CLRManager.Add(this);
        }

        protected void DisableUpdates()
        {
            Assert.IsTrue((__setupFlags & CLRSetupFlags.ManualUpdatesSubscriptionControl) != 0, "To Enable/Disable updates script must have ManualUpdateSubscriptionControl enabled");
            Assert.IsTrue(CLRManager.IsScriptSubscribed(this), "Script can not be unsubsibed if it was never added");
            CLRManager.Remove(this);
        }

        internal static CLRSetupFlags DefaultSetupFunction(Type scriptType)
        {
            CLRSetupFlags finalFlags = FindCallsByOverride(scriptType);
            if (scriptType.GetCustomAttribute<DontCacheComponentsAttribute>() != null ||
                scriptType.Assembly.GetCustomAttribute<DontCacheComponentsAttribute>() != null)
                finalFlags |= CLRSetupFlags.DontCacheComponents;

            if (scriptType.GetCustomAttribute<ManuallySubscribeToUpdatesAttribute>() != null)
                finalFlags |= CLRSetupFlags.ManualUpdatesSubscriptionControl;

            return finalFlags;
        }

        internal static CLRSetupFlags FindCallsByOverride(Type type)
        {
            CLRSetupFlags finalCalls = 0;

            addFromMethodName(nameof(OnPreUpdate),   CLRSetupFlags.PreUpdate);
            addFromMethodName(nameof(OnUpdate),      CLRSetupFlags.Update);
            addFromMethodName(nameof(OnFixedUpdate), CLRSetupFlags.FixedUpdate);
            addFromMethodName(nameof(OnLateUpdate),  CLRSetupFlags.LateUpdate);
            addFromMethodName(nameof(OnEarlyUpdate), CLRSetupFlags.EarlyUpdate);

            return finalCalls;

            void addFromMethodName(string method, CLRSetupFlags call)
            {
                if (ComponentHelpers.HasOverride(typeof(CLRScript).GetMethod(method), type))
                    finalCalls |= call;
            }
        }

        internal static Type GetExecutionOrderType(Type type)
        {
            Type startingType = type;
            ExecutionGroupBaseClassAttribute baseClassAttribute;

            do
            {
                baseClassAttribute = type.GetCustomAttribute<ExecutionGroupBaseClassAttribute>(false);

                if (baseClassAttribute is null)
                    type = type.BaseType;
                else break;
            }
            while (type != typeof(CLRScript));

            return baseClassAttribute is null ? startingType : type;
        }
    }
}