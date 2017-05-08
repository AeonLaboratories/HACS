using HACS.Core;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
    public class Controller : Component
    {
		public static new List<Controller> List;
		public static new Controller Find(string name)
		{ return List?.Find(x => x.Name == name); }

		public delegate void ResponseProcessorType(string s);

        #region variables

		Stopwatch txrxStopWatch = new Stopwatch();
        
        #endregion variables

        #region Properties

        [XmlAttribute]
        public override string Name
        {
            get { return _Name; }
            set
            {
                _Name = value;
				if (log != null) log = openLog();
			}
        }
        string _Name = "";

		public SerialDevice SerialDevice { get; set; }

		public bool Disconnected { get { return SerialDevice.Disconnected; } }
		public bool Idle { get { return SerialDevice.Idle; } }

		[XmlIgnore] public ResponseProcessorType ResponseProcessor;

		[XmlIgnore] public long ResponseTime
		{
			get
			{
				long ms; lock (txrxStopWatch) ms = txrxStopWatch.ElapsedMilliseconds;
				if (ms > _ResponseTime) _ResponseTime = ms;
				return _ResponseTime;
			} 
		}
		long _ResponseTime;
		[XmlIgnore] public long LongestResponseTime
		{
			get  { long ms; lock (txrxStopWatch) ms = txrxStopWatch.Longest; return ms; }
			set { lock (txrxStopWatch) txrxStopWatch.Longest = value; }
		}

        [XmlIgnore] public uint CommandCount { get { return _CommandCount; } }
        uint _CommandCount = 0;
		[XmlIgnore] public uint ResponseCount { get { return _ResponseCount; } }
        uint _ResponseCount = 0;

        #endregion Properties

		LogFile openLog() { return new LogFile(@"Controller " + Name + " Log.txt"); }

		LogFile _log;
		[XmlIgnore] public LogFile log
		{
			get
			{
				if (_log == null)
					log = openLog();
				return _log;
			}
			set
			{
				_log = value;
				if (SerialDevice != null)
					SerialDevice.DebugLog = _log;
			}
		}

		bool _logEverything = false;
        public bool LogEverything
        {
            get { return _logEverything; }
            set
			{
				_logEverything = value;
				if (SerialDevice != null)
					SerialDevice.Logging = _logEverything;
				if (_logEverything && log == null)
					log = openLog();
            }
        }
        
        public Controller()
		{
			SerialDevice = new SerialDevice();
		}

		public Controller(string name, SerialPortSettings portSettings) : this()
		{
			Name = name;
			SerialDevice.Configure(portSettings);
		}

		public override void Initialize()
		{
			SerialDevice.Initialize();
			SerialDevice.ResponseReceived = ResponseReceivedHandler;
			SerialDevice.DebugLog = _log;
			SerialDevice.Logging = _logEverything;

			base.Initialize();
		}

		public void Reset() { SerialDevice.Reset(); lock (txrxStopWatch) txrxStopWatch.Stop(); }

		public void Close()
		{ 
			SerialDevice.Close();
			if (_log != null) _log.Close();
		}

        public bool Command(string s)
        {
            if (LogEverything) log.Record(s);
			bool status = SerialDevice.Command(s);
			lock (txrxStopWatch) if (!txrxStopWatch.IsRunning) txrxStopWatch.Restart();
            _CommandCount++;
			return status;
		}

        // This is SerialDevice's ResponseReceived delegate.
        // Forwards the Response to the ResponseProcessor delegate,
        // which has the responsiblity of marshalling if needed.
        // Runs in SerialDevice's prxThread.
        void ResponseReceivedHandler(string s)
        {
            if (LogEverything) log.Record(s.TrimEnd());
            lock (txrxStopWatch)
			{
				txrxStopWatch.Stop();
				_ResponseTime = txrxStopWatch.ElapsedMilliseconds;
			}
			
            _ResponseCount++;
            if (ResponseProcessor != null) ResponseProcessor(s);
        }
    }
}
