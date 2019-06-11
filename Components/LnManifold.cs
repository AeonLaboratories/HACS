using HACS.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public class LnManifold : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<LnManifold> List = new List<LnManifold>();
		public static new LnManifold Find(string name) { return List.Find(x => x?.Name == name); }

		protected void PreStop()
		{
			IsActive = false;
		}

		public LnManifold()
		{
			List.Add(this);
			OnPreStop += PreStop;
		}

		#endregion Component Implementation

		public bool OverflowDetected => (OverflowSensor?.Temperature ?? 25) < 0;

		bool _IsActive = false;

		[JsonProperty]
		public bool IsActive
		{
			get { return _IsActive; }
			set { _IsActive = value; Update(); }
		}

		[JsonProperty]
		public bool KeepActive { get; set; }

		[JsonProperty]
		public HacsComponent<OnOffDevice> LNSupplyRef { get; set; }
        [XmlIgnore] public OnOffDevice LNSupply => LNSupplyRef?.Component;

		[JsonProperty]
		public HacsComponent<TempSensor> LevelSensorRef { get; set; }
		[XmlIgnore] public TempSensor LevelSensor => LevelSensorRef?.Component;

		[JsonProperty]
		public HacsComponent<TempSensor> OverflowSensorRef { get; set; }
		[XmlIgnore] public TempSensor OverflowSensor => OverflowSensorRef?.Component;

		[JsonProperty]
        public double TargetTemp { get; set; }		// LN stops when LevelSensor <= this temperature
		[JsonProperty]
		public double FillTrigger { get; set; }

        protected bool WarmStart = true;
        Stopwatch sw = new Stopwatch();
		[JsonProperty]
		public int SecondsSlowToFill { get; set; }
		public int SecondsFilling => (int)sw.Elapsed.TotalSeconds;
		public bool SlowToFill
		{
			get
			{
				if (SecondsFilling > SecondsSlowToFill * ( WarmStart ? 2 : 1))
				{
					sw.Restart();
					return true;
				}
				return false;
			}
		}

		public void ForceFill()
		{
			if (!LNSupply.IsReallyOn)
			{
				IsActive = true;
				startLN();
			}
		}

		void startLN()
		{
            WarmStart = LevelSensor.Temperature > -100;

			LNSupply.TurnOn();
			sw.Restart();
		}

		void stopLN()
		{
			LNSupply.TurnOff();
			sw.Reset();
		}

		public void Update()
		{
			if (!Initialized) return;
			if (IsActive)
			{
				if (LNSupply.IsReallyOn)
				{
					if (OverflowDetected || LevelSensor.Temperature <= TargetTemp)
						stopLN();
				}
				else
				{
					if (LevelSensor.Temperature > TargetTemp + FillTrigger)
						startLN();
				}
			}
			else if (LNSupply.IsReallyOn)
				stopLN();
		}

		public override string ToString()
		{
			return $"{Name}: {(IsActive ? "Active" : "Not Active")} "+
                $"{LevelSensor?.Temperature} {(LNSupply?.IsReallyOn ?? false ? "On" : "Off")}";
		}
	}
}
