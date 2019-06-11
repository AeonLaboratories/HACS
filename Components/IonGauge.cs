using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HACS.Components
{
	public class IonGauge : Meter
	{
		#region Component Implementation

		public static readonly new List<IonGauge> List = new List<IonGauge>();
		public static new IonGauge Find(string name) { return List.Find(x => x?.Name == name); }

		protected void PreStop()
		{
			Disable();
		}

		public IonGauge()
		{
			List.Add(this);
			OnPreStop += PreStop;
		}

		#endregion Component Implementation

		static readonly bool On = true, Off = false;
        
		[JsonProperty]
		public HacsComponent<DigitalOutput> IonGaugeEnableRef { get; set; }
		public DigitalOutput IonGaugeEnable => IonGaugeEnableRef?.Component;

		public bool IsOn => IonGaugeEnable?.IsOn ?? false;
		public long MillisecondsOn => IonGaugeEnable?.MillisecondsOn ?? 0;
		public long MillisecondsOff => IonGaugeEnable?.MillisecondsOff ?? 0;
		public long MillisecondsInState => IonGaugeEnable?.MillisecondsInState ?? 0;

		[JsonProperty] public int milliseconds_stabilize;
		[JsonProperty] public int milliseconds_min_off;

		public bool Valid => MillisecondsOn >= milliseconds_stabilize;
		public double Pressure => Value;

		public void Enable()
		{
			if (!IonGaugeEnable.IsOn)
			{
				IonGaugeEnable.SetOutput(On);
				StateChanged?.Invoke();
			}
		}

		public void Disable()
		{
			if (IonGaugeEnable.IsOn)
			{
				IonGaugeEnable.SetOutput(Off);
				StateChanged?.Invoke();
			}
		}
	}
}
