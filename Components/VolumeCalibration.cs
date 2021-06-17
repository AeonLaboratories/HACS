using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Utilities;
using static Utilities.Utility;
using static HACS.Components.CegsPreferences;

namespace HACS.Components
{
	public class VolumeCalibration : HacsComponent, IVolumeCalibration
	{
		#region HacsComponent

		[HacsConnect]
		protected virtual void Connect()
		{
			GasSupply = Find<GasSupply>(gasSupplyName);
		}

		#endregion HacsComponent

		[JsonProperty("GasSupply")]
		string GasSupplyName { get => GasSupply?.Name; set => gasSupplyName = value; }
		string gasSupplyName;
        public IGasSupply GasSupply
		{
			get => gasSupply;
			set => Ensure(ref gasSupply, value, NotifyPropertyChanged);
		}
		IGasSupply gasSupply;

		[JsonProperty]
		public double CalibrationPressure
		{
			get => calibrationPressure;
			set => Ensure(ref calibrationPressure, value);
		}
		double calibrationPressure;

		[JsonProperty]
		public int CalibrationMinutes
		{
			get => calibrationMinutes;
			set => Ensure(ref calibrationMinutes, value);
		}
		int calibrationMinutes;

		[JsonProperty]
		public List<VolumeExpansion> Expansions
		{
			get => expansions;
			set => Ensure(ref expansions, value);
		}
		List<VolumeExpansion> expansions;

		// Set true to measure a volume by expanding into a Known Volume.
		// Normally false.
		[JsonProperty]
		public bool ExpansionVolumeIsKnown
		{
			get => expansionVolumeIsKnown;
			set => Ensure(ref expansionVolumeIsKnown, value);
		}
		bool expansionVolumeIsKnown;

		public StepTracker ProcessStep
		{
			get => processStep;
			set => Ensure(ref processStep, value);
		}
		StepTracker processStep;
		public StepTracker ProcessSubStep
		{
			get => processSubStep;
			set => Ensure(ref processSubStep, value);
		}
		StepTracker processSubStep;
		public Action OpenLine { get; set; }
		public double OkPressure
		{
			get => okPressure;
			set => Ensure(ref okPressure, value);
		}
		double okPressure;
		public IMeter Manometer => GasSupply.Destination.Manometer;
		public double Kelvins => GasSupply.Destination.Temperature + ZeroDegreesC;
	
		public HacsLog Log { get; set; }


		// p1 (v0 + v1) = p0 v0; p0 / p1 = (v0 + v1) / v0 = v1 / v0 + 1
		// v1 / v0 = p0 / p1 - 1
		double v1_v0(double[] p0, double[] p1) { return meanQuotient(p0, p1) - 1; }
		double v1(double v0, double[] p0, double[] p1) { return v0 * v1_v0(p0, p1); }
		double[][] daa(int n, int m)
		{
			double[][] a = new double[n][];
			for (int i = 0; i < n; ++i) a[i] = new double[m];
			return a;
		}
		double meanQuotient(double[] numerators, double[] denominators)
		{
			// WARNING: no checking for divide-by-zero or programmer errors
			// like empty or mis-matched arrays, etc
			double s = 0;
			int n = numerators.Length;
			for (int i = 0; i < n; i++)
				s += numerators[i] / denominators[i];
			return s / n;
		}

		void openLine()
		{
			var vsList = new List<VacuumSystem>();

			GasSupply.Destination.Isolation.ForEach(v =>
			{
				var vvs = NamedObject.FirstOrDefault<VacuumSystem>(vs => vs.HighVacuumValve == v || vs.LowVacuumValve == v);
				if (vvs != null && !vsList.Exists(vs => vs == vvs))
					vsList.Add(vvs);
			});

            Expansions.ForEach(exp =>
            {
                exp.ValveList.ForEach(v =>
                {
					var vvs = NamedObject.FirstOrDefault<VacuumSystem>(vs => vs.HighVacuumValve == v || vs.LowVacuumValve == v);
					if (vvs != null && !vsList.Exists(vs => vs == vvs))
                        vsList.Add(vvs);
                });
            });

            vsList.ForEach(vs => { vs.Evacuate(); });

			OpenLine();

			vsList.ForEach(vs => { vs.WaitForPressure(OkPressure); });
			GasSupply.Destination.VacuumSystem.WaitForPressure(OkPressure);
		}

		void admitGas()
		{
			ProcessSubStep.Start("Open line");
			openLine();
			ProcessSubStep.End();

			ProcessSubStep.Start($"Admit calibration gas into {GasSupply.Destination.Name}");
			GasSupply.Destination.ClosePorts();
			GasSupply.Pressurize(CalibrationPressure);
			ProcessSubStep.End();

			ProcessSubStep.Start("Evacuate unnecessary gas from supply path.");
			GasSupply.EvacuatePath(OkPressure);
			ProcessSubStep.End();
		}

