using HACS.Core;
using System;
using System.ComponentModel;
using Utilities;
using System.Threading;


namespace HACS.Components
{
	/// <summary>
	/// An object that raises a Detected event when a Condition is met.
	/// </summary>
	public class Detector : BindableObject
	{
        #region static

        public static PropertyChangedEventArgs DetectedEventArgs;
		static Detector()
		{
			DetectedEventArgs = PropertyChangedEventArgs(nameof(Detected));
		}

		#endregion static

		/// <summary>
		/// The condition that raises the Detected event.
		/// </summary>
		public Func<bool> Condition
		{
			get => condition;
			set => Ensure(ref condition, value);
		}
		Func<bool> condition;

		/// <summary>
		/// Raised whenever Condition becomes "met." I.e., its
		/// evaluation changes to true.
		/// </summary>
		public PropertyChangedEventHandler Detected { get; set; }

		/// <summary>
		/// The Condition was met the last time it was checked.
		/// Accessing this value does not cause Condition to be
		/// re-evaluated. Null if Condition is null.
		/// </summary>
		public bool? State
		{
			get => state;
			protected set => Ensure(ref state, value);
		}
		bool? state;
		bool Met => State ?? false;
		protected ManualResetEvent BecameMet { get; set; }

		protected virtual void NotifyDetected()
		{
			BecameMet.Set();
			Detected?.Invoke(this, DetectedEventArgs);
		}

		/// <summary>
		/// If Sensor is specified, Condition is re-evaluted whenever 
		/// Sensor's Value changes. 
		/// </summary>
		public ISensor Sensor
		{
			get => sensor;
			set => Ensure(ref sensor, value, OnPropertyChanged);
		}
		ISensor sensor;

		public virtual Stopwatch StateStopwatch { get; protected set; } = new Stopwatch();
		public virtual long MillisecondsInState => StateStopwatch.ElapsedMilliseconds;

		/// <summary>
		/// Evaluate Condition and raise the Detected event if its
		/// State changes to true.
		/// </summary>
		public void Update()
		{
			var priorState = State;
			State = Condition?.Invoke();
			if (State != priorState)
			{
				StateStopwatch.Restart();
				if (Met)
					NotifyDetected();
			}
		}

		protected void OnPropertyChanged(object sender = null, PropertyChangedEventArgs e = null)
		{
			var propertyName = e?.PropertyName;
			if (sender == Sensor)
			{
				if (propertyName == nameof(Sensor.Value))
					Update();
			}
		}

		/// <summary>
		/// Waits a potentially limited time for Condition to be met.
		/// The default timeout value of -1 is infinite.
		/// </summary>
		/// <param name="timeout">milliseconds to wait before giving up</param>
		/// <returns>true if Condition was Detected, false if it timed out</returns>
		public bool WaitForCondition(int timeout = -1)
		{
			BecameMet.Reset();
			return Met || BecameMet.WaitOne(timeout);
		}


		public Detector()
		{
			StateStopwatch.Restart();
		}
	}
}
