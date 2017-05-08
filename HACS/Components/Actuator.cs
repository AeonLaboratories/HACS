using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Utilities;
using HACS.Core;

namespace HACS.Components
{
	public class ActuatorAction
	{
		[XmlAttribute] public string Name { get; set; }
		public int CPW { get; set; }             // control pulse width
		public bool EnableLimit0 { get; set; }
		public bool EnableLimit1 { get; set; }
		public int CurrentLimit { get; set; }
		public double TimeLimit { get; set; }

		public ActuatorAction(string name, int cpw, bool l0, bool l1, int iLim, double tLim)
		{
			Name = name;
			CPW = cpw;
			EnableLimit0 = l0;
			EnableLimit1 = l1;
			CurrentLimit = iLim;
			TimeLimit = tLim;
		}

		public ActuatorAction() { }

		public ActuatorAction Clone
		{ get { return new ActuatorAction(Name, CPW, EnableLimit0, EnableLimit1, CurrentLimit, TimeLimit); } }

		public override string ToString()
		{
			return String.Format(Name + ": {0} {1} {2} {3} {4}",
				CPW, EnableLimit0, EnableLimit1, CurrentLimit, TimeLimit);
		}
	}

	public class ActuatorState
	{
		[XmlIgnore] public Action Changed;
		[XmlIgnore]
		public string Report
		{
			get { return _Report; }
			set
			{
				if (Active) // ignore reports when controller is not operating the actuator
				{
					_Report = value;
					ReportCount++;
					ReportValid = InterpretReport();
					if (ReportValid)
						Changed?.Invoke();
				}
			}
		}
		string _Report;

		[XmlIgnore] public bool Limit0Engaged { get; set; }
		[XmlIgnore] public bool Limit1Engaged { get; set; }
		[XmlIgnore] public int Current { get; set; }
		[XmlIgnore] public double Elapsed { get; set; }
		[XmlIgnore] public double ControllerVoltage { get; set; }
		[XmlIgnore] public int Errors { get; set; }
		/* errors
		_ErrorServoOutOfRange = (Errors & 1) > 0;
		_ErrorControlPulseWidthOutOfRange = (Errors & 2) > 0;
		_ErrorTimeLimitOutOfRange = (Errors & 4) > 0;
		_ErrorBothLimitSwitchesEngaged = (Errors & 8) > 0;
		_ErrorLowSpsVoltage = (Errors & 16) > 0 ?;
		_ErrorInvalidCommand = (Errors & 32) > 0 ?;
		_ErrorCurrentLimitOutOfRange = (Errors & 64) > 0;
		_ErrorInvalidStopLimit = (Errors & 128) > 0;
		_ErrorDataLoggingIntervalOutOfRange = (Errors & 256) > 0;
		_ErrorRS232InputBufferOverflow = (Errors & 512) > 0;
		*/

		[XmlIgnore] public int ReportCount = 0; // reports received since last Clear()
		[XmlIgnore] public bool ReportValid { get; set; }

		[XmlIgnore]
		public bool Active
		{
			get { return _Active; }
			set
			{
				if (value) Clear();
				_Active = value;
			}
		}
		bool _Active;       // whether this Actuator is currently being operated

		public int Channel { get; set; }
		public int CPW { get; set; }                 // control pulse width
		public bool CPEnabled { get; set; }          // control pulses enabled
		public bool Limit0Enabled { get; set; }
		public bool Limit1Enabled { get; set; }
		public int CurrentLimit { get; set; }
		public double TimeLimit { get; set; }

		public void Clear()
		{
			Current = 0;
			Elapsed = 0;
			ReportCount = 0;
			ReportValid = false;
		}

		// Use Stopped; do not use !InMotion, as that returns true if !ReportValid
		public bool InMotion
		{ get { return ReportValid && CPEnabled; } }

		// Use Stopped; do not use !InMotion, as that returns true if !ReportValid
		public bool Stopped
		{ get { return ReportValid && !CPEnabled; } }

		public bool PositionDetected
		{
			get
			{
				return ReportValid &&
					(Limit0Enabled && Limit0Engaged ||
					Limit1Enabled && Limit1Engaged);
			}
		}

