using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public class TempSensor : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<TempSensor> List = new List<TempSensor>();
		public static new TempSensor Find(string name) { return List.Find(x => x?.Name == name); }

		protected void Connect()
        {
            Controller?.Connect(this);
        }

		public TempSensor()
		{
			List.Add(this);
			OnConnect += Connect;
		}

		#endregion Component Implementation


		// Thermocouple Report
		//           1         2         3         4         5         6
		// 0123456789012345678901234567890123456789012345678901234567890
		// CH T __TEMP ___CJT Error
		// ## # ####.# ####.# #####
		public static string ReportHeader = "CH T __TEMP ___CJT Error\r\n";
		public static int ReportLength = ReportHeader.Length;	// line terminator included
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

		[JsonProperty]
		public HacsComponent<ThermalController> ControllerRef { get; set; }
		public ThermalController Controller => ControllerRef?.Component;
		
		[JsonProperty]
		public int TCChannel { get; set; }

		[JsonProperty]
		public TempSensorConfig Target { get; set; }

		#region Device State

		[XmlIgnore] public double Temperature { get; private set; }

		[XmlIgnore] public ThermocoupleTypes TCType { get; private set; }

		[XmlIgnore] public double MuxTemperature { get; private set; }
		[XmlIgnore] public int Errors { get; private set; }

		#endregion Device State

		bool interpretReport()
		{
			try
			{
				// Thermocouple Report
				//           1         2         3         4         5         6
				// 0123456789012345678901234567890123456789012345678901234567890
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
			return $"{Name} ({Controller.Name}:{TCChannel}): {Temperature}\r\n" +
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
