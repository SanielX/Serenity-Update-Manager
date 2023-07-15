﻿using UnityEngine;

namespace Serenity
{
    public static class UpdateManagerSettings
    {
        private static int _bucketGCFrequency = 10;

        /// <summary>
        /// Rounded to next power of 2 when set
        /// </summary>
        public static int BucketGCFrequency
        {
            get => (1 << _bucketGCFrequency);
            set => _bucketGCFrequency = value < 0? value : (int)Mathf.Log(Mathf.NextPowerOfTwo(value), 2);
        }

        public static bool BlockInitializationQueue { get; set; }

        public static bool Enabled
        {
            get => UpdateManager.Instance.enabled;
            set => UpdateManager.Instance.enabled = value;
        }
    }
}