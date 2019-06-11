using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Utilities;
using System.Xml.Serialization;
using System.Text;
using HACS.Core;

namespace HACS.Components
{
	public class Eurotherm818Furnace : TubeFurnace
	{
		#region Component Implementation

		public static readonly new List<Eurotherm818Furnace> List = new List<Eurotherm818Furnace>();
		public static new Eurotherm818Furnace Find(string name) { return List.Find(x => x?.Name == name); }

		protected override void Initialize()
        {
            if (LogEverything) Log.Record("Initializing...");

            ResponseProcessor = GetResponse;

            ResponseThread = new Thread(ProcessResponses)
            {
                Name = $"{Name} ProcessResponses",
                IsBackground = true
            };
            ResponseThread.Start();

            if (LogEverything) Log.Record("Setting state StateChanged handlers.");

            TargetState.StateChanged = targetStateChanged;
            TargetState.DeviceState = DeviceState;
            DeviceState.StateChanged = deviceStateChanged;


            if (LogEverything) Log.Record("Starting stateThread");

            stateThread = new Thread(ManageState)
            {
                Name = $"{Name} ManageState",
                IsBackground = true
            };
            stateThread.Start();

            TargetState.StateStopwatch.Restart();

            if (LogEverything) Log.Record("Initialization complete");
			base.Initialize();
		}

		public Eurotherm818Furnace()
		{
			List.Add(this);
		}

		#endregion Component Implementation


		#region Physical device constants

		public enum Parameters
        {
            Setpoint = 0,
            WorkingSetpoint = 1,
            Temperature = 2,
            OutputPower = 3,
            OutputPowerLimit = 4
        }
        public static string[] ParameterMnemonics = { "SL", "SP", "PV", "OP", "HO" };
        public static int ParameterCount = ParameterMnemonics.Length;

        #endregion

        public string InstrumentID { get; set; } = "0000";

		#region Class Interface Values - Check the device state using these properties and methods
		//
		// These properties expose the state of the physical
		// device to the class user.
		//

        public override double Setpoint => TargetState.Setpoint;
        public override double Temperature => DeviceState.Temperature;  // PV (read-only)
        public int WorkingSetpoint => TargetState.WorkingSetpoint;
        public int TFSetpoint => DeviceState.Setpoint;                  // SL
        public int TFWorkingSetpoint => DeviceState.WorkingSetpoint;    // SP (read-only)
        public int OutputPower => DeviceState.OutputPower;              // OP
        public int OutputPowerLimit => DeviceState.OutputPowerLimit;    // HO

		bool DeviceOn => DeviceState.OutputPowerLimit > 0;

		public override bool UseTimeLimit { get; set; }
		public override double TimeLimit { get; set; }

		/// <summary>
		/// True if the furnace is on, except during system startup, 
		/// when the returned value indicates whether the furnace 
		/// is supposed to be on, instead.
		/// </summary>
		public override bool IsOn
		{
			get { return Initialized ? DeviceOn : _IsOn; }
			set { if (!Initialized) _IsOn = value; }
		}
		bool _IsOn;


		public override string ToString()
		{
            return $"{Name}: \r\n" + 
                Utility.IndentLines(
                    Utility.byte_string(Report) + "\r\n" +
                    $"SP: {Setpoint}" +
                    $" WSP: {WorkingSetpoint}" +
                    $" TFSL: {TFSetpoint}" +
                    $" TFSP: {TFWorkingSetpoint}" +
                    $" PV: {Temperature}" +
                    $" OP: {OutputPower}" +
                    $" HO: {OutputPowerLimit}" +
                    $" RR: {TargetState.RampRate}"
                );
		}

        #endregion

        #region Class Interface Methods -- Control the device using these functions
        //
        // These methods expose the functionality of the physical
        // device to the class user.
        //

        /// <summary>
        /// Turns off the furnace.
        /// </summary>
        public override void TurnOff()
		{
            TargetState.OutputPowerLimit = 0;
        }

