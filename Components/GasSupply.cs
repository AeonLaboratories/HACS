using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;
using System.Threading;
using Newtonsoft.Json;
using System.ComponentModel;

namespace HACS.Components
{
	/// <summary>
	/// Supplies a gas to a Destination Section
	/// via a Path Section. Set Path to null if 
	/// v_source is on the Destination boundary.
	/// </summary>
	public class GasSupply : HacsComponent
    {
		#region Component Implementation

		public static readonly new List<GasSupply> List = new List<GasSupply>();
		public static new GasSupply Find(string name) { return List.Find(x => x?.Name == name); }

		public GasSupply()
		{
			List.Add(this);
		}

		#endregion Component Implementation

		[XmlIgnore] public Action<string, string> Alert;
		[XmlIgnore] public StepTracker ProcessStep;

		[JsonProperty]
		public HacsComponent<RS232Valve> v_flowRef { get; set; }
        public RS232Valve v_flow => v_flowRef?.Component;

		[JsonProperty]
		public HacsComponent<HacsComponent> v_sourceRef { get; set; }
        public IValve v_source => v_sourceRef?.Component as IValve;

		// the DynamicQuantity that is supposed to reach TargetValue
		[JsonProperty]
		public HacsComponent<DynamicQuantity> ValueRef { get; set; }
        public DynamicQuantity Value => ValueRef?.Component;

		[JsonProperty]
		public HacsComponent<Section> DestinationRef { get; set; }
        /// <summary>
        /// The Section to receive the gas. The Chambers receive the gas.
        ///	Isolation isolates the Destination and PathToVacuum.
        ///	PathToVacuum evacuates the Destination.
        /// </summary>
        public Section Destination => DestinationRef?.Component;

		[JsonProperty]
		public HacsComponent<Section> PathRef { get; set; }
		/// <summary>
		/// Path should be null if v_source is on Destination boundary
		///	Chambers includes those along the path but not in Destination
		///	Isolation isolates the Path, and also closes any valves needed to isolate PathToVacuum.
		///	PathToVacuum should be null if it would breach the Destination Isolation boundary.
		///	InternalValves *is* the path (Its final valve should also be on Destination.Isolation.).
		/// </summary>
		public Section Path => PathRef?.Component ?? null;

		[JsonProperty]
		public int StartFlowPosition { get; set; }
		[JsonProperty]
		public double PosPerUnitRoC { get; set; }   // estimated initial movement required to change RoC by one unit
		[JsonProperty]
		public double PurgePressure { get; set; }   // when roughing through closed v_flow, 
													// p_Foreline will drop below this pressure
		[JsonProperty]
		public double SecondsSettlingTime { get; set; }
		[JsonProperty]
		public int MillisecondsCycleTime { get; set; }

		[JsonProperty]//, DefaultValue(20)]
		public int SecondsToPurge { get; set; } = 20;   // max

		public string GasName => string.IsNullOrEmpty(Name) ? Name.Split('_')[1] : "gas";

		void Wait(int milliseconds) { Thread.Sleep(milliseconds); }
        void Wait() { Wait(35); }


		/// <summary>
		/// Isolates Destination and Path sections. Ports on Destination are
		/// not altered, but any Ports on the Path are Closed.
		/// </summary>
		public void Isolate() { IsolateDestination(); IsolatePath(); }

		/// <summary>
		/// Isolates the Destination section. Destination Ports are not changed.
		/// </summary>
		public void IsolateDestination() { /* Destination?.ClosePorts(); */ Destination?.Isolate(); }

		/// <summary>
		/// Closes any Ports on the Path and Isolates the Path section.
		/// </summary>
		public void IsolatePath() { Path?.ClosePorts(); Path?.Isolate(); }


		/// <summary>
		/// Connects the Destination Chambers together (Opens Destination.InternalValves).
		/// </summary>
		public void JoinDestination() { Destination?.Open(); }

		/// <summary>
		/// Opens the gas Path (Path.InternalValves), if one is defined.
		/// </summary>
		public void JoinPath() { Path?.Open(); }

		/// <summary>
		/// Connects the Destination Chambers together (Opens Destination.InternalValves),
		/// and also joins the Path, if one is defined.
		/// </summary>
		public void Join() { JoinDestination(); JoinPath(); }

