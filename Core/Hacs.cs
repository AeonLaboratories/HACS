using System;
using System.Threading;

namespace HACS.Core
{
	public static class Hacs
	{
		public static HacsLog EventLog
		{
			get => eventLog ??= new HacsLog("Event log.txt") { ArchiveDaily = false };
			set => eventLog = value; 
		}
		static HacsLog eventLog;
		public static bool Connected { get; private set; }
		public static bool Initialized { get; private set; }
		public static bool Started { get; private set; }
		public static bool Stopping { get; private set; }
		public static bool Stopped { get; private set; }

		public static Action OnPreConnect;
		public static Action OnConnect;
		public static Action OnPostConnect;

		public static Action OnPreInitialize;
		public static Action OnInitialize;
		public static Action OnPostInitialize;

		public static Action OnPreStart;
		public static Action OnStart;
		public static Action OnPostStart;

		public static Action OnPreUpdate;
		public static Action OnUpdate;
		public static Action OnPostUpdate;

		public static Action OnPreStop;
		public static Action OnStop;
		public static Action OnPostStop;

		public static void Connect()
		{
			OnPreConnect?.AsyncInvoke();
			OnConnect?.AsyncInvoke();
			OnPostConnect?.AsyncInvoke();
            Connected = true;
		}

		public static void Initialize()
		{
			OnPreInitialize?.AsyncInvoke();
			OnInitialize?.AsyncInvoke();
			OnPostInitialize?.AsyncInvoke();
			Initialized = true;
		}

		public static void Start()
		{
			OnPreStart?.AsyncInvoke();
			OnStart?.AsyncInvoke();
			OnPostStart?.AsyncInvoke();
            Started = true;
		}

		public static void Update()
		{
			OnPreUpdate?.AsyncInvoke();
			OnUpdate?.AsyncInvoke();
			OnPostUpdate?.AsyncInvoke();
		}

		public static void Stop()
		{
			Stopping = true;
			OnPreStop?.AsyncInvoke();
			OnStop?.AsyncInvoke();
			OnPostStop?.AsyncInvoke();
			HacsLog.List.ForEach(log => { if (log != EventLog) log.Close(); });
			// Event log should be closed immediately before Application exits.
			Stopped = true;
		}
	}
}