		/// <summary>
		/// Sets the furnace temperature and turns it on.
		/// If the furnace is on when the specified time elapses, it is turned off.
		/// </summary>
		/// <param name="setpoint">Desired furnace temperature (°C)</param>
		/// <param name="minutes">Maximum number of minutes to remain on</param>
		public override void TurnOn(double setpoint, double minutes)
		{
			TimeLimit = minutes;
			UseTimeLimit = true;
			TurnOn(setpoint);
		}

		/// <summary>
		/// Sets the furnace temperature and turns it on.
		/// </summary>
		/// <param name="setpoint">Desired furnace temperature (°C)</param>
		public override void TurnOn(double setpoint)
		{
			SetSetpoint(setpoint);
			TurnOn();
		}

		/// <summary>
		/// Turns the furnace on.
		/// </summary>
		public override void TurnOn()
		{
            TargetState.OutputPowerLimit = 100;
        }

		/// <summary>
		/// Sets the desired furnace temperature.
		/// </summary>
		/// <param name="setpoint"></param>
		public override void SetSetpoint(double setpoint)
		{
            TargetState.Setpoint = (int)setpoint;
        }

		/// <summary>
		/// Sets the Setpoint rate limit (deg/minute; 0 means no limit).
		/// This driver thereafter ramps the Setpoint
		/// to programmed levels at the given rate.
		/// </summary>
		/// <param name="degreesPerMinute"></param>
		public void SetRampRate(int degreesPerMinute)
		{
			TargetState.RampRate = degreesPerMinute;
		}

		#endregion



		#region main class

		public override double MinutesOn => IsOn ? MinutesInState : 0;
		public override double MinutesOff => !IsOn ? MinutesInState : 0;
		public override double MinutesInState => (int)TargetState.StateStopwatch.Elapsed.TotalMinutes;

		#endregion


		#region State Manager
		//
		// These functions control the physical device
		// to achieve the desired "TargetState" which is
		// (indirectly) defined by the user of this 
		// class. The TargetState properties are managed 
		// by the Class Interface Methods.
		//

		Thread stateThread;
		AutoResetEvent stateSignal = new AutoResetEvent(false);

		public Eurotherm818TargetState TargetState;
		[XmlIgnore] public Eurotherm818DeviceState DeviceState = new Eurotherm818DeviceState();

		void targetStateChanged()
        {
            stateSignal.Set();
            StateChanged?.Invoke();
        }

		void deviceStateChanged()
        {
            stateSignal.Set();
            StateChanged?.Invoke();
        }

        void ManageState()
		{
            int timeout = 3 * SerialDevice.MillisecondsBetweenMessages;
            bool signalReceived = false;
            try
            {
				while (true)
				{
                    if (!SerialDevice.Disconnected && SerialDevice.Idle)
                    {
                        // If it was InterpretReport() that ultimately triggered the
                        // stateSignal, ensure that it has finished (and released the 
                        // DeviceState) before continuing. For maximum thread safety, 
                        // DeviceState could be locked here for every access to it, but 
                        // this "trick" seems to be adequate so far.
                        lock (DeviceState) { /* just wait for it to be free */ }

                        if (!DeviceState.TemperatureValid)
                        {
                            GetTemperature();
                        }
                        else if (!DeviceState.OutputPowerLimitValid)
                        {
                            GetOutputPowerLimit();
                        }
                        else if (!DeviceState.SetpointValid)
                        {
                            GetSetpoint();
                        }
                        else if (UseTimeLimit && MinutesOn >= TimeLimit)
                        {
                            TurnOff();
                            SetOutputPowerLimit();
                            UseTimeLimit = false;
                        }
                        else if (TargetState.OutputPowerLimit == 0 && DeviceState.OutputPowerLimit != 0)
                        {
                            SetOutputPowerLimit();
                        }
                        else if (DeviceState.Setpoint != TargetState.WorkingSetpoint)
                        {
                            SetSetpoint();
                        }
                        else if (DeviceState.OutputPowerLimit != TargetState.OutputPowerLimit)
                        {
                            SetOutputPowerLimit();
                        }
                        else if (!signalReceived)
                        {
                            GetStatus();
                        }
                    }
                    signalReceived = stateSignal.WaitOne(timeout);
				}
			}
			catch (Exception e)
			{
				if (LogEverything)
					Log.Record("Exception in ManageState(): " + e.ToString());
				else
					Notice.Send(e.ToString());
			}
		}