		/// <summary>
		/// Isolates the Destination and Path Sections, Opens the Destination and also
		/// the Path, if one is defined.
		/// </summary>
		void IsolateAndJoin() { Isolate(); Join(); }

		/// <summary>
		/// Opens the Path.PathToVacuum, or Destination.PathToVacuum if
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
		/// Closes the Path.PathToVacuum, or Destination.PathToVacuum if
		/// Path doesn't exist.
		/// </summary>
		public void IsolateFromVacuum()
		{
			if (Path != null)
				Path?.PathToVacuum?.Close();
			else
				Destination?.PathToVacuum.Close();
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
			Path?.InternalValves?.CloseLast();
			Path?.VacuumSystem?.IsolateManifold();
			Path?.PathToVacuum?.Open();
			Path?.VacuumSystem?.Evacuate(pressure);
		}

		/// <summary>
		/// Connects the gas supply to the Destination. Destination Ports are not changed.
		/// </summary>
		public void Admit()
        {
			IsolateAndJoin();
			v_source?.OpenWait();
			v_flow?.Open();			// but don't wait for flow valve
		}

		/// <summary>
		/// Waits for pressure, but stops waiting if 10 seconds elapse with no increase.
		/// </summary>
		/// <param name="pressure"></param>
		public void WaitForPressure(double pressure)
		{
			var sw = new Stopwatch();
			double peak = Value;
			ProcessStep?.Start($"Wait for {pressure:0} {Value.UnitSymbol} {GasName} in {Destination.Name}");
			sw.Restart();
			while (Value < pressure && sw.Elapsed.TotalSeconds < 10)
			{
				Wait();
				if (Value > peak)
				{
					peak = Value;
					sw.Restart();
				}
			}
			ProcessStep?.End();
		}

		/// <summary>
		/// Stops the flow of gas.
		/// </summary>
		public void ShutOff() { ShutOff(false); }

		public void ShutOff(bool alsoCloseFlow)
		{
			//if (v_flow != null)
			//	v_flow.Stop();   // TODO: need an elegant way to stop a valve that doesn't clobber other pending valve activity
			v_source?.CloseWait();
			if (alsoCloseFlow)
				v_flow?.CloseWait();
		}

		/// <summary>
		/// Admit the given pressure of gas into the Destination,
		/// then close the supply and flow valves. If there is no
		/// pressure reading available, silently waits one second
		/// before closing the valves. Ports on the Destination
		/// are not changed.
		/// </summary>
		/// <param name="pressure"></param>
		public void Admit(double pressure) { Admit(pressure, true); }

		public void Admit(double pressure, bool thenCloseFlow)
		{
			if (Value == null)
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
					if (Value >= pressure)
						break;
				}

