using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HostGame
{
    [System.Flags]
    public enum CLRSetupFlags : byte // trying to have least influence on size of a class, so make it byte
    {
        PreUpdate   = 1 << 0,
        Update      = 1 << 1,
        LateUpdate  = 1 << 2,
        FixedUpdate = 1 << 3,
        EarlyUpdate = 1 << 4,

        // Inversion so SafetyChecks and CacheComponents are default values
        NoSafetyChecks                   = 1 << 5,
        DontCacheComponents              = 1 << 6,
        ManualUpdatesSubscriptionControl = 1 << 7,

        Updates = Update     | PreUpdate   |
                  LateUpdate | FixedUpdate |
                  EarlyUpdate
    }
}