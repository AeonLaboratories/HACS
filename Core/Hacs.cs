namespace HACS.Core
{
	public static class Hacs
	{
        public static bool Connected;
        public static bool Initialized;
        public static bool Started;
        public static bool Stopped;

        public static void Connect()
		{
			HacsComponent.OnPreConnect?.Invoke();
			HacsComponent.OnConnect?.Invoke();
			HacsComponent.OnPostConnect?.Invoke();
            Connected = true;
		}

		public static void Initialize()
		{
			HacsComponent.OnPreInitialize?.Invoke();
			HacsComponent.OnInitialize?.Invoke();
			HacsComponent.OnPostInitialize?.Invoke();
			Initialized = true;
		}

		public static void Start()
		{
			HacsComponent.OnPreStart?.Invoke();
			HacsComponent.OnStart?.Invoke();
			HacsComponent.OnPostStart?.Invoke();
            Started = true;
		}

		public static void Stop()
		{
			HacsComponent.OnPreStop?.Invoke();
			HacsComponent.OnStop?.Invoke();
			HacsComponent.OnPostStop?.Invoke();
            Stopped = true;
		}
	}
}
