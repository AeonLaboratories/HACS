using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Utilities;

namespace HACS.Components
{
	/// <summary>
	/// Supplies a gas to a Destination Section via a Path Section. 
	/// Set Path to null if v_source is on the Destination boundary.
	/// </summary>
	public class GasSupply : HacsComponent, IGasSupply
	{
		#region HacsComponent

		[HacsConnect]
		protected virtual void Connect()
		{
			FlowValve = Find<IRS232Valve>(flowValveName);
			SourceValve = Find<IValve>(sourceValveName);
			Meter = Find<IMeter>(meterName);
			Destination = Find<ISection>(destinationName);
			Path = Find<ISection>(pathName);
			FlowManager = Find<FlowManager>(flowManagerName);
		}

		#endregion HacsComponent

		/// <summary>
		/// A StepTracker to receive ongoing process state messages.
		/// </summary>
		public StepTracker ProcessStep
		{
			get => processStep ?? StepTracker.Default;
			set => Ensure(ref processStep, value);
		}
		StepTracker processStep;

		/// <summary>
		/// The name of the gas.
		/// </summary>
		[JsonProperty]
		public string GasName
		{
			get => gasName ?? (Name.IsBlank() ? "gas" :
				gasName = Name.Split('.')[0]);
			set => gasName = value;
		}
		string gasName;

		[JsonProperty("SourceValve")]
		string SourceValveName { get => SourceValve?.Name; set => sourceValveName = value; }
		string sourceValveName;
		/// <summary>
		/// The gas supply shutoff valve.
		/// </summary>
		public IValve SourceValve
		{
			get => sourceValve;
			set => Ensure(ref sourceValve, value, NotifyPropertyChanged);
		}
		IValve sourceValve;

		[JsonProperty("FlowValve")]
		string FlowValveName { get => FlowValve?.Name; set => flowValveName = value; }
		string flowValveName;
		/// <summary>
		/// The valve (if any) that controls the gas supply flow.
		/// </summary>
		public IRS232Valve FlowValve
		{
			get => flowValve;
			set => Ensure(ref flowValve, value, NotifyPropertyChanged);
		}
		IRS232Valve flowValve;

		[JsonProperty("Meter")]
		string MeterName { get => Meter?.Name; set => meterName = value; }
		string meterName;
		/// <summary>
		/// The Meter that is supposed to reach the target value when a 
		/// controlled amount of gas is to be admitted to the Destination.
		/// </summary>
		public IMeter Meter
		{
			get => meter;
			set => Ensure(ref meter, value, NotifyPropertyChanged);
		}
		IMeter meter;

		/// <summary>
		/// The typical time between closing the shutoff valve and Meter.Value stability.
		/// </summary>
		[JsonProperty]
		public double SecondsSettlingTime
		{
			get => secondsSettlingTime;
			set => Ensure(ref secondsSettlingTime, value);
		}
		double secondsSettlingTime;

		[JsonProperty("FlowManager")]
		string FlowManagerName { get => FlowManager?.Name; set => flowManagerName = value; }
		string flowManagerName;
		/// <summary>
		/// The control system that manages the flow valve position to
		/// achieve a desired condition, usually a target Value for Meter or 
		/// its RateOfChange.
		/// </summary>
		public IFlowManager FlowManager
		{
			get => flowManager;
			set => Ensure(ref flowManager, value);
		}
		IFlowManager flowManager;

		[JsonProperty("Destination")]
		string DestinationName { get => Destination?.Name; set => destinationName = value; }
		string destinationName;
		/// <summary>
		/// The Section to receive the gas. The Section's Isolation ValveList isolates the Destination 
		/// and also the PathToVacuum. The Section's PathToVacuum ValveList joins the Destination 
		/// volume to the Vacuum Manifold.
		/// </summary>
		public ISection Destination
		{
			get => destination;
			set => Ensure(ref destination, value, NotifyPropertyChanged);
		}
		ISection destination;

		[JsonProperty("Path")]
		string PathName { get => Path?.Name; set => pathName = value; }
		string pathName;
		/// <summary>
		/// The Section comprising the Chambers between v_source and Destination. 
		/// Set Path to null if v_source is on the Destination boundary. Set 
		/// Path.PathToVacuum to null if Path cannot be evacuated without also 
		/// evacuating Destination. Path.InternalValves *is* the path except
		/// for the final valve between Path and Destination.
		/// </summary>
		public ISection Path
		{
			get => path;
			set => Ensure(ref path, value, NotifyPropertyChanged);
		}
		ISection path;

