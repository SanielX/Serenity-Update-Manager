using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HostGame
{
    /// 
    /// For start functions it goes like this:
    /// Awake -> Start -> 1 frame skipped -> ManagedStart
    /// 
    [System.Flags]
    public enum Calls
    {
        ManagedStart = 1,
        PreUpdate = 2,
        Update = 4,
        LateUpdate = 8,
        FixedUpdate = 16,
        EarlyUpdate = 32,

        Updates = Update     | PreUpdate   |
                  LateUpdate | FixedUpdate |
                  EarlyUpdate
    }

    public readonly struct CLRSettings
    {
        public readonly Calls UsedCalls;
        public readonly bool NoSafetyChecks;

        public CLRSettings(Calls calls, bool noSafeChecks = false)
        {
            UsedCalls = calls;
            NoSafetyChecks = noSafeChecks;
        }
    }
}