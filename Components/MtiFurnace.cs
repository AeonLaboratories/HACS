using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Utilities;
using System.Xml.Serialization;
using System.Text;

namespace HACS.Components
{
	public class MtiFurnace : TubeFurnace
	{
		#region Component Implementation

		public static readonly new List<MtiFurnace> List = new List<MtiFurnace>();
		public static new MtiFurnace Find(string name) { return List.Find(x => x?.Name == name); }

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

		public MtiFurnace()
		{
			List.Add(this);
		}

		#endregion Component Implementation


		#region Physical device constants

		public enum Parameters { Setpoint = 0x1A, PowerMode = 0x15 }
        public enum MessageTypes { Read = 0x52, Write = 0x43 }
        public enum PowerModes { Hold = 0x04, Stop = 0x0C };

        #endregion

        public byte InstrumentID { get; set; } = 1;

		#region Class Interface Values - Check the device state using these properties and methods
		//
		// These properties expose the state of the physical
		// device to the class user.
		//

        public override double Setpoint => TargetState.Setpoint;
        public override double Temperature => DeviceState.Temperature;
        public int WorkingSetpoint => TargetState.WorkingSetpoint;
        public int TFSetpoint => DeviceState.Setpoint;
        public bool PowerEnabled => DeviceState.IsOn;

		bool DeviceOn => DeviceState.IsOn;

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
                    $" TFSP: {TFSetpoint}" +
                    $" PV: {Temperature}" +
                    $" On: {(PowerEnabled ? "Yes" : "No")}" +
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
            TargetState.IsOn = false;
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
            TargetState.IsOn = true;
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
		public override double MinutesInState => TargetState.StateStopwatch.Elapsed.TotalMinutes;

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

		public MtiTargetState TargetState;
		[XmlIgnore] public MtiDeviceState DeviceState = new MtiDeviceState();

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

                        if (!DeviceState.Valid)
                        {
                            GetStatus();
                        }
                        else if (UseTimeLimit && MinutesOn >= TimeLimit)
                        {
                            TurnOff();              // update TargetState
                            SetPowerEnabled();      // configure the Device to match
                            UseTimeLimit = false;
                        }
                        else if (DeviceState.Setpoint != TargetState.WorkingSetpoint)
                        {
                            SetSetpoint();
                        }
                        else if (DeviceState.IsOn != TargetState.IsOn)
                        {
                            SetPowerEnabled();
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
        Parameters Parameter = Parameters.Setpoint;     // default -- anything but PowerMode
        
		void GetStatus()
		{
            byte[] instruction = new byte[8];
            instruction[0] = (byte)(0x80 + InstrumentID);
            instruction[1] = instruction[0];
            instruction[2] = (byte)MessageTypes.Read;
            instruction[3] = (byte)Parameters.PowerMode;
            instruction[4] = 0;
            instruction[5] = 0;
            int ecc = (0x100 * instruction[3] + instruction[2] + InstrumentID) & 0xFFFF;
            instruction[6] = LSB(ecc);
            instruction[7] = MSB(ecc);

            if (LogEverything)
            {
                Log.WriteLine("");
                Log.Record("GetStatus()");
            }
            Parameter = Parameters.PowerMode;
            Command(Utility.ByteArrayToString(instruction));
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
            // Setpoint and temperature values are written and read as integers in tenths of a degree C
            SetParameter(Parameters.Setpoint, TargetState.WorkingSetpoint * 10);
		}

        void SetPowerEnabled()
        {
            DeviceState.PowerModeValid = false;
            int value = (int)(TargetState.IsOn ? PowerModes.Hold : PowerModes.Stop);
            SetParameter(Parameters.PowerMode, value);
        }

        public void SetParameter(Parameters param, int value)
        {
            byte[] instruction = new byte[8];
            instruction[0] = (byte)(0x80 + InstrumentID);
            instruction[1] = instruction[0];
            instruction[2] = (byte)MessageTypes.Write;
            instruction[3] = (byte)param;
            instruction[4] = LSB(value);
            instruction[5] = MSB(value);
            int ecc = (0x100 * instruction[3] + instruction[2] + value + InstrumentID) & 0xFFFF;
            instruction[6] = LSB(ecc);
            instruction[7] = MSB(ecc);

            if (LogEverything)
            {
                Log.WriteLine("");
                Log.Record($"SetParameter {param:X} to {value}");
            }
            Parameter = param;
            Command(Utility.ByteArrayToString(instruction));
        }

        #endregion

        byte LSB(int i) { return (byte)(i & 0xFF); }
        byte MSB(int i) { return (byte)((i >> 8) & 0xFF); }

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
                string s;
                lock (Report) s = Report;
                byte[] report = Encoding.GetEncoding("iso-8859-1").GetBytes(s);

                if (report.Length != 10)
                {
                    if (LogEverything) Log.Record("Unrecognized response");
                    DeviceState.Error = 1;
                    return false;
                }

                int pv = 0x100 * report[1] + report[0];
                int sv = 0x100 * report[3] + report[2];
                byte  mv = report[4];
                byte alarm = report[5];
                int value = 0x100 * report[7] + report[6];
                int ecc = 0x100 * report[9] + report[8];

                int eccCheck = (pv + sv + 0x100 * alarm + mv + value + InstrumentID) & 0xFFFF;

                if (ecc != eccCheck)
                {
                    if (LogEverything)
                        Log.Record($"ECC mismatch: is {eccCheck:X}, should be {ecc:X}");
                    DeviceState.Error = 2;
                    return false;
                }

                // alarm and mv normally do have meaningful values
                // typically mv = 0x46 (01000110) when furnace is on; 0 when furnace is off
                if (LogEverything)
                    Log.Record($"PV={pv} SV={sv} MV={BinaryString(mv)} AL={BinaryString(alarm)} VAL={value}");

                DeviceState.SuspendEvents = true;
                DeviceState.Temperature = pv/10;
                DeviceState.Setpoint = sv/10;
                if (Parameter == Parameters.PowerMode)
                    DeviceState.IsOn = value < 8;       // 8 == Stopped
                DeviceState.SuspendEvents = false;

                return true;
			}
			catch (Exception e)
			{
				if (LogEverything) Log.Record(e.ToString());
				return false; 
			}
		}

