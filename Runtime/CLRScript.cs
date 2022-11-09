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
    /// CLR scripts are run on special manager, override methods instead of adding magic unity methods
    /// </summary>
    public abstract class CLRScript : MonoBehaviour, IGOComponent
    {
        [HideInInspector] internal CLRSetupFlags __setupFlags;
        
        Transform  IGOComponent.iTransform  => transform;
        GameObject IGOComponent.iGameObject => gameObject;

        
        private void Awake()
        {
            var type = GetType();
            
            // Caching setup calls for every type helps avoid reflection
            if (!CLRScriptDataContainer.TryGetDefaultSetup(type, out __setupFlags)) 
            {
                __setupFlags = Setup(); // If no cached result, then do setup ourselves
            }

            ComponentManager.AddObjectInstance(this);
            ComponentManager.AddObjectInstance(gameObject);

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

            ComponentManager.RemoveInstance(this);
            ComponentManager.RemoveInstance(gameObject);
        }

        protected virtual CLRSetupFlags Setup()
        {
            return DefaultSetupFunction(GetType());
        }

        private bool ShouldAutoSubscribe()
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

        internal protected virtual void DrawDIMGUI() { }

        public static void IssueDIMGUIDraw() => CLRManager.DIMGUIUpdateCallback();

        internal static CLRSetupFlags DefaultSetupFunction(Type scriptType)
        {
            CLRSetupFlags finalFlags = FindCallsByOverride(scriptType);
            
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
            addFromMethodName(nameof(DrawDIMGUI),    CLRSetupFlags.DIMGUIDraw);

            return finalCalls;

            void addFromMethodName(string method, CLRSetupFlags call)
            {
                const BindingFlags bindings = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                if (ComponentHelpers.HasOverride(typeof(CLRScript).GetMethod(method, bindings), type))
                    finalCalls |= call;
            }
        }
    }
}