using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Threading;
using Utilities;

namespace HACS.Components
{
	/// <summary>
	/// A generic device State manager base class that conforms to 
	/// the HacsComponent Start/Stop model. It periodically executes 
	/// the derived class' ManageState Action.
	/// </summary>
	public class StateManager : HacsComponent, IStateManager
	{
		#region HacsComponent

		[HacsStart]
		protected virtual void Start()
		{
			if (LogEverything) Log.Record($"StateManager {Name}: Starting...");
			if (Stopped || Stopping)
			{
				Stopping = false;
				stateThread = new Thread(StateLoop) { Name = $"{Name} StateLoop", IsBackground = true };
				stateThread.Start();
			}
			if (LogEverything) Log.Record($"...StateManager {Name}: Started.");
		}

		[HacsStop]
		protected virtual void Stop()
		{
			Stopping = true;
			if (LogEverything) Log.Record($"StateManager {Name}: Stopping...");
			if (!Stopped)
			{
				StateSignal.Set();
				StoppedSignal.WaitOne();
			}
			if (LogEverything) Log.Record($"...StateManager {Name}: Stopped.");
		}

		#endregion HacsComponent

		#region Class interface properties and methods
		public override string Name
		{
			get { return base.Name; }
			set
			{
				var oldName = base.Name;
				if (value == oldName) return;
				base.Name = value;
				if (LogEverything)
					Log = default;
				if (!oldName.IsBlank())
					NotifyPropertyChanged();
			}
		}

		[JsonProperty, DefaultValue(500)]
		public virtual int IdleTimeout
		{ 
			get => idleTimeout;
			set => Ensure(ref idleTimeout, value);
		} 
		int idleTimeout = 500;

		public virtual bool Ready => true;

		public virtual bool HasWork => false;

		public virtual bool Busy => Ready && HasWork;

		public new virtual bool Stopped => StoppedSignal.WaitOne(0);

		public override string ToString()
		{
			return $"{Name}: {(Stopped ? "Stopped" : (Stopping ? "Stopping" : "Running"))}";
		}

		#endregion Class interface properties and methods

		#region State Management
		protected ManualResetEvent StoppedSignal { get; set; } = new ManualResetEvent(true);

        /// <summary>
        /// The State Manager is stopping.
        /// </summary>
		public virtual bool Stopping { get; protected set; }

 
		Thread stateThread;
		protected AutoResetEvent StateSignal { get; } = new AutoResetEvent(false);


		/// <summary>
		/// The StateLoop WaitOne received a signal before the
		/// timeout (StateLoopTimeout) period.
		/// </summary>
		protected bool StateSignalReceived = false;


		protected virtual void StateLoop()
		{
			if (LogEverything) Log.Record($"StateManager {Name}: Starting StateLoop.");
			StoppedSignal.Reset();
			try
			{
				// Busy check allows completion of StopActions before stopping
				while (!Stopping || Busy)
				{
					(this as IStateManager).ManageState?.Invoke();

					// Refer to StateLoopTimeout only once per loop; it can 
					// vary over multiple calls when it depends on changing
					// conditions.
					var timeout = StateLoopTimeout;
					if (timeout < 0) timeout = Timeout.Infinite;

					if (LogEverything) 
						Log?.Record($"StateManager {Name}: StateLoop is waiting for StateSignal" + 
							(timeout == Timeout.Infinite ? "..." : $" or {timeout} ms..."));

					StateSignalReceived = StateSignal.WaitOne(timeout);

					if (LogEverything)
					{
						Log?.Record(
							StateSignalReceived ?
							$"...StateManager {Name}: StateLoop signal received." :
							$"...StateManager {Name}: StateLoop timed out ({timeout} ms)."
							);
					}
				}
			}
			catch (Exception e) { LogMessage(e.ToString()); }

			if (LogEverything) Log.Record($"StateManager {Name}: StateLoop stopped.");
			StoppedSignal.Set();
		}

		Action IStateManager.ManageState { get; set; }


		/// <summary>
		/// Maximum time (milliseconds) for the StateLoop to wait before doing
		/// something. If StateLoopTimeout is -1, the StateLoop waits until a
		/// StateSignal is received. This property may be overridden by in the
		/// derived class, or set to a value in ManageState(). The default value
		/// is IdleTimeout.
		/// </summary>
		protected virtual int StateLoopTimeout => stateLoopTimeout ?? IdleTimeout;

