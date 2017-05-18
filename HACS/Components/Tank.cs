﻿using HACS.Core;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public class Tank : Component
	{
		public static new List<Tank> List;
		public static new Tank Find(string name)
		{ return List?.Find(x => x.Name == name); }

		bool _IsActive = false;
		public bool IsActive
		{
			get { return _IsActive; }
			set { _IsActive = value; Update(); }
		}

		public bool KeepActive { get; set; }

		[XmlElement("LNSupply")]
		public string LNSupplyName { get; set; }
		[XmlIgnore] OnOffDevice LNSupply;

		[XmlElement("LevelSensor")]
		public string LevelSensorName { get; set; }
		[XmlIgnore] public TempSensor LevelSensor;

		[XmlElement("OverflowSensor")]
		public string OverflowSensorName { get; set; }
		[XmlIgnore] TempSensor OverflowSensor;

		public double TargetTemp { get; set; }		// LN stops when LevelSensor <= this temperature
		public double FillTrigger { get; set; }

		Stopwatch sw = new Stopwatch();
		public int SecondsSlowToFill { get; set; }
		public int SecondsFilling { get { return (int)sw.Elapsed.TotalSeconds; } }
		public bool SlowToFill
		{
			get
			{
				if (SecondsFilling > SecondsSlowToFill)
				{
					sw.Restart();
					return true;
				}
				return false;
			}
		}

		public Tank() { }

		public Tank(string name, int target, int trigger)
		{
			Name = name;
			TargetTemp = target;
			FillTrigger = trigger;
		}

		public override void Connect()
		{
			LevelSensor = TempSensor.Find(LevelSensorName);
			LNSupply = OnOffDevice.Find(LNSupplyName);
			OverflowSensor = TempSensor.Find(OverflowSensorName);
		}

		public void Connect(TempSensor levelSensor, OnOffDevice lnSupply, TempSensor overflowSensor)
		{
			LevelSensor = levelSensor;
			LNSupply = lnSupply;
			OverflowSensor = overflowSensor;
		}

		public void ForceFill()
		{
			if (!LNSupply.IsOn)
			{
				IsActive = true;
				startLN();
			}
		}

		void startLN()
		{
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
				if (LNSupply.IsOn)
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
			else if (LNSupply.IsOn)
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
