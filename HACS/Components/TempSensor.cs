using HACS.Core;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public enum ThermocoupleTypes { None, TypeK, TypeT }

	public class TempSensor : Component
    {
		public static new List<TempSensor> List;
		public static new TempSensor Find(string name)
		{ return List?.Find(x => x.Name == name); }

		// Thermocouple Report
		//           1         2         3         4
		// 012345678901234567890123456789012345678901234
		// CH T __TEMP ___CJT Error
		// ## # ####.# ####.# #####
		public static string ReportHeader = "CH T __TEMP ___CJT Error\r\n";
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

		public int TCChannel { get; set; }

		public TempSensorConfig Target { get; set; }

		#region Device State

		[XmlIgnore] public double Temperature { get; private set; }

		[XmlIgnore] public ThermocoupleTypes TCType { get; private set; }

		[XmlIgnore] public double MuxTemperature { get; private set; }
		[XmlIgnore] public int Errors { get; private set; }

		#endregion Device State

		public TempSensor() { }

		public override void Connect()
		{
			ThermalController controller = ThermalController.Find(ControllerName);
			Connect(controller);
		}

		public void Connect(ThermalController controller)
		{
			if (Controller != controller)
			{
				Controller = controller;

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
				// Thermocouple Report
				//           1         2         3         4
				// 012345678901234567890123456789012345678901234
				// CH T __TEMP ___CJT Error
				// ## # ####.# ####.# #####
				TCType = (ThermocoupleTypes)int.Parse(_Report.Substring(3, 1));
				Temperature = double.Parse(_Report.Substring(5, 6));
				MuxTemperature = double.Parse(_Report.Substring(12, 6));
				Errors = int.Parse(_Report.Substring(19, 5));
				return true;
			}
			catch { return false; }
		}

		public void SetThermocoupleType(ThermocoupleTypes tcType)
		{
			Target.TCType = tcType;
			Update();
		}

		public void Update()
		{
			if (!Initialized || ReportsReceived == 0) return;

			if (TCType != Target.TCType)
				Controller.Command(String.Format("tn{0:0} tt{1:0}", TCChannel, (int)(Target.TCType)));
			else
				return;

			Controller.Command(String.Format("tn {0:0} r", TCChannel));
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

	public class TempSensorConfig
	{
		public ThermocoupleTypes TCType { get; set; }
	}
}
