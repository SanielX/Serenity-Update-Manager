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
    public enum CLRSetupFlags
    {
        PreUpdate = 2,
        Update = 4,
        LateUpdate = 8,
        FixedUpdate = 16,
        EarlyUpdate = 32,

        // Inversion so SafetyChecks and CacheComponents are default values
        NoSafetyChecks = 256,
        DontCacheComponents = 512,

        Updates = Update     | PreUpdate   |
                  LateUpdate | FixedUpdate |
                  EarlyUpdate
    }
}