		/// <summary>
		/// When roughing through "Closed" v_flow, the vacuum system's foreline pressure should
		/// fall below this value.
		/// </summary>
		[JsonProperty, DefaultValue(2.0)]
		public double PurgePressure
		{
			get => purgePressure;
			set => Ensure(ref purgePressure, value);
		}
		double purgePressure = 2.0;


		/// <summary>
		/// When roughing through "Closed" v_flow, if it takes longer than this 
		/// for the vacuum system's foreline pressure to fall below PurgePressure, 
		/// issue a warning.
		/// </summary>
		[JsonProperty, DefaultValue(20)]
		public int SecondsToPurge
		{
			get => secondsToPurge;
			set => Ensure(ref secondsToPurge, value);
		}
		int secondsToPurge = 20;   // max

		// TODO these two methods do not belong here
		void Wait(int milliseconds) { Thread.Sleep(milliseconds); }
        void Wait() { Wait(35); }

		/// <summary>
		/// Isolate Destination and Path, then Open the Destination and Path,
		/// joined together.
		/// </summary>
		public void IsolateAndJoin()
		{
			var toBeOpened = Destination?.InternalValves?.SafeUnion(Path?.InternalValves);
			var joinsDestinationToPath = Destination?.Isolation.SafeIntersect(Path?.Isolation);
			toBeOpened = toBeOpened.SafeUnion(joinsDestinationToPath);
			var toBeClosed = Destination?.Isolation.SafeUnion(Path?.Isolation);

			Path?.ClosePorts();
			toBeClosed?.CloseExcept(toBeOpened);
			toBeOpened?.Open();
		}

		/// <summary>
		/// Open the Path.PathToVacuum, or Destination.PathToVacuum if
		/// Path doesn't exist.
		/// </summary>
		public void JoinToVacuumManifold()
		{
			if (Path != null)
				Path?.PathToVacuum?.Open();
			else
				Destination?.PathToVacuum.Open();
		}

		/// <summary>
		/// Isolates the Path and Destination from vacuum.
		/// </summary>
		public void IsolateFromVacuum()
		{
			if (Path?.PathToVacuum is List<IValve> list && list.Any())
				list.First().CloseWait();
			else
				Path?.VacuumSystem?.Isolate();

			if (Destination?.PathToVacuum is List<IValve> list2 && list2.Any())
				list2.First().CloseWait();
			else
				Destination?.VacuumSystem?.Isolate();
		}

		/// <summary>
		/// Evacuate the Path, but not the Destination. Does nothing
		/// if this is not possible.
		/// </summary>
		public void EvacuatePath() { EvacuatePath(-1); }

		/// <summary>
		/// Evacuate the Path to pressure, but not the Destination.
		/// Does nothing if this is not possible.
		/// </summary>
		/// <param name="pressure"></param>
		public void EvacuatePath(double pressure)
		{
			// Do nothing if it's impossible to evacuate Path without
			// also evacuating Destination.
			if (Destination?.Isolation != null && 
				Path?.PathToVacuum != null &&
				Destination.Isolation.SafeIntersect(Path.PathToVacuum).Any())
				return;

			Path?.OpenAndEvacuate(pressure);
		}

		/// <summary>
		/// Stop the flow of gas.
		/// </summary>
		public void ShutOff() { ShutOff(false); }

		/// <summary>
		/// Close the source/shutoff valve, and optionally close the flow valve, too.
		/// </summary>
		/// <param name="alsoCloseFlow"></param>
		public void ShutOff(bool alsoCloseFlow)
		{
			if (FlowValve != null)
				FlowValve.Stop();
			SourceValve?.CloseWait();
			if (alsoCloseFlow)
				FlowValve?.CloseWait();
		}

		/// <summary>
		/// Wait for pressure, but stop waiting if 10 seconds elapse with no increase.
		/// </summary>
		/// <param name="pressure"></param>
		public void WaitForPressure(double pressure)
		{
			var sw = new Stopwatch();
			double peak = Meter.Value;
			ProcessStep?.Start($"Wait for {pressure:0} {Meter.UnitSymbol} {GasName} in {Destination.Name}");
			sw.Restart();
			while (Meter.Value < pressure && sw.Elapsed.TotalSeconds < 10)
			{
				Wait();
				if (Meter.Value > peak)
				{
					peak = Meter.Value;
					sw.Restart();
				}
			}
			ProcessStep?.End();
		}