		public bool CurrentLimitDetected
		{
			get
			{
				return ReportValid &&
					CurrentLimit > 0 && Current >= CurrentLimit;
			}
		}

		public bool TimeLimitDetected
		{
			get
			{
				return ReportValid &&
					TimeLimit > 0 && Elapsed >= TimeLimit;
			}
		}

		public bool MotionInhibited
		{
			get
			{
				return
					ReportValid &&
					(Limit0Enabled && Limit0Engaged ||
					Limit1Enabled && Limit1Engaged);
			}
		}

		public bool InterpretReport()
		{
			try
			{
				// parse the report values
				//           1         2         3         4         5         6
				// 0123456789012345678901234567890123456789012345678901234567890
				// SRV __CPW G L0 L1 ILIM ___I __TLIM __ELAP _____V Error
				// ### ##### # ## ## #### #### ###.## ###.## ##.### #####
				int rChannel = int.Parse(_Report.Substring(0, 3));    // also parsed by Controller
				int rCPW = int.Parse(_Report.Substring(4, 5));     // control pulse width, in microseconds
				bool rCPEnabled = int.Parse(_Report.Substring(10, 1)) == 1;
				bool rLimit0Enabled = int.Parse(_Report.Substring(12, 1)) == 1;
				bool rLimit0Engaged = int.Parse(_Report.Substring(13, 1)) == 1;
				bool rLimit1Enabled = int.Parse(_Report.Substring(15, 1)) == 1;
				bool rLimit1Engaged = int.Parse(_Report.Substring(16, 1)) == 1;
				int rCurrentLimit = int.Parse(_Report.Substring(18, 4));       // milliamps
				int rCurrent = int.Parse(_Report.Substring(23, 4));            // milliamps
				double rTimeLimit = double.Parse(_Report.Substring(28, 6));    // seconds
				double rElapsed = double.Parse(_Report.Substring(35, 6));      // seconds
				double rControllerVoltage = double.Parse(_Report.Substring(42, 6));   // volts
				int rErrors = int.Parse(_Report.Substring(49, 5));

				// parsing succeeded
				Channel = rChannel;
				CPW = rCPW;
				CPEnabled = rCPEnabled;
				Limit0Enabled = rLimit0Enabled;
				Limit0Engaged = rLimit0Engaged;
				Limit1Enabled = rLimit1Enabled;
				Limit1Engaged = rLimit1Engaged;
				CurrentLimit = rCurrentLimit;
				Current = rCurrent;
				TimeLimit = rTimeLimit;
				Elapsed = rElapsed;
				ControllerVoltage = rControllerVoltage;
				Errors = rErrors;

				return true;
			}
			catch { return false; }
		}

		public override string ToString()
		{
			return String.Format("({11} reports) Ch:{12} {0}:{1} L0:{2}/{3} L1:{4}/{5} I:{6}/{7} t:{8:0.00}/{9:0.00} {10}",
				CPW, CPEnabled ? 1 : 0,
				Limit0Engaged ? 1 : 0, Limit0Enabled ? 1 : 0,
				Limit1Engaged ? 1 : 0, Limit1Enabled ? 1 : 0,
				Current, CurrentLimit,
				Elapsed, TimeLimit,
				Errors,
				ReportCount,
				Channel);
		}

		public ActuatorState()
		{
			ReportValid = false;
			Active = false;
		}
	}

	public class Actuator : Component
	{
		public static new List<Actuator> List;
		public static new Actuator Find(string name)
		{ return List?.Find(x => x.Name == name); }

		// Report Response:
		// SRV __CPW G L0 L1 ILIM ___I __TLIM __ELAP _____V Error
		// ### ##### # ## ## #### #### ###.## ###.## ##.### #####
		public static string ReportHeader = "SRV __CPW G L0 L1 ILIM ___I __TLIM __ELAP _____V Error\r\n";
		public static int ReportLength = ReportHeader.Length;    // line terminator included

		[XmlIgnore] public ServoController Controller { get; set; }