				if (Value < 0.98 * pressure)       // tolerate 98% of target
				{
					Alert("Process Alert!", $"Couldn't admit {pressure:0} {Value.UnitSymbol} of {GasName} into {Destination.Name}");
				}
			}
		}

		public void Flush(double pressureHigh, double pressureLow)
		{ Flush(pressureHigh, pressureLow, 3); }

		public void Flush(double pressureHigh, double pressureLow, int flushes)
		{ Flush(pressureHigh, pressureLow, flushes, null); }

		public void Flush(double pressureHigh, double pressureLow, int flushes, Port port)
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
			v_flow?.Close();
		}

		/// <summary>
		/// Admits a gas into the Destination, controlling the flow rate 
		/// to achieve a higher level of precision over a wider range
		/// of target pressures. Requires a flow/metering valve.
		/// </summary>
		/// <param name="pressure"></param>
		public void Pressurize(double pressure)
		{
			bool normalized = false;

			for (int tries = 0; tries < 5; tries++)
			{
				normalized = NormalizeFlow(tries == 0);
				if (normalized) break;
				restoreRegulation();
			}

			if (!normalized)
				Alert?.Invoke("Alert!", v_flow.Name + " minimum flow is too high.");

			FlowPressurize(pressure);
		}

		/// <summary>
		/// Removes excessive gas from gas supply line to reduce the
		/// pressure into the regulated range.
		/// </summary>
		void restoreRegulation() => restoreRegulation(15);

		/// <summary>
		/// Removes excessive gas from gas supply line to reduce the
		/// pressure into the regulated range.
		/// </summary>
		/// <param name="secondsFlow">Seconds to evacuate at maximum flow rate.</param>
		void restoreRegulation(int secondsFlow)
		{
			Stopwatch sw = new Stopwatch();

			var vacuumSystem = Path?.VacuumSystem;
			if (vacuumSystem == null) vacuumSystem = Destination.VacuumSystem;
			if (vacuumSystem == null) return;

			ProcessStep?.Start($"Restore {GasName} pressure regulation");

			IsolateAndJoin();
			vacuumSystem.IsolateManifold();

			v_flow.Open();
			v_source.Open();
			JoinToVacuumManifold();
			vacuumSystem.Rough();

			while
			(
				vacuumSystem.State != VacuumSystem.States.Roughing &&
				vacuumSystem.State != VacuumSystem.States.Isolated
			)
				Wait();

			sw.Restart();
			while (sw.Elapsed.TotalSeconds < secondsFlow)
				Wait();

			Path?.InternalValves?.CloseLast();
			IsolateFromVacuum();
			v_source.Close();
			v_flow.Close();
			vacuumSystem.Isolate();

			ProcessStep?.End();
		}

		public bool NormalizeFlow() => NormalizeFlow(true);

		/// <summary>
		/// Evacuates most of the gas from between the shutoff and flow valves.
		/// </summary>
		public bool NormalizeFlow(bool calibrate)
		{
			Stopwatch sw = new Stopwatch();

			var vacuumSystem = Path?.VacuumSystem;
			if (vacuumSystem == null) vacuumSystem = Destination.VacuumSystem;
			if (vacuumSystem == null)
				throw new Exception("NormalizeFlow requires a VacuumSystem");

			ProcessStep?.Start($"Normalize {GasName}-{Destination.Name} flow conditions");

			v_flow.Close();
			if (calibrate)
			{
				ProcessStep?.Start("Calibrate flow valve");
				v_flow.Calibrate();
				ProcessStep?.End();
			}

			IsolateAndJoin();

			vacuumSystem.IsolateManifold();
			v_source.Open();
			JoinToVacuumManifold();
			vacuumSystem.Rough();

			while
			(
				vacuumSystem.State != VacuumSystem.States.Roughing &&
				vacuumSystem.State != VacuumSystem.States.Isolated
			)
				Wait();

			ProcessStep?.Start("Wait up to 2 seconds for Foreline to sense gas");
			sw.Restart();
			while (vacuumSystem.pForeline < PurgePressure && sw.Elapsed.TotalMilliseconds < 2000)
				Wait();
			ProcessStep?.End();

			ProcessStep?.Start("Drain flow-supply volume");
			sw.Restart();
			while (vacuumSystem.pForeline > PurgePressure && sw.Elapsed.TotalSeconds < SecondsToPurge)
				Wait();
			bool success = vacuumSystem.pForeline <= PurgePressure;

			Path?.InternalValves?.CloseLast();
			IsolateFromVacuum();
			v_source.Close();
			vacuumSystem.Isolate();

			ProcessStep?.End();
			ProcessStep?.End();

			return success;
		}

		/// <summary>
		/// Pressurize to a target value. Requires a flow valve.
		/// </summary>
		/// <param name="targetValue"></param>
		public void FlowPressurize(double targetValue)
        {
            double ppr = PosPerUnitRoC;   // initial estimate of valve position change to cause a unit-change in roc

            int maxMovement = 24;       // of the valve, in servo Positions
            double maxRate = 15.0;        // Value units/sec
            double lowRate = 1.0;
            double coastSeconds = 15;   // when to "Wait for pressure" instead of managing flow
            double lowRateSeconds = 20; // time to settle into lowRate before coasting

            double coastToDo = lowRate * coastSeconds;
            double lowRateToDo = coastToDo + lowRate * lowRateSeconds;

            int rampDownCycles = 10;
            // rampStart is in supplyValue units:
            //		When toDo < rampStart, scale the target rate down from max to low; 
            //		Allow rampDownCycles cycles for ramp down. Effective rate should be average of max and low
            double rampStart = (maxRate + lowRate) / 2 * rampDownCycles * MillisecondsCycleTime / 1000;

            var rateSpan = maxRate - lowRate;
            var rampLen = rampStart - lowRateToDo;

            ActuatorOperation operation = new ActuatorOperation()
            {
                Name = "Move",
                Value = 0,
                Incremental = true,
                Configuration = v_flow.FindOperation("Close").Configuration
            };

			Stopwatch loopTimer = new Stopwatch();
			int priorPos;
			double priorRoC;
			int amountToMove;
			double roc;

			double toDo = targetValue - Value;

			bool gasIsCO2 = Name.Contains("CO2");
			if (gasIsCO2)
				ProcessStep?.Start($"Admit {targetValue:0} {Value.UnitSymbol} into the {Destination.Name}");
			else
				ProcessStep?.Start($"Pressurize {Destination.Name} to {targetValue:0} {Value.UnitSymbol} with {GasName}");

			if (toDo > coastToDo)
			{
				IsolateAndJoin();
				v_source.Open();
			}

			int tRemaining;
			loopTimer.Restart();
			while ((toDo = targetValue - Value) > coastToDo && (tRemaining = MillisecondsCycleTime - (int)loopTimer.ElapsedMilliseconds) > 0)
				Wait(Math.Min(10, tRemaining));

			if (toDo > coastToDo)
			{
                ActuatorOperation startFlow = new ActuatorOperation()
                {
                    Name = "StartFlow",
                    Value = StartFlowPosition,
                    Incremental = false,
                    Configuration = operation.Configuration
                };
                v_flow.DoOperation(startFlow);
                v_flow.WaitForIdle();
			}

			loopTimer.Restart();
			while ((toDo = targetValue - Value) > coastToDo && (tRemaining = MillisecondsCycleTime - (int)loopTimer.ElapsedMilliseconds) > 0)
				Wait(Math.Min(10, tRemaining));

			priorPos = v_flow.Position;
			priorRoC = 0;
			amountToMove = 0;
			roc = Value.RoC;

			while (toDo > coastToDo)
			{
				loopTimer.Restart();

				var rampFraction = (toDo - lowRateToDo) / rampLen;
				if (rampFraction > 1) rampFraction = 1;
				if (rampFraction < 0) rampFraction = 0;
				var rTarget = lowRate + rateSpan * rampFraction;

				roc = Value.RoC;
				double drate = roc - priorRoC;
				double dpos = v_flow.Position - priorPos;
				if (Math.Abs(drate) > 0.5 && Math.Abs(dpos) > 2)
				{
					var latestPpr = dpos / drate;
					if (latestPpr < 0)
						ppr = DigitalFilter.WeightedUpdate(latestPpr, ppr, 0.4);
				}
				amountToMove = (int)(ppr * (rTarget - roc));

				ProcessStep?.Start($"rTg = {rTarget:0.0}, roc: {roc:0.0}, ppr: {ppr:0.0}, dpos: {amountToMove}");
				if (amountToMove != 0)
				{
					if (amountToMove > maxMovement) amountToMove = maxMovement;
					else if (amountToMove < -maxMovement) amountToMove = -maxMovement;

					priorPos = v_flow.Position;
					priorRoC = roc;

					operation.Value = amountToMove;
					v_flow.DoOperation(operation);
					v_flow.WaitForIdle();
				}

				while ((toDo = targetValue - Value) > coastToDo && (tRemaining = MillisecondsCycleTime - (int)loopTimer.ElapsedMilliseconds) > 0)
					Wait(Math.Min(10, tRemaining));

				ProcessStep?.End();
			}

			if (toDo > 0 && Value.IsRising && v_source.IsClosed)
			{
				IsolateAndJoin();
				v_flow.Close();
				v_source.Open();
			}

			ProcessStep?.Start($"Wait for {targetValue:0} {Value.UnitSymbol}");
            while (Value + Value.RoC * SecondsSettlingTime < targetValue && Value.IsRising)
                Wait();
            ProcessStep?.End();

			IsolateDestination();
			v_source.Close();
            v_flow.Close();

			ProcessStep?.Start($"Wait 20 seconds for maximum stability");
			loopTimer.Restart();
			while ((tRemaining = 20000 - (int)loopTimer.ElapsedMilliseconds) > 0)
				Wait(Math.Min(10, tRemaining));
			ProcessStep?.End();

			ProcessStep?.End();
		}
	}
}