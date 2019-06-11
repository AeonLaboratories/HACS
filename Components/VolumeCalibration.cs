using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public class VolumeCalibration : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<VolumeCalibration> List = new List<VolumeCalibration>();
		public static new VolumeCalibration Find(string name) { return List.Find(x => x?.Name == name); }

		public VolumeCalibration()
		{
			List.Add(this);
		}

		#endregion Component Implementation

		[JsonProperty]
		public HacsComponent<GasSupply> GasSupplyRef { get; set; }
        public GasSupply GasSupply => GasSupplyRef?.Component;
		[JsonProperty]
		public double pressure_calibration { get; set; }
		[JsonProperty]
		public int minutes_calibration { get; set; }

		[JsonProperty]
		public List<VolumeExpansion> Expansions { get; set; }

		// Set true to measure a volume by expanding into a Known Volume.
		// Normally false.
		[JsonProperty]
		public bool ExpansionVolumeIsKnown { get; set; }

		[XmlIgnore] public StepTracker ProcessStep { get; set; }
		[XmlIgnore] public StepTracker ProcessSubStep { get; set; }
		[XmlIgnore] public Action OpenLine { get; set; }
		[XmlIgnore] public double pressure_ok { get; set; }
		[XmlIgnore] public DynamicQuantity Measurement { get; set; }    // use ugCinMc
		[XmlIgnore] public HacsLog Log { get; set; }


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

			GasSupply.Destination.Isolation.Valves.ForEach(v =>
			{
				var vvs = VacuumSystem.List.Find(vs => vs.v_HighVacuum == v || vs.v_LowVacuum == v);
				if (vvs != null && !vsList.Exists(vs => vs == vvs))
					vsList.Add(vvs);
			});

            Expansions.ForEach(exp =>
            {
                exp.ValveList.Valves.ForEach(v =>
                {
                    var vvs = VacuumSystem.List.Find(vs => vs.v_HighVacuum == v || vs.v_LowVacuum == v);
                    if (vvs != null && !vsList.Exists(vs => vs == vvs))
                        vsList.Add(vvs);
                });
            });

            vsList.ForEach(vs => { vs.Evacuate(); });

			OpenLine();

			vsList.ForEach(vs => { vs.WaitForPressure(pressure_ok); });
			GasSupply.Destination.VacuumSystem.WaitForPressure(pressure_ok);
		}

		void admitGas()
		{
			ProcessSubStep.Start("Open line");
			openLine();
			ProcessSubStep.End();

			ProcessSubStep.Start("Admit calibration gas into initial volume");
			GasSupply.Destination.ClosePorts();
			GasSupply.Pressurize(pressure_calibration);
			ProcessSubStep.End();

			ProcessSubStep.Start("Evacuate unnecessary gas from supply path.");
			GasSupply.EvacuatePath(pressure_ok);
			ProcessSubStep.End();
		}

		double measure() { return measure(null); }

		double measure(VolumeExpansion expansion)
		{
			var valves = expansion?.ValveList?.Valves;
			if (valves != null && valves.Any())
			{
				ProcessSubStep.Start($"Expand gas into {expansion.ChamberRef} via {valves[0].Name}");
				expansion.ValveList.Close();
				valves[0].Open();
				ProcessSubStep.End();
			}

			ProcessSubStep.Start($"Wait a minimum of {minutes_calibration} minutes");
			int tRemaining;
			while ((tRemaining = minutes_calibration * 60000 - (int)ProcessSubStep.Elapsed.TotalMilliseconds) > 0)
				Thread.Sleep(Math.Min(35, tRemaining));
			ProcessSubStep.End();

			ProcessSubStep.Start($"Wait for >= {5} seconds of {Measurement.Name} stability");
			Measurement.WaitForStable(5);
			ProcessSubStep.End();

			return Measurement;
		}

		double[][] measureExpansions(int repeats = 5)
		{
			double[][] obs = daa(Expansions.Count + 1, repeats);   // observations

			ProcessStep.Start("Measure expansions");

			var sb = new StringBuilder();
			string vol0 = GasSupply.Destination.Name.Replace("_", "..");
			sb.Append(vol0);
			foreach (var expansion in Expansions)
				sb.Append($", +{expansion.ChamberRef}");
			Log.WriteLine();
			Log.Record(sb.ToString());

			int ob = 0;
			for (int repeat = 0; repeat < repeats; repeat++)
			{
				admitGas();
				obs[ob = 0][repeat] = measure();

				foreach (var expansion in Expansions)
					obs[++ob][repeat] = measure(expansion);
				int n = ob;

				sb = new StringBuilder($"{obs[ob = 0][repeat]:0.0}");
				while (ob < n)
					sb.Append($"\t{obs[++ob][repeat]:0.0}");
				Log.Record(sb.ToString());
			}

			ProcessStep.End();

			ProcessStep.Start("Open line");
			openLine();
			ProcessStep.End();

			return obs;
		}

		void UpdateExpansionVolumes(double[][] obs)
		{
			double v0 = GasSupply.Destination.MilliLiters;

			int ob = 0;
			foreach (var expansion in Expansions)
			{
				var chamber = expansion.Chamber;
				double prior = chamber.MilliLiters;
				chamber.MilliLiters = v1(v0, obs[ob], obs[++ob]);
				Log.Record($"{chamber.Name} (mL): {prior} => {chamber.MilliLiters}");
				v0 += chamber.MilliLiters;
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
		/// having only one Destination Chamber.
		/// </summary>
		/// <param name="repeats"></param>
		void CalibrateInitialVolume(int repeats = 5)
		{
			var obs = measureExpansions(repeats);
			GasSupply.Destination.Chambers[0].MilliLiters =
				Expansions[0].Chamber.MilliLiters / v1_v0(obs[0], obs[1]);
		}

		public void Calibrate(int repeats = 5)
		{
			if (ExpansionVolumeIsKnown)
				CalibrateInitialVolume(repeats);
			else
				CalibrateExpansions(repeats);
		}
	}


	public class VolumeExpansion : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<VolumeExpansion> List = new List<VolumeExpansion>();
		public static new VolumeExpansion Find(string name) { return List.Find(x => x?.Name == name); }

		protected void Connect()
		{
			if (ValveList != null) ValveList.Name = Name + ".ValveList";
		}

		public VolumeExpansion()
		{
			List.Add(this);
			OnConnect += Connect;
		}

		#endregion Component Implementation

		[JsonProperty]
		public HacsComponent<Chamber> ChamberRef { get; set; }
        public Chamber Chamber => ChamberRef?.Component;

		/// <summary>
		/// The first valve in this list joins Chamber to a known volume.
		/// Any additional valves are to be closed before opening the first.
		/// </summary>
		[JsonProperty]
		public ValveList ValveList { get; set; }
	}
}