		int _Channel;
		public int Channel
		{
			get { return _Channel; }
			set
			{
				_Channel = value;
				if (Controller != null)
					Controller.Connect(this, Channel);
			}
		}

		[XmlIgnore]
		public Action StateChanged;
		[XmlIgnore] public ActuatorState State = new ActuatorState();

		public List<ActuatorAction> Actions { get; set; }
		public ActuatorAction FindAction(string name)
		{ try { return Actions.Find(x => x.Name == name); } catch { return null; } }

		public void DoAction(ActuatorAction action)
		{
			lock (this) { PendingActions++; }
			Controller.EnQforService(this, action);
		}

		public void DoAction(string actionName)
		{
			DoAction(FindAction(actionName));
		}

		public void Select() { DoAction(""); }
		public void Stop() { Controller.Abort(); }

		[XmlIgnore] public int PendingActions = 0;
		[XmlIgnore]
		public ActuatorAction Action
		{
			get { return _Action; }
			set { _Action = ValidateAction(value); }
		}
		ActuatorAction _Action;

		public virtual ActuatorAction ValidateAction(ActuatorAction action)
		{
			return action;      // no validation
		}

		[XmlIgnore]
		public bool Active
		{
			get { return _Active; }
			set
			{
				if (!value)
					lock (this) { PendingActions--; }
				_Active = value;
				State.Active = value;
				State.Changed?.Invoke();
			}
		}
		bool _Active = false;       // whether this actuator is currently being operated

		// whether this actuator currently has any actions pending
		[XmlIgnore] public bool Idle { get { return PendingActions == 0; } }

		bool StateMatchesAction
		{
			get
			{
				if (Action == null)
					return true;
				else
					return
						State.CPW == Action.CPW &&
						State.Limit0Enabled == Action.EnableLimit0 &&
						State.Limit1Enabled == Action.EnableLimit1 &&
						State.CurrentLimit == Action.CurrentLimit &&
						State.TimeLimit == Action.TimeLimit;
			}
		}

		[XmlIgnore]
		public bool Configured
		{ get { return State.ReportValid && StateMatchesAction; } }

		[XmlIgnore]
		public bool PositionDetectable
		{
			get
			{
				return Action != null && (Action.EnableLimit0 || Action.EnableLimit1);
			}
		}

		[XmlIgnore]
		public bool MotionInhibited
		{ get { return State.MotionInhibited; } }

		[XmlIgnore]
		public bool InMotion
		{ get { return State.InMotion; } }

		[XmlIgnore]
		public bool Stopped
		{ get { return State.Stopped; } }

		[XmlIgnore]
		public bool PositionDetected
		{ get { return State.PositionDetected; } }

		[XmlIgnore]
		public bool CurrentLimitDetected
		{ get { return State.CurrentLimitDetected; } }

		[XmlIgnore]
		public bool TimeLimitDetected
		{ get { return State.TimeLimitDetected; } }

		[XmlIgnore]
		public bool ActionSucceeded
		{
			get
			{
				return Action == null ||
					Configured && (PositionDetected || CurrentLimitDetected || TimeLimitDetected);
			}
		}

		[XmlIgnore]
		public string Command
		{
			get
			{
				if (Action == null) return String.Format("n{0:0} s r", Channel);
				return String.Format("n{0:0} p{1:0} l{2:0} l{3:0} i{4:0} t{5:0.00} r",
					Channel, Action.CPW, Action.EnableLimit0 ? 10 : -10, Action.EnableLimit1 ? 11 : -11,
					Action.CurrentLimit, Action.TimeLimit);
			}
		}

		public Actuator() { Actions = new List<ActuatorAction>(); }

		public Actuator(string name, int channel) : this()
		{
			Name = name;
			Channel = channel;
		}

		public void Connect(ServoController sc)
		{
			if (Controller != sc)
			{
				Controller = sc;
				Controller.Connect(this);
			}
		}

		public override string ToString()
		{
			return Name + ":\r\n" +
				Utility.IndentLines(
					String.Format("Ch:{0} PendingActions:{1} Succeeded:{2}\r\n",
							Channel, PendingActions, ActionSucceeded) +
					State.ToString()
				);
		}
	}
}