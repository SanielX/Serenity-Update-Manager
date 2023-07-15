using System;
using System.Reflection;
using UnityEngine;

namespace Serenity
{
    /// <summary>
    /// CLR scripts are run on special manager, override methods instead of adding magic unity methods
    /// </summary>
    public abstract class SRScript : MonoBehaviour
    {
        [HideInInspector] internal UpdateSetupFlags __setupFlags;
        
        private void Awake()
        {
            var type = GetType();
            
            // Caching setup calls for every type helps avoid reflection
            if (!SerenityScriptDataContainer.TryGetDefaultSetup(type, out __setupFlags)) 
            {
                __setupFlags = Setup(); // If no cached result, then do setup ourselves
            }

            ComponentLookup.AddObjectInstance(this);
            ComponentLookup.AddObjectInstance(gameObject);

            OnAwake();
        }

        private void Start()
        {
            OnStart();
        }

        private void OnEnable()
        {
            if (ShouldAutoSubscribe())
                UpdateManager.Add(this);

            OnEnabled();
        }

        private void OnDisable()
        {
            if (ShouldAutoSubscribe())
                UpdateManager.Remove(this);

            OnDisabled();
        }

        private void OnDestroy()
        {
            OnDestroyed();

            ComponentLookup.RemoveInstance(this);
            ComponentLookup.RemoveInstance(gameObject);
        }

        protected virtual UpdateSetupFlags Setup()
        {
            return DefaultSetupFunction(GetType());
        }

        private bool ShouldAutoSubscribe()
        {
            return (__setupFlags & UpdateSetupFlags.Updates) != 0;
        }

        protected virtual void OnAwake() { }

        /// <summary>
        /// It's safe to use void Start, so OnStart is for consistency only
        /// </summary>
        protected virtual void OnStart() { }

        protected virtual void OnEnabled() { }
        protected virtual void OnDisabled() { }

        protected internal virtual void OnPreUpdate() { }

        protected internal virtual void OnUpdate() { }
        protected internal virtual void OnLateUpdate() { }
        
        /// <summary>
        /// Called before <see cref="OnFixedUpdate"/> every frame
        /// </summary>
        protected internal virtual void OnEarlyUpdate() { }

        /// <summary>
        /// Called before <see cref="OnUpdate"/> but not every frame
        /// </summary>
        protected internal virtual void OnFixedUpdate() { }

        protected internal virtual void OnDestroyed() { }

        internal protected virtual void DrawDIMGUI() { }

        public static void IssueDIMGUIDraw() => UpdateManager.DIMGUIUpdateCallback();

        internal static UpdateSetupFlags DefaultSetupFunction(Type scriptType)
        {
            UpdateSetupFlags finalFlags = FindCallsByOverride(scriptType);
            
            return finalFlags;
        }

        internal static UpdateSetupFlags FindCallsByOverride(Type type)
        {
            UpdateSetupFlags finalCalls = 0;

            addFromMethodName(nameof(OnPreUpdate),   UpdateSetupFlags.PreUpdate);
            addFromMethodName(nameof(OnUpdate),      UpdateSetupFlags.Update);
            addFromMethodName(nameof(OnFixedUpdate), UpdateSetupFlags.FixedUpdate);
            addFromMethodName(nameof(OnLateUpdate),  UpdateSetupFlags.LateUpdate);
            addFromMethodName(nameof(OnEarlyUpdate), UpdateSetupFlags.EarlyUpdate);
            addFromMethodName(nameof(DrawDIMGUI),    UpdateSetupFlags.DIMGUIDraw);

            return finalCalls;

            void addFromMethodName(string method, UpdateSetupFlags call)
            {
                const BindingFlags bindings = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                if (ComponentHelpers.HasOverride(typeof(SRScript).GetMethod(method, bindings), type))
                    finalCalls |= call;
            }
        }
    }
    
    /// <summary>
    /// Can be put on class or assembly so that type or all types from certain assembly 
    /// do not try to ever cache components
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class DontCacheComponentsAttribute : Attribute { }
}