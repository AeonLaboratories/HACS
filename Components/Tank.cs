using HACS.Core;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	// TODO: rename Tank to LNManifold
	public class Tank : Component
	{
        #region Component Implementation

		public static new Tank Find(string name)
		{ return List?.Find(x => x?.Name == name) as Tank; }

        public override void Connect()
        {
            LevelSensor = TempSensor.Find(LevelSensorName);
            LNSupply = OnOffDevice.Find(LNSupplyName);
            OverflowSensor = TempSensor.Find(OverflowSensorName);
        }

        #endregion Component Implementation


        bool _IsActive = false;
		public bool IsActive
		{
			get { return _IsActive; }
			set { _IsActive = value; Update(); }
		}

		public bool KeepActive { get; set; }

		[XmlElement("LNSupply")]
		public string LNSupplyName { get; set; }
		[XmlIgnore] public OnOffDevice LNSupply;

		[XmlElement("LevelSensor")]
		public string LevelSensorName { get; set; }
		[XmlIgnore] public TempSensor LevelSensor;

		[XmlElement("OverflowSensor")]
		public string OverflowSensorName { get; set; }
		[XmlIgnore] public TempSensor OverflowSensor;

		public double TargetTemp { get; set; }		// LN stops when LevelSensor <= this temperature
		public double FillTrigger { get; set; }

        protected bool WarmStart = true;
        Stopwatch sw = new Stopwatch();
		public int SecondsSlowToFill { get; set; }
		public int SecondsFilling { get { return (int)sw.Elapsed.TotalSeconds; } }
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
            WarmStart = LevelSensor.Temperature > 0;

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
					if (LevelSensor.Temperature <= TargetTemp)
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
			return Name + ": " + (IsActive ? "Active" : "Not Active") + "\r\n" +
				LevelSensor.ToString() + "\r\n" +
				LNSupply.ToString();
		}
	}
}
