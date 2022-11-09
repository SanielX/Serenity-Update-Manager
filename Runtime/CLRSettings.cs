using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HostGame
{
    [System.Flags]
    public enum CLRSetupFlags
    {
        PreUpdate   = 1 << 0,
        Update      = 1 << 1,
        LateUpdate  = 1 << 2,
        FixedUpdate = 1 << 3,
        EarlyUpdate = 1 << 4,

        // Inversion so SafetyChecks and CacheComponents are default values
        NoSafetyChecks                   = 1 << 5,


        DIMGUIDraw = 1 << 14,

        Updates = Update      | PreUpdate   |
                  LateUpdate  | FixedUpdate |
                  EarlyUpdate | DIMGUIDraw
    }

    internal static class CLRSetupFlagsExt
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool Has(this CLRSetupFlags value, CLRSetupFlags flag) => (value & flag) != 0;
    }
}