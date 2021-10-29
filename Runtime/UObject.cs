using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Assertions;

namespace HostGame
{
    /// <summary>
    /// Contains a reference to object of type UnityEngine.Object in form of integer,
    /// it allows to have this reference inside NativeArrays!
    /// </summary>
    public readonly struct UObject : IEquatable<UObject>
    {
        private readonly int instanceID;

        internal UObject(int instanceID)
        {
            this.instanceID = instanceID;
        }

        public T Dereference<T>() where T : UnityEngine.Object
        {
            if (instanceID == 0)
                return null;

            return ComponentManager.GetByInstanceID(instanceID) as T;
        }

        /// <summary>
        /// Caches instance id before creating instance of UObject
        /// </summary>
        public static UObject NewSafe(UnityEngine.Object obj)
        {
            ComponentManager.AddInstance(obj, throwIfExists: false);
            return (UObject)obj;
        }

        public override bool Equals(object obj)
        {
            return obj is UObject @object && Equals(@object);
        }

        public bool Equals(UObject other)
        {
            return instanceID == other.instanceID;
        }

        public override int GetHashCode()
        {
            return instanceID;
        }

        public static implicit operator UObject(UnityEngine.Object obj)
        {
            Assert.IsNotNull(obj, "You're trying to create reference to destroyed object");
            return new UObject(obj.GetInstanceID());
        }

        public static bool operator ==(UObject u0, UObject u1) => u0.instanceID == u1.instanceID;
        public static bool operator !=(UObject u0, UObject u1) => u0.instanceID != u1.instanceID;
    }

    /// <summary>
    /// Strictly typed version of UObject
    /// </summary>
    /// <typeparam name="T">Type of object it has reference to</typeparam>
    public readonly struct UObject<T> : IEquatable<UObject<T>> where T : UnityEngine.Object
    {
        private readonly int instanceID;

        internal UObject(int instanceID)
        {
            this.instanceID = instanceID;
        }

        public T Dereference()
        {
            if (instanceID == 0)
                return null;

            return ComponentManager.GetByInstanceID(instanceID) as T;
        }

        public static UObject NewSafe(UnityEngine.Object obj)
        {
            ComponentManager.AddInstance(obj, throwIfExists: false);
            return (UObject)obj;
        }

        public override bool Equals(object obj)
        {
            return obj is UObject<T> @object && Equals(@object);
        }

        public bool Equals(UObject<T> other)
        {
            return instanceID == other.instanceID;
        }

        public override int GetHashCode()
        {
            return instanceID;
        }

        public static implicit operator UObject<T>(T obj)
        {
            Assert.IsNotNull(obj, "You're trying to create reference to destroyed object");
            return new UObject<T>(obj.GetInstanceID());
        }

        public static explicit operator UObject(UObject<T> obj)
        {
            return new UObject(obj.instanceID);
        }

        public static bool operator ==(UObject<T> u0, UObject<T> u1) => u0.instanceID == u1.instanceID;
        public static bool operator !=(UObject<T> u0, UObject<T> u1) => u0.instanceID != u1.instanceID;
    }
}