        #endregion

        #region Controller commands

        #region Controller read commands
        //
        // Commands to retrieve information from the controller
        //
        void GetSetpoint() { GetParameter(Parameters.Setpoint); }
        void GetWorkingSetpoint() { GetParameter(Parameters.WorkingSetpoint); }
        void GetTemperature() { GetParameter(Parameters.Temperature); }
        void GetOutputPower() { GetParameter(Parameters.OutputPower); }
        void GetOutputPowerLimit() { GetParameter(Parameters.OutputPowerLimit); }

        // used when idle
        int paramToGet = 0;
		void GetStatus()
		{
            GetParameter((Parameters)paramToGet);
            ++paramToGet;
            if (paramToGet >= ParameterCount)
                paramToGet = 0;
		}

        public void GetParameter(Parameters param)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append((char)ASCIICodes.EOT);
            sb.Append(InstrumentID);
            sb.Append(ParameterMnemonics[(int)param]);
            sb.Append((char)ASCIICodes.ENQ);

            if (LogEverything)
            {
                Log.WriteLine("");
                Log.Record($"GetParameter {ParameterMnemonics[(int)param]}");
            }
            Command(sb.ToString());
        }

        #endregion

        #region Controller write commands
        //
        // These functions issue commands to change the physical device,
        // and check whether they worked.
        //

		void SetSetpoint()
		{
            DeviceState.SetpointValid = false;
            SetParameter(Parameters.Setpoint, TargetState.WorkingSetpoint);
		}

        void SetOutputPowerLimit()
        {
            DeviceState.OutputPowerLimitValid = false;
            SetParameter(Parameters.OutputPowerLimit, TargetState.OutputPowerLimit);
        }

        public void SetParameter(Parameters param, int value)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb2 = new StringBuilder();

            sb.Append((char)ASCIICodes.EOT);
            sb.Append(InstrumentID);
            sb.Append((char)ASCIICodes.STX);
            sb2.Append(ParameterMnemonics[(int)param]);
            sb2.Append($"{value:0.0}".PadLeft(6));
            sb2.Append((char)ASCIICodes.ETX);
            sb.Append(sb2);
            sb.Append((char)bcc(sb2.ToString()));

