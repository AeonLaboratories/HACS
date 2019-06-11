using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using HACS.Core;

namespace HACS.Components
{
	// high vacuum system pressure monitor
	public class HVSPressure : Component
	{
		public static new List<HVSPressure> List;
		public static new HVSPressure Find(string name)
		{ return List?.Find(x => x.Name == name); }

		public static implicit operator double(HVSPressure x)
		{ return x == null ? 0 : x.Pressure; }

		[XmlElement("HighPressureMeter")]
		public string HighPressureMeterName { get; set; }
		[XmlIgnore] Meter m_HP;					// for pressures > high vacuum

		[XmlElement("IonGauge")]
		public string IonGaugeName { get; set; }
		[XmlIgnore] public IonGauge IG;          // for pressures <= "high vacuum"

		public double pressure_VM_max_IG;       // max pressure to read exclusively from ion gauge
		public double pressure_VM_min_HP;       // min pressure to read exclusively from HP gauge
		public double pressure_VM_switchpoint;  // ion gauge on/off switchpoint pressure

		public double Pressure { get; set; }

		public HVSPressure() { }

		public HVSPressure(string name)
			: this()
		{ Name = name; }

		public override void Connect()
		{
			m_HP = Meter.Find(HighPressureMeterName);
			m_HP.StateChanged += Update;				// null check?
			IG = IonGauge.Find(IonGaugeName);
			IG.StateChanged += Update;                  // null check?
		}

		public override void Initialize() { Initialized = true; }

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


		public bool SignificantChange(double pFrom, double pTo)
		{
			if (pFrom < 0 || pTo < 0) return pFrom != pTo;

			double dp, dn = 0.95;   // positive, negative significant changes
			if (pFrom < 0.001)
				dp = 1.2;
			else
			{
				if (pFrom > 200) dn = 0.995;
				else if (pFrom > 1) dn = 0.99;
				else if (pFrom > 0.005) dn = 0.98;
				dp = 2 - dn;
			}

			double change = pTo - pFrom;
			if (change < 0)
				return -change > dn * pFrom;
			return change > dp * pFrom;
		}


		public override string ToString()
		{
			return Name + ": " + Pressure.ToString("0.00e0") + "\t" +
					IG.Value.ToString("0.00e0") + "\t" +
					m_HP.Value.ToString("0.00e0"); ;
		}

	}
}