		double measure(VolumeExpansion expansion)
		{
			var valves = expansion?.ValveList;
			if (valves != null && valves.Any())
			{
				ProcessSubStep.Start($"Expand gas into {expansion.Chamber.Name} via {valves[0].Name}");
				valves.CloseExcept(new List<IValve>(){ valves[0]});
				valves[0].OpenWait();
				ProcessSubStep.End();
				ProcessSubStep.Start("Wait 15 seconds");
				while (ProcessSubStep.Elapsed.TotalSeconds < 15)
					Thread.Sleep(100);
				ProcessSubStep.End();
			}

			ProcessSubStep.Start($"Wait a minimum of {MinutesString(CalibrationMinutes)}.");
			int tRemaining;
			while ((tRemaining = CalibrationMinutes * 60000 - (int)ProcessSubStep.Elapsed.TotalMilliseconds) > 0)
				Thread.Sleep(Math.Min(35, tRemaining));
			ProcessSubStep.End();

			ProcessSubStep.Start($"Wait for >= {5} seconds of {Manometer.Name} stability");
			Manometer.WaitForStable(5);
			ProcessSubStep.End();

			ProcessSubStep.Start($"Average the pressure over 30 seconds");
			var value = Manometer.WaitForAverage(30) / Kelvins;
			ProcessSubStep.End();

			// Measurement units are Torr/K to compensate for pressure drift due to
			// temperature changes.
			return value;	
		}

		double[][] measureExpansions(int repeats = 5)
		{
			double[][] obs = daa(Expansions.Count + 1, repeats);   // observations

			ProcessStep.Start("Measure expansions");

			var sb = new StringBuilder();
			string vol0 = GasSupply.Destination.Name.Replace("_", "..");
			sb.Append(vol0);
			foreach (var expansion in Expansions)
				sb.Append($"\t+{expansion.Chamber.Name}");
			Log.WriteLine();
			Log.Record(sb.ToString());

			int ob = 0;
			for (int repeat = 0; repeat < repeats; repeat++)
			{
				admitGas();
				obs[ob = 0][repeat] = measure(null);

				foreach (var expansion in Expansions)
					obs[++ob][repeat] = measure(expansion);
				int n = ob;

				sb = new StringBuilder($"{obs[ob = 0][repeat]:0.00000}");
				while (ob < n)
					sb.Append($"\t{obs[++ob][repeat]:0.00000}");
				Log.Record(sb.ToString());
			}

			ProcessStep.End();

			ProcessStep.Start("Open line");
			openLine();
			ProcessStep.End();

			return obs;
		}

		void UpdateChamberVolume(IChamber chamber, double milliLiters)
        {
			var prior = chamber.MilliLiters;
			chamber.MilliLiters = milliLiters;
			Log.Record($"{chamber.Name} (mL): {prior} => {chamber.MilliLiters}");
		}

		void UpdateExpansionVolumes(double[][] obs)
		{
			double v0 = GasSupply.Destination.MilliLiters;

			int ob = 0;
			foreach (var expansion in Expansions)
			{
				var chamber = expansion.Chamber;
				var measuredVolume = v1(v0, obs[ob], obs[++ob]);

				// Chamber.MilliLiters is the chamber's volume when
				// its valve is closed. The following correction is
				// needed because the measurement is made with the valve
				// opened (which is done to avoid complexities caused by
				// multiple valves closing in series, with each
				// one potentially increasing the pressure in only
				// a portion of the relevant volumes).
				var closedVolume = measuredVolume;
				if (expansion.ValveList is List<IValve> valves && valves.Any())
					closedVolume -= valves[0].OpenedVolumeDelta;
				UpdateChamberVolume(chamber, closedVolume);
				v0 += measuredVolume;
			}
		}

		/// <summary>
		/// Finds the volumes for the Expansions chambers, based on
		/// the volume of the GasSupply's (initial) Destination.
		/// </summary>
		/// <param name="repeats"></param>
		void CalibrateExpansions(int repeats = 5)
		{
			UpdateExpansionVolumes(measureExpansions(repeats));
		}

		/// <summary>
		/// Finds and sets the volume of the GasSupply's (first) Destination
		/// Chamber, based on the (known) volume of the first expansion in 
		/// Expansions. This method is intended to be used with a GasSupply 
		/// having only one Destination Chamber. The known volume value must
		/// include the "downstream" headspace of the valve that connects
		/// it and any volume present when the valve in the opened position
		/// but absent in the closed position (such as a bellows which
		/// generally expands the volume when it is extended in the
		/// valve's opened position.
		/// </summary>
		/// <param name="repeats"></param>
		void CalibrateInitialVolume(int repeats = 5)
		{
			var obs = measureExpansions(repeats);
			UpdateChamberVolume(GasSupply.Destination.Chambers[0],
				Expansions[0].Chamber.MilliLiters / v1_v0(obs[0], obs[1]));
		}

		public void Calibrate(int repeats = 5)
		{
			if (ExpansionVolumeIsKnown)
				CalibrateInitialVolume(repeats);
			else
				CalibrateExpansions(repeats);
		}
	}

	public class VolumeExpansion : HacsComponent, IVolumeExpansion
	{
		#region HacsComponent

		[HacsConnect]
		protected virtual void Connect()
		{
			Chamber = Find<Chamber>(chamberName);
			ValveList = FindAll<IValve>(valveNames);
		}

		#endregion HacsComponent

		[JsonProperty("Chamber")]
		string ChamberName { get => Chamber?.Name; set => chamberName = value; }
		string chamberName;
		public IChamber Chamber { get => chamber; set => Ensure(ref chamber, value); }
		IChamber chamber;

		/// <summary>
		/// The first valve in this list joins Chamber to a known volume.
		/// Any additional valves are to be closed before opening the first.
		/// </summary>
		[JsonProperty("ValveList")]
		List<string> ValveNames { get => ValveList?.Names(); set => valveNames = value; }
		List<string> valveNames;
		public List<IValve> ValveList { get => valveList; set => Ensure(ref valveList, value); }
		List<IValve> valveList;
	}
}