using System.Collections.Generic;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;
using System;
using Newtonsoft.Json;

namespace HACS.Components
{
	public class OnOffDevice : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<OnOffDevice> List = new List<OnOffDevice>();
		public static new OnOffDevice Find(string name) { return List.Find(x => x?.Name == name); }

		protected void Connect()
        {
            Controller?.Connect(this);
        }

        protected void Initialize()
        {
            IsOn = IsReallyOn;
            if (IsOn) ForceOn();
            else ForceOff();
        }

		public OnOffDevice()
		{
			List.Add(this);
			OnConnect += Connect;
			OnInitialize += Initialize;
		}

		#endregion Component Implementation


		// Report Response:
		// CH S
		// ## #
		public static string ReportHeader = "CH S Error\r\n";
		public static int ReportLength = ReportHeader.Length;   // line terminator included

		[JsonProperty]
		public HacsComponent<SwitchBank> ControllerRef { get; set; }
		protected SwitchBank Controller => ControllerRef?.Component;
		[JsonProperty]
		public int Channel { get; set; }

		/// <summary>
		/// True if the switchbank has turned the device on (or false if off), 
		/// except during system startup,  when the returned value indicates whether it 
		/// is supposed to be on, instead.
		/// </summary>
		[XmlIgnore] public bool IsReallyOn
		{
			get { return Initialized ? _IsReallyOn : IsOn; }
			set
			{
				if (Initialized)
					_IsReallyOn = value;
				else
					IsOn = value;
				sw.Restart();
			}
		}
		bool _IsReallyOn = false;									// device state

		/// <summary>
		/// Whether the device is supposed to be on or off, even if the
		/// switchbank hasn't actually flipped the switch yet.
		/// </summary>
		[JsonProperty]
		public bool IsOn { get; set; }		// target state

		[XmlIgnore] public Stopwatch sw = new Stopwatch();
		public long MillisecondsOn => IsReallyOn ? MillisecondsInState : 0;
		public long MillisecondsOff => IsReallyOn ? 0 : MillisecondsInState;
        public long MillisecondsInState => sw.ElapsedMilliseconds;

		[XmlIgnore] public bool ReportValid;
		[XmlIgnore] public int Errors;
		[XmlIgnore] public string Report
		{
			get { return _Report; }
			set
			{
				_Report = value;
				ReportValid = InterpretReport();
				if (ReportValid)
					StateChanged?.Invoke();
			}
		}
		string _Report;

		public bool InterpretReport()
		{
			try
			{
				// parse the report values
				//           1         2         3         4         5         6
				// 0123456789012345678901234567890123456789012345678901234567890
				// CH S Error
				// ## # #####
				int rChannel = int.Parse(_Report.Substring(0, 2));	// also parsed by Controller
				bool rState = int.Parse(_Report.Substring(3, 1)) == 1;
				int rErrors = int.Parse(_Report.Substring(5, 5));

				// parsing succeeded
				if (rChannel != Channel) return false;
				IsReallyOn = rState;
				Errors = rErrors;

				return true;
			}
			catch { return false; }
		}

		void ForceOn()
		{
			IsOn = true;
            Controller.RequestService(this);
            StateChanged?.Invoke();
        }

        void ForceOff()
		{
			IsOn = false;
            Controller.RequestService(this);
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Turn on the device if on is true, otherwise turn it off.
        /// </summary>
        /// <param name="on">true to turn the device On, false to turn it Off</param>
        public void TurnOnOff(bool on) { if (on) TurnOn(); else TurnOff(); }

		public void TurnOn() { if (!IsReallyOn) ForceOn(); }

		public void TurnOff() { if (IsReallyOn) ForceOff(); }

		public override string ToString()
		{
			return $"{Name} ({Controller.Name}:{Channel}): {(IsReallyOn ? "On" : "Off")}";
		}
	}
}
