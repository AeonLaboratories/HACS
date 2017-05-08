using HACS.Core;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public class Heater : Component
	{
		public static new List<Heater> List;
		public static new Heater Find(string name)
		{ return List?.Find(x => x.Name == name); }

		public enum Devices { CFFurnace, VTT }
		public enum Modes { Off, Manual, Auto }
		public enum Error
		{
			None = 0,			// No Error
			PV = 1,				// PV out of range
			Channel = 2,		// invalid heater number
			Command = 4,		// unrecognized command from rs232
			Setpoint= 8,		// setpoint out of range
			CO = 16,			// control output power level out of range
			DataLog = 32,		// datalogging time interval out of range
			BufOvfl = 64,		// RS232 input buffer overflow
			TCChannel = 128,	// invalid thermocouple selected
			TCType = 256,		// invalid thermocouple type
			DevType = 512,		// invalid device type
			CRC = 1024,			// RS232 CRC error
			ADC = 2048,			// adc out of range
			COMAX = 4096,		// control output limit out of range
			NoTC = 8092			// PID commanded on heater with no thermocouple
		}

		// Heater Report
		//           1         2         3         4
		// 01234567890123456789012345678901234567890123456
		// C DM TCT _POWER POWMAX SETP __TEMP ___CJT Error
		// # ## ### ###.## ###.## #### ####.# ####.# #####
		public static string ReportHeader = "C DM TCT _POWER POWMAX SETP __TEMP ___CJT Error\r\n";
		public static int ReportLength = ReportHeader.Length;    // line terminator included
		[XmlIgnore] public int ReportsReceived { get; private set; }

		string _Report;
		[XmlIgnore]
		public string Report
		{
			get { return _Report; }
			set
			{
				_Report = value;
				interpretReport();
				ReportsReceived++;
				Update();
				StateChanged?.Invoke();
			}
		}

		[XmlIgnore] public Action StateChanged;

		[XmlElement("Controller")]
		public string ControllerName { get; set; }
		ThermalController Controller;
		
		public int Channel { get; set; }

		public HeaterConfig Target { get; set; }

		#region Device State

		[XmlIgnore] public Heater.Devices DeviceType { get; private set; }
		[XmlIgnore] public Heater.Modes Mode { get; private set; }

		[XmlIgnore] public double PowerMax { get; private set; }
		[XmlIgnore] public double PowerLevel { get; private set; }
		[XmlIgnore] public int Setpoint { get; private set; }

		[XmlIgnore] public double Temperature { get; private set; }

		[XmlIgnore] public int TCChannel { get; private set; }
		[XmlIgnore] public ThermocoupleTypes TCType { get; private set; }

		[XmlIgnore] public double MuxTemperature { get; private set; }
		[XmlIgnore] public int Errors { get; private set; }

		public bool IsOn { get { return Mode != Modes.Off; } }

		#endregion Device State

		public Heater() { }

		public override void Connect()
		{
			ThermalController c = ThermalController.Find(ControllerName);
			Connect(c);
		}

		public void Connect(ThermalController c)
		{
			if (Controller != c)
			{
				Controller = c;

				if (Controller != null)
					Controller.Connect(this);
			}
		}

		public override void Initialize()
		{
			Initialized = true;
		}

		bool interpretReport()
		{
			try
			{
				//           1         2         3         4
				// 01234567890123456789012345678901234567890123456
				// C DM TCT _POWER POWMAX SETP __TEMP ___CJT Error
				// # ## ### ###.## ###.## #### ####.# ####.# #####
				DeviceType = (Heater.Devices)int.Parse(_Report.Substring(2, 1));
				Mode = (Heater.Modes)int.Parse(_Report.Substring(3, 1));
				PowerLevel = double.Parse(_Report.Substring(9, 6));
				PowerMax = double.Parse(_Report.Substring(16, 6));
				Setpoint = int.Parse(_Report.Substring(23, 4));
				Temperature = double.Parse(_Report.Substring(28, 6));
				TCChannel = int.Parse(_Report.Substring(5, 2));
				TCType = (ThermocoupleTypes)int.Parse(_Report.Substring(7, 1));
				MuxTemperature = double.Parse(_Report.Substring(35, 6));
				Errors = int.Parse(_Report.Substring(42, 5));
				return true;
			}
			catch { return false; }
		}

		public void SetDevice(Heater.Devices deviceType)
		{
			Target.DeviceType = deviceType;
			Update();
		}

		public void SelectThermocouple(int tCChannel)
		{
			Target.TCChannel = tCChannel;
			Update();
		}

		public void SetThermocoupleType(ThermocoupleTypes tcType)
		{
			Target.TCType = tcType;
			Update();
		}

		public void SetPowerMax(int powerMax)
		{
			Target.PowerMax = powerMax;
			Update();
		}

		public void SetSetpoint(int setpoint)
		{
			Target.Setpoint = setpoint;
			Update();
		}

		public void TurnOn()
		{
			Target.On = true;
			Update();
		}

		public void TurnOn(int setpoint)
		{
			Target.Setpoint = setpoint;
			Target.On = true;
			Update();
		}

		public void TurnOff()
		{
			Target.On = false;
			Update();
		}

		public void Auto()
		{
			Target.ManualMode = false;
			Update();
		}

		public void Manual()
		{
			Target.ManualMode = true;
			Update();
		}

		public void Manual(double powerLevel)
		{
			Target.Setpoint = powerLevel;
			Target.ManualMode = true;
			Update();
		}

		public void Hold()
		{
			Manual(PowerLevel);
		}

		public void Update()
		{
			if (!Initialized || ReportsReceived == 0) return;

			if (DeviceType != Target.DeviceType)
				Controller.Command(String.Format("n{0:0} d{1:0}", Channel, (int)Target.DeviceType));
			else if (TCChannel != Target.TCChannel)
				Controller.Command(String.Format("n{0:0} tc{1:0}", Channel, Target.TCChannel));
			else if (TCType != Target.TCType)
				Controller.Command(String.Format("tn{0:0} tt{1:0}", TCChannel, (int)Target.TCType));
			else if (PowerMax != Target.PowerMax)
				Controller.Command(String.Format("n{0:0} x{1:0.00}", Channel, Target.PowerMax));
			else if (!Target.On && Mode != Modes.Off)
				Controller.Command(String.Format("n{0:0} 0", Channel));
			else if (Target.On && Target.ManualMode && (Mode != Modes.Manual || PowerLevel != Target.Setpoint))
				Controller.Command(String.Format("n{0:0} m{1:0.00}", Channel, Target.Setpoint));
			else if (!Target.ManualMode && Setpoint != (int)Target.Setpoint)
				Controller.Command(String.Format("n{0:0} s{1:0}", Channel, (int)Target.Setpoint));
			else if (!Target.ManualMode && Target.On && Mode != Modes.Auto)
				Controller.Command(String.Format("n{0:0} a", Channel));
			else
				return;

			Controller.Command(String.Format("n{0:0} r", Channel));
		}

		public override string ToString()
		{
			return Name + ":\r\n" +
				Utility.IndentLines(
					ReportHeader +
					Report
				);
		}
	}

	public class HeaterConfig
	{
		public Heater.Devices DeviceType { get; set; }
		public int TCChannel { get; set; }
		public ThermocoupleTypes TCType { get; set; }
		public double PowerMax { get; set; }

		public double Setpoint { get; set; }
		public bool On { get; set; }
		public bool ManualMode { get; set; }
	}
}
