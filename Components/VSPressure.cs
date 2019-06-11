using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using HACS.Core;
using Newtonsoft.Json;

namespace HACS.Components
{
	// vacuum system pressure monitor
	public class VSPressure : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<VSPressure> List = new List<VSPressure>();
		public static new VSPressure Find(string name) { return List.Find(x => x?.Name == name); }

		protected void Connect()
        {
            if (m_HP != null) m_HP.StateChanged += Update;
            if (IG != null) IG.StateChanged += Update;
        }

		public VSPressure()
		{
			List.Add(this);
			OnConnect += Connect;
		}

		#endregion Component Implementation


		public static implicit operator double(VSPressure x)
		{ return x?.Pressure ?? 0; }

		// for pressures > high vacuum
		[JsonProperty]
		public HacsComponent<Meter> HighPressureMeterRef { get; set; }
        public Meter m_HP => HighPressureMeterRef?.Component;

		// for pressures <= "high vacuum"
		[JsonProperty]
		public HacsComponent<IonGauge> IonGaugeRef { get; set; }
        public IonGauge IG => IonGaugeRef?.Component;

		[JsonProperty]
		public double pressure_VM_max_IG;       // max pressure to read exclusively from ion gauge
		[JsonProperty]
		public double pressure_VM_min_HP;       // min pressure to read exclusively from HP gauge
		[JsonProperty]
		public double pressure_VM_switchpoint;  // ion gauge on/off switchpoint pressure

		[JsonProperty]
		public double Pressure { get; set; }

        // not a ComponentUpdate operations
		// triggered by change in either p_HP or p_IG; might be called twice for a single DAQ read...
		public void Update()
		{
			if (!Initialized) return;

			double pressure;
			double pHP = Math.Max(m_HP, m_HP.Sensitivity);
			double pIG = Math.Max(IG, IG.Sensitivity);

			if (pHP > pressure_VM_min_HP || !IG.Valid)
				pressure = pHP;
			else if (pIG < pressure_VM_max_IG)
				pressure = pIG;
			else if (pIG > pHP)
				pressure = pHP;
			else    // pressure_VM_max_IG <= pIG <= pHP <= pressure_VM_min_HP
			{
				// high pressure reading weight coefficient
				double whp = (pHP - pressure_VM_max_IG) / (pressure_VM_min_HP - pressure_VM_max_IG);
				pressure = whp * pHP + (1 - whp) * pIG;
			}

			if (pressure < 0) pressure = 0;         // this should never happen

			double oldPressure = Pressure;
			Pressure = pressure;

			if (SignificantChange(oldPressure, pressure))
				StateChanged?.Invoke();
		}

        // TODO: this should be static
		public bool SignificantChange(double pFrom, double pTo)
		{
			if (pFrom <= 0 || pTo <= 0) return pFrom != pTo;

            double change = pTo - pFrom;
            double scale;         // which defines the scale, pFrom or pTo?
            if (change < 0)
            {
                change = -change;
                scale = pTo;
            }
            else
                scale = pFrom;

            double significant;

            // TODO: make this a list of key-value pairs? move magic numbers into settings file
            // is this whole idea dumb? Is there a better way to charaterize a signficant difference?
            if (scale >= 10)
                significant = 1;                // 1 Torr (10% at 10; 1% at 100; 0.1% at 1000)
            else if (scale >= 0.01)
                significant = 0.05 * scale;     // 5%
            else
                significant = 0.02 * scale;     // 2%

            return change >= significant;
		}


        public override string ToString()
        {
            return $"{Name} {Pressure:0.00e0} IG:{IG.Value:0.00e0}({(IG.IsOn ? "on" : "off")}) HP:{m_HP.Value:0.00e0}";
        }
	}
}