		/// <summary>
		/// Connect the gas supply to the Destination. Destination Ports are not changed.
		/// </summary>
		public void Admit()
        {
			IsolateAndJoin();
			SourceValve?.OpenWait();
			FlowValve?.Open();			// but don't wait for flow valve
		}

		/// <summary>
		/// Admit the given pressure of gas into the Destination,
		/// then close the supply and flow valves. If there is no
		/// pressure reading available, silently waits one second
		/// before closing the valves. Ports on the Destination
		/// are not changed.
		/// </summary>
		public void Admit(double pressure) { Admit(pressure, true); }

        /// <summary>
		/// Admit the given pressure of gas into the Destination,
		/// then close the source/shutoff valve and, optionally,
        /// the flow valve. If no pressure reading is available, 
        /// silently waits one second before closing the valves. 
        /// Ports on the Destination are not changed.
        /// </summary>
		public void Admit(double pressure, bool thenCloseFlow)
		{
			if (Meter == null)
			{
				Admit();
				Wait(1000);
				ShutOff(thenCloseFlow);
			}
			else
			{
				for (int i = 0; i < 5; ++i)
				{
					Admit();
					WaitForPressure(pressure);
					ShutOff(thenCloseFlow);
					Wait(3000);
					if (Meter.Value >= pressure)
						break;
				}

				if (Meter.Value < 0.98 * pressure)       // tolerate 98% of target
				{
					Alert.Send("Process Alert!", $"Couldn't admit {pressure:0} {Meter.UnitSymbol} of {GasName} into {Destination.Name}");
				}
			}
		}

        /// <summary>
        /// Perform three flushes, each time admitting gas at pressureHigh 
        /// into Destination, and then evacuating to pressureLow.
        /// </summary>
        /// <param name="pressureHigh">pressure of gas to admit</param>
        /// <param name="pressureLow">evacuation pressure</param>
		public void Flush(double pressureHigh, double pressureLow)
		{ Flush(pressureHigh, pressureLow, 3); }

        /// <summary>
        /// Perform the specified number of flushes, each time admitting gas 
        /// at pressureHigh into Destination, and then evacuating to pressureLow.
        /// </summary>
        /// <param name="pressureHigh">pressure of gas to admit</param>
        /// <param name="pressureLow">evacuation pressure</param>
        /// <param name="flushes">number of times to flush</param>
		public void Flush(double pressureHigh, double pressureLow, int flushes)
		{ Flush(pressureHigh, pressureLow, flushes, null); }

        /// <summary>
        /// Perform the specified number of flushes, each time admitting gas 
        /// at pressureHigh into Destination, and then evacuating to pressureLow.
        /// If a port is specified, then all Destination ports are closed before
        /// the gas is admitted, and the given port is opened before evacuation.
        /// </summary>
        /// <param name="pressureHigh">pressure of gas to admit</param>
        /// <param name="pressureLow">evacuation pressure</param>
        /// <param name="flushes">number of times to flush</param>
        /// <param name="port">port to be flushed</param>
		public void Flush(double pressureHigh, double pressureLow, int flushes, IPort port)
		{
			for (int i = 1; i <= flushes; i++)
			{
				ProcessStep?.Start($"Flush {Destination.Name} with {GasName} ({i} of {flushes})");
				if (port != null) Destination.ClosePorts();
				Admit(pressureHigh, false);
				port?.Open();
				Destination.Evacuate(pressureLow);
				ProcessStep?.End();
			}
			FlowValve?.CloseWait();
		}

		/// <summary>
		/// Admit a gas into the Destination, controlling the flow rate 
		/// to achieve a higher level of precision over a wider range
		/// of target pressures. Requires a flow/metering valve.
		/// </summary>
		/// <param name="pressure">desired final pressure</param>
		public void Pressurize(double pressure)
		{
			bool normalized = false;

			for (int tries = 0; tries < 5; tries++)
			{
				normalized = NormalizeFlow();
				if (normalized) break;
				RestoreRegulation();
			}

			if (!normalized)
				Alert.Send("Alert!", FlowValve.Name + " minimum flow is too high.");

			FlowPressurize(pressure);
		}

		/// <summary>
		/// Remove excessive gas from gas supply line to reduce the
		/// pressure into the regulated range.
		/// </summary>
		void RestoreRegulation() => RestoreRegulation(5);

