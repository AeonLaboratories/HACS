using HACS.Core;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using Utilities;
using System;
using Newtonsoft.Json;
using System.ComponentModel;

namespace HACS.Components
{
    public class Controller : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<Controller> List = new List<Controller>();
		public static new Controller Find(string name) { return List.Find(x => x?.Name == name); }

		protected virtual void Initialize()
        {
			SerialDevice.ResponseReceived = ResponseReceivedHandler;
			SerialDevice.Initialize();
        }

		protected virtual void Stop()
		{
			WaitForIdle();
		}

		protected virtual void PostStop()
        {
            SerialDevice.Close();
            Log?.Close();
        }

		public Controller()
		{
			List.Add(this);
			OnInitialize += Initialize;
			OnPostStop += PostStop;
		}

		#endregion Component Implementation

		#region variables

		Stopwatch txrxStopwatch = new Stopwatch();
		
		#endregion variables

		#region Properties

		[XmlAttribute]
		public override string Name
		{
			get { return base.Name; }
			set
			{
                base.Name = value;
				if (Log != null) Log = openLog();
			}
		}

		[JsonProperty]
		public SerialDevice SerialDevice { get; set; } = new SerialDevice();

		public virtual bool Disconnected => SerialDevice.Disconnected;
		public virtual void WaitForIdle() => SerialDevice.WaitForIdle();
		public virtual bool Idle => SerialDevice.Idle; 

		[XmlIgnore] public Action<string> ResponseProcessor;

		[XmlIgnore] public long ResponseTime
		{
			get
			{
				long ms; lock (txrxStopwatch) ms = txrxStopwatch.ElapsedMilliseconds;
				if (ms > _ResponseTime) _ResponseTime = ms;
				return _ResponseTime;
			}
		}

        [XmlIgnore] public long LongestResponseTime
		{
			get  { long ms; lock (txrxStopwatch) ms = txrxStopwatch.Longest; return ms; }
			set { lock (txrxStopwatch) txrxStopwatch.Longest = value; }
		}
        long _ResponseTime;

        [XmlIgnore] public uint CommandCount { get; private set; } = 0;

        [XmlIgnore] public uint ResponseCount { get; private set; } = 0;

        string LogFileName => (string.IsNullOrEmpty(Name) ? "Controller" : Name) + " Log.txt";
        LogFile openLog() { return new LogFile(LogFileName); }
        [XmlIgnore] public LogFile Log
		{
			get
			{
				if (_log == null)
					Log = openLog();
				return _log;
			}
			set
			{
				_log = value;
			}
		}
        LogFile _log;

		[JsonProperty]//, DefaultValue(false)]
        public bool EscapeLoggedData
        {
            get { return _EscapeLoggedData;  }
            set
            {
                _EscapeLoggedData = value;
                if (SerialDevice != null)
                    SerialDevice.EscapeLoggedData = _EscapeLoggedData;
            }
        }
        bool _EscapeLoggedData = false;

        string Escape(string s)
        {
            if (EscapeLoggedData)
                return Regex.Escape(s);
            else
                return s;
        }

		[JsonProperty]//, DefaultValue(false)]
		public bool LogEverything
		{
			get { return _logEverything; }
			set
			{
				_logEverything = value;
				if (LogEverything && SerialDevice != null)
					SerialDevice.Log = Log;
			}
		}
        bool _logEverything = false;

		[JsonProperty]//, DefaultValue(false)]
		public bool LogCommands
        {
            get { return LogComms || _LogCommands; }
            set { _LogCommands = value; }
        }
        bool _LogCommands = false;

		[JsonProperty]//, DefaultValue(false)]
		public bool LogResponses
        {
            get { return LogComms || _LogResponses; }
            set { _LogResponses = value; }
        }
        bool _LogResponses = false;

		[JsonProperty]//, DefaultValue(false)]
		public bool LogComms
		{
			get { return (LogEverything || _LogCommands && _LogResponses); }
			set { LogCommands = LogResponses = value; }
		}

        #endregion Properties

		public Controller(string name, SerialPortSettings portSettings) : this()
		{
			Name = name;
			SerialDevice.Configure(portSettings);
		}

		public void Reset() { SerialDevice.Reset(); lock (txrxStopwatch) txrxStopwatch.Stop(); }

		public virtual bool Command(string s)
		{
            if (LogCommands)
                Log.Record(Name + " Command: " + Escape(s));

            bool status = SerialDevice.Command(s);
			lock (txrxStopwatch) if (!txrxStopwatch.IsRunning) txrxStopwatch.Restart();
			CommandCount++;
			return status;
		}

		// This is SerialDevice's ResponseReceived delegate.
		// Forwards the Response to the ResponseProcessor delegate,
		// which has the responsiblity of marshalling if needed.
		// Runs in SerialDevice's prxThread.
		void ResponseReceivedHandler(string s)
		{
			if (LogResponses)
                Log.Record(Name + " Response: " + Escape(s.TrimEnd()));
			lock (txrxStopwatch)
			{
				txrxStopwatch.Stop();
				_ResponseTime = txrxStopwatch.ElapsedMilliseconds;
			}
			
			ResponseCount++;
			ResponseProcessor?.Invoke(s);
		}
	}
}