        // TODO: move to Utility class?
        string BinaryString(int n, int len = 16)
        { return Convert.ToString(n, 2).PadLeft(len, '0'); }
        string BinaryString(uint n, int len = 16)
        { return Convert.ToString(n, 2).PadLeft(len, '0'); }
        string BinaryString(byte n, int len=8)
        { return Convert.ToString(n, 2).PadLeft(len, '0'); }

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

	public class MtiTargetState
	{
		[XmlIgnore] public Action StateChanged;

        public Stopwatch StateStopwatch { get; set; } = new Stopwatch();

        // needed for Temperature ramping (WorkingSetpoint
        [XmlIgnore] public MtiDeviceState DeviceState;
        public int RampStartTemperature { get; set; }

        public int Setpoint
		{
			get { return _Setpoint; }
			set
			{
				if (value <= 0) _Setpoint = 1;
				else if (value > 1200) _Setpoint = 1200;
				else _Setpoint = value;

                if (IsOn)
                    StartRamp();

                StateChanged?.Invoke();
			}
		}
		int _Setpoint;

        public bool IsOn
        {
            get { return _IsOn; }
            set
            {
                _IsOn = value;
                if (IsOn)
                    StartRamp();
                else
                    StateStopwatch.Restart();
                StateChanged?.Invoke();
            }
        }
        bool _IsOn;

        /// <summary>
        /// Degrees C per minute. Default is 10.
        /// </summary>
        public int RampRate { get; set; } = 10;


        [XmlIgnore] bool RampStarted = false;
        void StartRamp()
        {
            if (DeviceState == null)
                RampStarted = false;
            else
            {
                RampStartTemperature = DeviceState.Temperature;
                StateStopwatch.Restart();
                RampStarted = true;
            }
        }

        public int WorkingSetpoint
        {
            get
            {
                if (!IsOn)
                {
                    //return DeviceState.Setpoint;    // so there's nothing to do in stateManager
                    return Setpoint;                // so the Device.Setpoint will match the Target.Setpoint
                }

                if (!RampStarted)
                    StartRamp();

                if (!RampStarted)
                    return Setpoint;

                int dT = (int)Math.Round(StateStopwatch.Elapsed.TotalMinutes * Math.Abs(RampRate));
                if (Setpoint > RampStartTemperature)
                    return Math.Min(Setpoint, RampStartTemperature + dT);
                else
                    return Math.Max(Setpoint, RampStartTemperature - dT);
            }
        }
	}
    
    public class MtiDeviceState
	{
        public Action StateChanged;
        public bool SuspendEvents
        {
            get { return _SuspendEvents; }
            set
            {
                _SuspendEvents = value;
                if (!_SuspendEvents && ChangeOccurred)
                {
                    ChangeOccurred = false;
                    StateChanged?.Invoke();
                }
            }
        }
        bool _SuspendEvents;

        bool ChangeOccurred = false;
        private void registerChange()
        {
            if (SuspendEvents)
                ChangeOccurred = true;
            else
                StateChanged?.Invoke();
        }

        public bool Valid => SetpointValid && TemperatureValid && PowerModeValid;
        public int Error { get; set; }

		public int Setpoint
		{
			get { return _Setpoint; }
			set
			{
                int oldvalue = _Setpoint;
				_Setpoint = value;
				if (!SetpointValid || _Setpoint != oldvalue)
                    registerChange();
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
                    registerChange();
                TemperatureValid = true;
            }
        }
		int _Temperature;
        public bool TemperatureValid { get; set; } = false;

        public bool IsOn
        {
            get { return _IsOn; }
            set
            {
                bool oldvalue = _IsOn;
                _IsOn = value;
                if (!PowerModeValid || _IsOn != oldvalue)
                    registerChange();
                PowerModeValid = true;
            }
        }
        bool _IsOn;
        public bool PowerModeValid { get; set; } = false;

    }
}