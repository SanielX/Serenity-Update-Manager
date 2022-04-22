namespace HostGame
{
    public static class CLRManagerSettings
    {
        private static int _bucketGCFrequency = 10;

        /// <summary>
        /// Rounded to next power of 2 when set
        /// </summary>
        public static int BucketGCFrequency
        {
            get => (1 << _bucketGCFrequency);
            set => _bucketGCFrequency = value < 0? value : Mathf.Log(Mathf.NextPowerOf2(value), 2);
        }

        public static bool BlockInitializationQueue { get; set; }

        public static bool Enabled
        {
            get => CLRManager.Instance.enabled;
            set => CLRManager.Instance.enabled = value;
        }
    }
}