		int IStateManager.StateLoopTimeout
		{
			get => stateLoopTimeout ?? IdleTimeout;
			set => stateLoopTimeout = value;
		}
		int? stateLoopTimeout;

		void IStateManager.StopWaiting() => StateSignal.Set();

		#endregion State Management

		protected void Wait() =>
			Thread.Sleep(35);

		/// <summary>
		/// A place to record transmitted and received messages,
		/// and various status conditions for debugging.
		/// </summary>
		public virtual LogFile Log
		{
			get => log ?? (Log = default);
			set
			{
				var oldfname = log?.FileName;
				var newfname = value is LogFile f ? f.FileName : LogFileName;
				if (newfname == oldfname && (value == null || value == log)) return;
				var newLog = value != default ? value : Name.IsBlank() ? default : new LogFile(newfname);
				newfname = newLog?.FileName;
				if (newLog != log)
				{
					var msg = $"StateManager {Name}: Log = \"{newfname}\", was \"{oldfname}\"";
					if (LogEverything) log?.Record(msg);
					log?.Close();
					Ensure(ref log, newLog);
					if (LogEverything) log?.Record(msg);
				}
			}
		}
		protected LogFile log;

		string LogFileName => $"{Name} Log.txt";

		/// <summary>
		/// For debugging, produce a verbose log file of the
		/// device operation. Keep this value false normally;
		/// it can very quickly produce extremely large files
		/// that will soon cripple the system.
		/// </summary>
		[JsonProperty]
		public virtual bool LogEverything
		{
			get => logEverything;
			set => Ensure(ref logEverything, value);
		}
		bool logEverything;

		public virtual void LogMessage(string message)
		{
			if (log != null)
				Log.Record(message);
			else
				Notice.Send(message);
		}
	}

	/// <summary>
	/// A device state manager. Monitors the current device State 
	/// and the desired TargetState, and works to bring State into 
	/// conformance with TargetState.
	/// </summary>
	/// <typeparam name="TargetStates">The TargetState type</typeparam>
	/// <typeparam name="States">The State type</typeparam>
	public class StateManager<TargetStates, States> : StateManager
	{
		protected override void Start()
		{
			base.Start();
			StateStopwatch.Restart();
		}

		/// <summary>
		/// The desired state of the device.
		/// </summary>
		[JsonProperty("State")]
		public virtual TargetStates TargetState
		{
			get => targetState;
			set => Ensure(ref targetState, value); 
		}
		TargetStates targetState;

        /// <summary>
        /// The current state of the device.
        /// </summary>
		public virtual States State
		{ 
			get => state;
			protected set
			{ 
				Ensure(ref state, value); 
				StateStopwatch.Restart();
			}
		}
		States state;

		/// <summary>
		/// A timer for the current state.
		/// </summary>
		protected Stopwatch StateStopwatch = new Stopwatch();

		/// <summary>
		/// How long the device has been in its current state.
		/// </summary>
		public virtual long MillisecondsInState { get => StateStopwatch.ElapsedMilliseconds; protected set { } }
		public virtual double MinutesInState { get => StateStopwatch.Elapsed.TotalMinutes; protected set { } }

		/// <summary>
		/// Change the TargetState
		/// </summary>
		/// <param name="targetState">the new TargetState</param>
		public virtual void ChangeState(TargetStates targetState)
		{
			ChangeState(targetState, null);
		}

        /// <summary>
        /// Ensure the State Manager is running, 
        /// change the TargetState, and if a
        /// predicate is provided, wait until it is true.
        /// </summary>
        /// <param name="targetState"></param>
        /// <param name="predicate"></param>
		public void ChangeState(TargetStates targetState, Predicate<StateManager<TargetStates, States>> predicate)
		{
			if (Stopped) Start();
			TargetState = targetState;
			StateSignal.Set();		// release the StateLoop if it's waiting
			while (!predicate?.Invoke(this) ?? false)
				Wait();
		}

		public override string ToString()
		{
			return $"{Name}{(Stopped ? " (Stopped)" : Stopping ? " (Stopping)" : "")}: {State}";
		}
	}
}