            if (LogEverything)
            {
                Log.WriteLine("");
                Log.Record($"SetParameter {ParameterMnemonics[(int)param]} to {value}");
            }
            Command(sb.ToString());
        }

        public byte bcc(string s)
        {
            byte bcc = 0;
            for (int i = 0; i < s.Length; i++)
                bcc ^= (byte)s[i];
            return bcc;
        }


		#endregion

        #endregion

        #region Controller responses

        [XmlIgnore] public override string Report
		{
			get { return _Report; }
			set
			{
				_Report = value;
                bool valid;
                lock (DeviceState)
                    valid = InterpretReport();
                if (!valid && LogEverything)
                    Log.Record("Couldn't interpret Report");
                StateChanged?.Invoke();         // if nothing else, the _Report is changed
			}
		}
		string _Report = "";

		public bool InterpretReport()
		{
			try
			{
                string report;
                lock (Report) report = Report;

                if (report.Length != 11)
                {
                    if (report[0] == (char)ASCIICodes.ACK)
                    {
                        if (LogEverything) Log.Record("ACK received");
                        DeviceState.Error = 0;
                        return true;
                    }
                    else
                    {
                        if (LogEverything) Log.Record("NAK or unrecognized response");
                        DeviceState.Error = 1;
                        return false;
                    }
                }

                if (bcc(report.Substring(1, report.Length - 2)) != report[report.Length - 1])
                {
                    if (LogEverything) Log.Record("BCC mismatch");
                    DeviceState.Error = 4;
                    return false;
                }

                string param = report.Substring(1, 2);
                if (!double.TryParse(report.Substring(3, 6), out double doubleValue))
                {
                    if (LogEverything) Log.Record($"Unrecognized parameter value: [{report.Substring(3, 6)}]");
                    DeviceState.Error = 16;
                    return false;
                }
                int value = (int)doubleValue;

                if (LogEverything) Log.Record($"Param [{param}] = [{value}]");

                if (param == ParameterMnemonics[(int)Parameters.Setpoint])
                {
                    if (LogEverything) Log.Record($"Setpoint received");
                    DeviceState.Setpoint = value;
                }
                else if (param == ParameterMnemonics[(int)Parameters.WorkingSetpoint])
                {
                    if (LogEverything) Log.Record($"Working Setpoint received");
                    DeviceState.WorkingSetpoint = value;
                }
                else if (param == ParameterMnemonics[(int)Parameters.Temperature])
                {
                    if (LogEverything) Log.Record($"Temperature received");
                    DeviceState.Temperature = value;
                }
                else if (param == ParameterMnemonics[(int)Parameters.OutputPower])
                {
                    if (LogEverything) Log.Record($"Output Power Level received");
                    DeviceState.OutputPower = value;
                }
                else if (param == ParameterMnemonics[(int)Parameters.OutputPowerLimit])
                {
                    if (LogEverything) Log.Record($"Output Power Limit received");
                    DeviceState.OutputPowerLimit = value;
                }
                else
                {
                    if (LogEverything) Log.Record($"Unrecognized parameter received");
                    DeviceState.Error = 8;
                    return false;
                }
				return true;
			}
			catch (Exception e)
			{
				if (LogEverything) Log.Record(e.ToString());
				return false; 
			}
		}

		#endregion

		#region Communications management

        public override bool Command(string cmd)
        {
            if (LogComms) Log.Record($"{Name} (out): {Utility.byte_string(cmd)}");
            return base.Command(cmd);
        }

        Thread ResponseThread;
        AutoResetEvent ResponseSignal = new AutoResetEvent(false);
        Queue<string> ResponseQ = new Queue<string>();
        void ProcessResponses()
        {
            string response;
            try
            {
                bool responseReceived = false;
                while (true)
                {
                    if (responseReceived)
                    {
                        lock (ResponseQ) response = ResponseQ.Dequeue();
                        if (LogComms) Log.Record($"{Name} (in): {Utility.byte_string(response)}");
                        Report = response;        // this assignment does stuff
                    }
                    // if () break;  // TODO: put code here to terminate the loop on shutdown
                    responseReceived = ResponseSignal.WaitOne(500);
                }
            }
            catch { }
        }

        // this runs in a SerialDevice thread
		void GetResponse(string s)
		{
            lock (ResponseQ) ResponseQ.Enqueue(s);
			ResponseSignal.Set();
		}

		#endregion

	}

	public class Eurotherm818TargetState
	{
		[XmlIgnore] public Action StateChanged;

        public Stopwatch StateStopwatch { get; set; } = new Stopwatch();

        // needed for Temperature ramping (WorkingSetpoint
        [XmlIgnore] public Eurotherm818DeviceState DeviceState;
        public int RampStartTemperature { get; set; }

        public int Setpoint
		{
			get { return _Setpoint; }
			set
			{
				if (value <= 0) _Setpoint = 1;
				else if (value > 1200) _Setpoint = 1200;
				else _Setpoint = value;

                if (OutputPowerLimit > 0)
                    StartRamp();

                StateChanged?.Invoke();
			}
		}
		int _Setpoint;

        public int OutputPowerLimit
        {
            get { return _OutputPowerLimit; }
            set
            {
                if (value <= 0)
                {
                    _OutputPowerLimit = 0;
                    StateStopwatch.Restart();
                }
                else
                {
                    if (value > 100)
                        _OutputPowerLimit = 100;
                    else
                        _OutputPowerLimit = value;

                    StartRamp();
                }
                StateChanged?.Invoke();
            }
        }
        int _OutputPowerLimit;

        /// <summary>
        /// Degrees C per minute. Default is 10.
        /// </summary>
        public int RampRate { get; set; } = 10;


        void StartRamp()
        {
            // this furnace thinks it's at 71 degrees (C) at room temperature
            if (DeviceState?.TemperatureValid ?? false)
                RampStartTemperature = Math.Max(75, DeviceState.Temperature);
            else
                RampStartTemperature = 75;
            StateStopwatch.Restart();
        }

        int workingSetpoint()
        {
            int dT = (int)Math.Round(StateStopwatch.Elapsed.TotalMinutes * Math.Abs(RampRate));
            if (Setpoint > RampStartTemperature)
                return Math.Min(Setpoint, RampStartTemperature + dT);
            else
                return Math.Max(Setpoint, RampStartTemperature - dT);
        }
        public int WorkingSetpoint
        {
            get
            {
                if (OutputPowerLimit <= 0)
                {
                    //return DeviceState.Setpoint;    // so there's nothing to do in stateManager
                    return Setpoint;                  // so the Device.Setpoint will match the Target.Setpoint
                }

                int wsp = workingSetpoint();

                // if the device temperature and working setpoint differ by "too much", refigure the ramp
                int toomuch = 3 * RampRate;
                if ((DeviceState?.TemperatureValid ?? false) && Math.Abs(DeviceState.Temperature - wsp) > toomuch)
                {
                    StartRamp();
                    wsp = workingSetpoint();
                }
                return wsp;
            }
        }
	}
    
    public class Eurotherm818DeviceState
	{
        public Action StateChanged;

        public int Error { get; set; }

		public int Setpoint
		{
			get { return _Setpoint; }
			set
			{
                int oldvalue = _Setpoint;
				_Setpoint = value;
				if (!SetpointValid || _Setpoint != oldvalue)
                    StateChanged?.Invoke();
                SetpointValid = true;
			}
		}
		int _Setpoint;
        public bool SetpointValid { get; set; } = false;
 
        public int Temperature
        {
			get { return _Temperature; }
			set
			{
                int oldvalue = _Temperature;
                _Temperature = value;
                if (!TemperatureValid || _Temperature != oldvalue)
                    StateChanged?.Invoke();
                TemperatureValid = true;
            }
        }
		int _Temperature = -1;
        public bool TemperatureValid { get; set; } = false;

        public int OutputPowerLimit
        {
            get { return _OutputPowerLimit; }
            set
            {
                int oldvalue = _OutputPowerLimit;
                _OutputPowerLimit = value;
                if (!OutputPowerLimitValid || _OutputPowerLimit != oldvalue)
                    StateChanged?.Invoke();
                OutputPowerLimitValid = true;
            }
        }
        int _OutputPowerLimit = -1;
        public bool OutputPowerLimitValid { get; set; } = false;

        public int WorkingSetpoint
        {
            get { return _WorkingSetpoint; }
            set
            {
                int oldvalue = _WorkingSetpoint;
                _WorkingSetpoint = value;
                if (_WorkingSetpoint != oldvalue)
                    StateChanged?.Invoke();
            }
        }
        int _WorkingSetpoint = -1;

        public int OutputPower
        {
            get { return _OutputPower; }
            set
            {
                int oldvalue = _OutputPower;
                _OutputPower = value;
                if (_OutputPower != oldvalue)
                    StateChanged?.Invoke();
            }
        }
        int _OutputPower = -1;
    }
}