		/// <summary>
		/// Remove excessive gas from gas supply line to reduce the
		/// pressure into the regulated range.
		/// </summary>
		/// <param name="secondsFlow">Seconds to evacuate at maximum flow rate.</param>
		void RestoreRegulation(int secondsFlow)
		{
			Stopwatch sw = new Stopwatch();

			var vacuumSystem = Path?.VacuumSystem;
			if (vacuumSystem == null) vacuumSystem = Destination.VacuumSystem;
			if (vacuumSystem == null) return;

			ProcessStep?.Start($"Restore {GasName} pressure regulation");

			IsolateAndJoin();
			vacuumSystem.IsolateManifold();

			FlowValve.Open();
			SourceValve.OpenWait();
			JoinToVacuumManifold();
			vacuumSystem.Rough();

			while
			(
				vacuumSystem.State != VacuumSystem.StateCode.Roughing &&
				vacuumSystem.State != VacuumSystem.StateCode.Isolated
			)
				Wait();

			sw.Restart();
			while (sw.Elapsed.TotalSeconds < secondsFlow)
				Wait();

			Path?.InternalValves?.CloseLast();
			IsolateFromVacuum();
			SourceValve.Close();
			FlowValve.CloseWait();
			vacuumSystem.Isolate();

			ProcessStep?.End();
		}

		/// <summary>
		/// Evacuate most of the gas from between the shutoff and flow valves.
		/// </summary>
        /// <param name="calibrate">calibrate the flow valve first</param>
        /// <returns>success</returns>
		public bool NormalizeFlow(bool calibrate = false)
		{
			Stopwatch sw = new Stopwatch();

			var vacuumSystem = Path?.VacuumSystem ?? Destination.VacuumSystem;
			if (vacuumSystem == null)
				throw new Exception("NormalizeFlow requires a VacuumSystem");

			ProcessStep?.Start($"Normalize {GasName}-{Destination.Name} flow conditions");

			FlowValve.CloseWait();
			if (calibrate)
			{
				ProcessStep?.Start("Calibrate flow valve");
				FlowValve.Calibrate();
				ProcessStep?.End();
			}

			var toBeOpened = Destination?.InternalValves.SafeUnion(Path?.InternalValves);
			if (Path?.PathToVacuum != null)
				toBeOpened = toBeOpened.SafeUnion(Path.PathToVacuum);
			else
				toBeOpened = toBeOpened.SafeUnion(Destination?.PathToVacuum);
			var toBeClosed = Destination?.Isolation.SafeUnion(Path?.Isolation); 
			
			vacuumSystem.Isolate();
			vacuumSystem.IsolateExcept(toBeOpened);
			Path?.ClosePorts();
			toBeClosed?.CloseExcept(toBeOpened);
			toBeOpened?.Open();
			SourceValve.OpenWait();
			vacuumSystem.Evacuate();

			while (!vacuumSystem.HighVacuumValve.IsOpened && !vacuumSystem.LowVacuumValve.IsOpened)
				Wait();
			Wait(2000);
			ProcessStep?.End();

			ProcessStep?.Start("Drain flow-supply volume");
			sw.Restart();
			while (vacuumSystem.Pressure > PurgePressure && sw.Elapsed.TotalSeconds < SecondsToPurge)
				Wait();
			bool success = vacuumSystem.Pressure <= PurgePressure;

			SourceValve.CloseWait();
			IsolateFromVacuum();
			ProcessStep?.End();

			return success;
		}


		/// <summary>
		/// Pressurize Destination to the given target value. Requires a flow valve.
		/// </summary>
		/// <param name="targetValue">desired final pressure or other metric</param>
		public void FlowPressurize(double targetValue)
        {
			if (!(FlowManager is IFlowManager))
			{
				var subject = "Configuration Error";
				var message = $"GasSupply {Name}: FlowPressurize() requires FlowManager.";
				Alert.Warn(subject, message); 
				return;
			}

			bool gasIsCO2 = Name.Contains("CO2");
			if (gasIsCO2)
				ProcessStep?.Start($"Admit {targetValue:0} {Meter.UnitSymbol} into the {Destination.Name}");
			else
				ProcessStep?.Start($"Pressurize {Destination.Name} to {targetValue:0} {Meter.UnitSymbol} with {GasName}");

			IsolateAndJoin();
			SourceValve.OpenWait();

			FlowManager.Start(targetValue);
            while (FlowManager.Busy && Meter.Value + Meter.RateOfChange * SecondsSettlingTime < targetValue)
                Wait();
			SourceValve.CloseWait();
			FlowManager.Stop();

			Destination?.Isolate();
			FlowValve.CloseWait();

			ProcessStep?.End();
		}
	}
}