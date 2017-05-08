using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabJack.LabJackUD;
using System.Windows.Forms;
using System.Threading;
using System.Xml.Serialization;
using Utilities;
using HACS.Core;

namespace HACS.Components
{
	public class LabJackDaq : Component
    {
		public static new List<LabJackDaq> List;
		public static new LabJackDaq Find(string name)
		{ return List?.Find(x => x.Name == name); }

		[XmlType(AnonymousType = true)]
		public enum States { Unknown, Initialized, Stopping, Stopped }		

		public string LocalID { get; set; }
		U6 lj;								 // device handle

		[XmlIgnore] public double HardwareVersion = 0;
		[XmlIgnore] public double SerialNumber = 0;
		[XmlIgnore] public double FirmwareVersion = 0;
		[XmlIgnore] public double BootloaderVersion = 0;
		[XmlIgnore] public double ProductID = 0;
		[XmlIgnore] public double U6Pro = 0;

        [XmlIgnore] public int StreamingBacklogHardware = 0;
        [XmlIgnore] public int StreamingBacklogDriver = 0;
		[XmlIgnore] public int ScanFreq = 0;
		[XmlIgnore] public int StreamSPP = 0;
		[XmlIgnore] public int StreamRPS = 0;
		[XmlIgnore] public long ScansReceived = 0;

		#region LJUD interface

		LJUD.IO ljIOType = 0;
		LJUD.CHANNEL ljChannel = 0;
		double ljDummyDouble = 0;      // dummy variables to satisfy LJUD driver signatures
		int ljDummyInt = 0;
		double[] ljDummyDoubleArray = { };

		#endregion LJUD interface

		#region state

		[XmlIgnore] public States State = States.Unknown;
		[XmlIgnore] Thread stateThread;
		[XmlIgnore] ManualResetEvent stateSignal = new ManualResetEvent(false);

		[XmlIgnore] public LogFile log;
		public bool LogEverything			// a debugging aid
		{
			get { return _logEverything; }
			set
			{
				_logEverything = value;
				if (_logEverything && log == null)
					log = new LogFile(@"LabJackDaq" + (" " + Name).TrimEnd() + " Log.txt");
			}
		}
		bool _logEverything = false;

		[XmlIgnore] public bool IsUp 
		{ 
			get 
			{ 
				return 
					State == States.Initialized &&
					stateThread != null && stateThread.IsAlive &&
					outputsThread != null && outputsThread.IsAlive &&
					streamThread != null && streamThread.IsAlive &&
					DataAcquired && Error == null; 
			} 
		}

		[XmlIgnore] public LabJackUDException Error { get; set; }

		#endregion state

		#region outputs

		public int OutputPace { get; set; }		// ms between output signal changes
		public int OutputIdle { get; set; }		// ms timeout when nothing to do

		[XmlIgnore] Thread outputsThread;
		[XmlIgnore] ManualResetEvent outputsSignal = new ManualResetEvent(false);

        // digital outputs
		struct doSet 
        { 
            public DIO Channel; 
            public bool OnOff;
			public doSet(DIO p, bool x) { Channel = p; OnOff = x; }
        }
        Queue<doSet> doSetQ = new Queue<doSet>();
		public int PendingDO { get { return doSetQ.Count; } }

		// analog outputs
		struct aoSet
        { 
            public int Channel; 
            public double Voltage;
            public aoSet(int c, double v) { Channel = c; Voltage = v; }

        }
        Queue<aoSet> aoSetQ = new Queue<aoSet>();
		public int PendingAO { get { return aoSetQ.Count; } }

		#endregion outputs

		#region streamed inputs

		static readonly int maxStreamChannels = 128;
		public int GND { get; set; }
		public int MinimumRetrievalInterval { get; set; }	// ms

		// If the AIN_SETTLING_TIME "channel" is not explicitly configured 
		// via ePut(), the driver adjusts settling time based on the resolution 
		// index and gain settings.
		public int SettlingTimeIndex = LJ_SETTLINGTIME_100uS;
		public int ResolutionIndex = 0;

		[XmlIgnore] public int RetrievalInterval;			// how often inputs are checked, in ms
        List<Meter> aiMeter = new List<Meter>();
        double[] streamData;
		

		[XmlIgnore] Thread streamThread;
		[XmlIgnore] ManualResetEvent streamSignal = new ManualResetEvent(false);

		[XmlIgnore] public bool IsStreaming { get { return streamThread != null && streamThread.IsAlive; } }
		[XmlIgnore] public bool DataAcquired = false;

		Stopwatch scanStopwatch = new Stopwatch();
		[XmlIgnore] public double ScanMilliseconds = 0;
		[XmlIgnore] public int MinimumScanTime = 0;
		// The minimum command response time is ~0.6 ms if the LabJack goes through a 
		// USB2 hub to the USB2 host. (I.e.: LJ -> hub -> host)
		// If the LJ is directly connected to a USB2 host port, or if any USB
		// component in the path is < 2.0, the minimum CRT goes up to 4 ms.
		[XmlIgnore] public int MinimumCRT = 1;		// rounded up to the nearest ms
		[XmlIgnore] public int MinimumStreamResponseTime = 0;
		[XmlIgnore] public int ExpectedScanTime = 0;
		[XmlIgnore] public int ExpectedStreamResponseTime = 0;


		#endregion streamed inputs

		#region LabJack Constants

		// U6 digital signals
		public enum DIO
		{
			FIO0 = 0, FIO1, FIO2, FIO3, FIO4, FIO5, FIO6, FIO7,
			EIO0, EIO1, EIO2, EIO3, EIO4, EIO5, EIO6, EIO7,
			CIO0, CIO1, CIO2, CIO3, MIO0, MIO1, MIO2
		}

		static readonly int LJ_SETTLINGTIME_AUTO = 0;
		static readonly int LJ_SETTLINGTIME_20uS = 1;
		static readonly int LJ_SETTLINGTIME_50uS = 2;
		static readonly int LJ_SETTLINGTIME_100uS = 3;
		static readonly int LJ_SETTLINGTIME_200uS = 4;
		static readonly int LJ_SETTLINGTIME_500uS = 5;
		static readonly int LJ_SETTLINGTIME_1mS = 6;
		static readonly int LJ_SETTLINGTIME_2mS = 7;
		static readonly int LJ_SETTLINGTIME_5mS = 8;
		static readonly int LJ_SETTLINGTIME_10mS = 9;
		static readonly double[] settlingTimes = { 0.0, 20e-6, 50e-6, 100e-6, 200e-6, 500e-6, 1e-3, 2e-3, 5e-3, 10e-3 };

		// The maximum sampling rate (samples/s) is limited by the resolution index
		// Both of these arrays are indexed by LJ_RESOLUTION_INDEX, below.
		static readonly double[] maxSampleRates = { 50000.0, 50000.0, 30000.0, 16000.0, 8400.0, 4000.0, 2000.0, 1000.0, 500.0 };
		static readonly double[] intersampleDelays = { 15e-6, 30e-6, 40e-6, 110e-6, 220e-6, 440e-6, 875e-6, 1740e-6, };

		#endregion

		public LabJackDaq()
		{
			OutputPace = 1;
			OutputIdle = 50;
			MinimumRetrievalInterval = 40;

		}

		public LabJackDaq(string name, int local_ID) : this()
		{
			Name = name;
			LocalID = local_ID.ToString();
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(Name);
			sb.Append(":\r\n");

			StringBuilder sb2 = new StringBuilder();
			sb2.Append(Utility.ToStringLine("Hardware Version", HardwareVersion));
			sb2.Append(Utility.ToStringLine("Serial Number", SerialNumber));
			sb2.Append(Utility.ToStringLine("Firmware Version", FirmwareVersion));
			sb2.Append(Utility.ToStringLine("Bootloader Version", BootloaderVersion));
			sb2.Append(Utility.ToStringLine("Product ID", ProductID));
			sb2.Append(Utility.ToStringLine("U6 Pro", U6Pro != 0));
			sb2.Append(Utility.ToStringLine("IsUp", IsUp));

			sb2.Append(Utility.ToStringLine("Meters", aiMeter.Count));
			sb2.Append(Utility.ToStringLine("AI count", streamData.Length));
			sb2.Append(Utility.ToStringLine("Target settling time", (settlingTimes[SettlingTimeIndex] * 1000000).ToString("0 µs")));

			sb2.Append(Utility.ToStringLine("Retrieval interval", 
				string.Format("{0} ms (minimum: {1} ms)", RetrievalInterval, MinimumRetrievalInterval)));
			sb2.Append(Utility.ToStringLine("Scan frequency", ScanFreq));
			sb2.Append(Utility.ToStringLine("Resolution index", ResolutionIndex));

			double msr = maxSampleRates[ResolutionIndex];
			sb2.Append(Utility.ToStringLine("Settling time",
				string.Format("~{0} µs", 1000000 / msr)));
			sb2.Append(Utility.ToStringLine("Maximum samples per second", msr));

			sb2.Append(Utility.ToStringLine("Expected scan time", 
				string.Format("{0} ms (minimum: {1} ms)", ExpectedScanTime, MinimumScanTime)));
			sb2.Append(Utility.ToStringLine("Command-response time", MinimumCRT.ToString("~0 ms")));
			sb2.Append(Utility.ToStringLine("Expected stream response time", 
				string.Format("{0} ms (minimum: {1} ms)", ExpectedStreamResponseTime, MinimumStreamResponseTime)));

			sb2.Append(Utility.ToStringLine("Stream samples per packet", StreamSPP));
			sb2.Append(Utility.ToStringLine("Stream reads per second", StreamRPS));

			sb2.Append(Utility.ToStringLine("Is Streaming", IsStreaming));
			sb2.Append(Utility.ToStringLine("Stream backlog (hardware)", string.Format("{0,3}", StreamingBacklogHardware)));
			sb2.Append(Utility.ToStringLine("Stream backlog (driver)", string.Format("{0,3}", StreamingBacklogDriver)));
			sb2.Append(Utility.ToStringLine("Data Acquired", DataAcquired));
			sb2.Append(Utility.ToStringLine("Scans received", ScansReceived));
			sb2.Append(Utility.ToStringLine("Most recent retrieval period", ScanMilliseconds.ToString("0.000 ms")));
			sb2.Append(Utility.ToStringLine("Pending DO", PendingDO));
			sb2.Append(Utility.ToStringLine("Pending AO", PendingAO));

			sb.Append(Utility.IndentLines(sb2.ToString()));
			return sb.ToString();
		}

		public override void Initialize()
		{
			if (LogEverything) log.Record("Initializing...");
			try
			{
				lj = new U6(LJUD.CONNECTION.USB, LocalID, false);
				checkStatus(LJUD.CHANNEL.LOCALID);
				HardwareVersion = checkStatus(LJUD.CHANNEL.HARDWARE_VERSION);
				SerialNumber = checkStatus(LJUD.CHANNEL.SERIAL_NUMBER);
				FirmwareVersion = checkStatus(LJUD.CHANNEL.FIRMWARE_VERSION);
				BootloaderVersion = checkStatus(LJUD.CHANNEL.BOOTLOADER_VERSION);
				ProductID = checkStatus(LJUD.CHANNEL.PRODUCTID);
				U6Pro = checkStatus(LJUD.CHANNEL.U6_PRO);

				if (LogEverything) 
					log.Record("\r\n\t" +
						"HardwareVersion: " + HardwareVersion.ToString() + "\r\n\t" +
						"SerialNumber: " + SerialNumber.ToString() + "\r\n\t" +
						"FirmwareVersion: " + FirmwareVersion.ToString() + "\r\n\t" +
						"BootloaderVersion: " + BootloaderVersion.ToString() + "\r\n\t" +
						"ProductID: " + ProductID.ToString() + "\r\n\t" +
						"U6Pro: " + U6Pro.ToString()
						);

				ClearError();
			}
			catch
			{
				Error = new LabJackUDException(LJUD.LJUDERROR.DEVICE_NOT_OPEN);
				return;
			}

			stateThread = new Thread(manageState);
			stateThread.Name = Name + " stateThread";
			stateThread.IsBackground = true;
			stateThread.Start();

			outputsThread = new Thread(manageOutputs);
			outputsThread.Name = Name + " ioThread";
			outputsThread.IsBackground = true;
			outputsThread.Start();

			configureDAQ();
			startStreaming();

			State = States.Initialized;
			if (LogEverything) log.Record("...Initialized.");
		}

		/// <summary>
		/// Set digital output channel on/high/true or off/low/false
		/// </summary>
		/// <param name="channel"></param>
		/// <param name="onOff"></param>
		public void SetDO(DIO channel, bool onOff)
		{
			lock (doSetQ)
			{
				doSetQ.Enqueue(new doSet(channel, onOff));
				outputsSignal.Set();
			}
		}

		/// <summary>
		/// Set analog output channel to the given voltage
		/// </summary>
		/// <param name="channel"></param>
		/// <param name="volts"></param>
		public void SetAO(int channel, double volts)
		{
			lock (aoSetQ)
			{
				aoSetQ.Enqueue(new aoSet(channel, volts));
				outputsSignal.Set();
			}
		}

		public void ConnectAI(Meter meter)
		{ if (meter != null) aiMeter.Add(meter); }
		
        public void Stop()
        {
			if (LogEverything) log.Record("Stopping...");
			State = States.Stopping;
			stopStreaming();
			stateSignal.Set();
		}

		public string ErrorMessage(LabJackUDException Error)
		{
			if (Error == null) return "";
			return Name + " Error: " + Error.ToString().TrimEnd(new[] {'\0'});
		}

		public void ClearError() { Error = null; }

		void manageState()
		{
			try
			{
				while (true)
				{
					if (State == States.Initialized)
					{
						if (!IsStreaming)		// attempt to recover
							startStreaming();
					}
					else if (State == States.Stopping)
					{
						if (!outputsThread.IsAlive && !streamThread.IsAlive)
						{
							State = States.Stopped;
							if (LogEverything) log.Record("Stopped.");
							break;
						}
					}
					checkStatus();
					stateSignal.WaitOne(100);
				}
				if (LogEverything) log.Record("Ending State Thread");
			}
			catch (Exception e) { MessageBox.Show(e.ToString()); }
		}

		void checkStatus()
		{
			// The following two eGet's check the amount of data currently
			// buffered by the LabJack U6 and the UD driver, respectively.
			// The hardware backlog should stay near zero in normal operation.
			// If the driver backlog is increasing over time, the application
			// is not retrieving the data often enough.
			StreamingBacklogHardware = (int)checkStatus(LJUD.CHANNEL.STREAM_BACKLOG_COMM);
			StreamingBacklogDriver = (int)checkStatus(LJUD.CHANNEL.STREAM_BACKLOG_UD);
		}

		double checkStatus(LJUD.CHANNEL configChannel)
		{
			double ljResult = 0;
			LJUD.eGet(lj.ljhandle, LJUD.IO.GET_CONFIG, configChannel, ref ljResult, 0);
			return ljResult;
		}

		void manageOutputs()
		{
			try
			{
				LJUD.LJUDERROR ljError;
				while (State < States.Stopping)
				{
					// Handle any scheduled digital outputs
					if (doSetQ.Count > 0)
					{
						doSet cmd;
						lock (doSetQ) cmd = doSetQ.Dequeue();

						ljError = LJUD.eDO(lj.ljhandle, (int)cmd.Channel, cmd.OnOff ? 1 : 0);
						if (ljError != LJUD.LJUDERROR.NOERROR)
							handleLabJackException(new LabJackUDException(ljError));
					}

					// Handle any scheduled analog outputs
					if (aoSetQ.Count > 0)
					{
						aoSet cmd;
						lock (aoSetQ) cmd = aoSetQ.Dequeue();
						ljError = LJUD.eDAC(lj.ljhandle, cmd.Channel, cmd.Voltage, 0, 0, 0);
						if (ljError != LJUD.LJUDERROR.NOERROR)
							handleLabJackException(new LabJackUDException(ljError));
					}

					int timeout = PendingDO + PendingAO > 0 ? OutputPace : OutputIdle;
					outputsSignal.Reset();
					outputsSignal.WaitOne(timeout);
				}
				if (LogEverything) log.Record("Ending Outputs Thread");
			}
			catch (Exception e) { MessageBox.Show(e.ToString()); }
		}

		// Determine the appropriate ResolutionIndex, given a settling time
		int determineResolutionIndex(double settlingTime)
		{
			for (int i = 0; i < intersampleDelays.Length; ++i)
				if (intersampleDelays[i] >= settlingTime)
					return i;
			return 0;
		}

		// Find the longest pre-programmed settling time that allows scanFrequency
		int bestSettlingTimeIndex(double scanFrequency)
		{
			double maxSettlingTime = 1 / scanFrequency;
			for (int i = settlingTimes.Length - 1; i > 0; --i)
				if (settlingTimes[i] < maxSettlingTime)
					return i;
			return settlingTimes.Length - 1;
		}

		int scanTimeMilliseconds(int samples, int resolutionIndex)
		{
			return (int)Math.Ceiling(samples * (1 / maxSampleRates[resolutionIndex]) * 1000);
		}

		void configureDAQ()
        {
			if (LogEverything) log.Record("Configuring...");
			try
            {
				DataAcquired = false;
				int AinCount = aiMeter.Count * 2;	// including ground after every "real" channel
				streamData = new double[AinCount];

				// Start with the minimum resolution index that would achieve the target settling time
				int minResolutionIndex = determineResolutionIndex(settlingTimes[SettlingTimeIndex]);

				// Find the number of milliseconds required to complete 1 scan of all the analog inputs
				MinimumScanTime = scanTimeMilliseconds(AinCount, minResolutionIndex);
				MinimumStreamResponseTime = MinimumScanTime + MinimumCRT;
				int targetScanTime = MinimumStreamResponseTime + 3;	//	provide a few ms idle time for unscheduled activities

				// To avoid buffer overflows, data must be retrieved faster
				// than it is produced.
				// Retrieve data at twice the maximum scan rate, if possible,
				// but no more often than the MinimumRetrievalInterval, which
				// is intended to limit communications bandwidth consumption.
				RetrievalInterval = Math.Max(targetScanTime / 2, MinimumRetrievalInterval);
				double retrievalFrequency = 1000.0 / RetrievalInterval;

				// Set the scan frequency to half the retrieval frequency.
				// New data should be available about every other attempted 
				// retrieval.
				double scanFrequency = retrievalFrequency / 2;

				// Based on the determined scanFrequency, find the resolution index that
				// corresponds to the maximum possible settling time.
				// note: settling time is not used directly in stream mode?
				double resolutionIndex = determineResolutionIndex(1 / scanFrequency);

				// Cancel any streaming in progress
				try { LJUD.ePut(lj.ljhandle, LJUD.IO.STOP_STREAM, 0, 0, 0); }
				catch { }

				// Clear any existing list of streamed channels
				LJUD.ePut(lj.ljhandle, LJUD.IO.CLEAR_STREAM_CHANNELS, 0, 0, 0);

				// define and configure the channels to be streamed
				foreach (Meter m in aiMeter)
					addToStream(m);

				LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_CONFIG,
					LJUD.CHANNEL.STREAM_SCAN_FREQUENCY, scanFrequency, 0, 0);

				LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_CONFIG,
					LJUD.CHANNEL.AIN_RESOLUTION, resolutionIndex, 0, 0);

				// STREAM_READS_PER_SECOND tells the UD driver how often to 
				// check for (and possibly retrieve) new data from the LabJack's
				// hardware buffer.
				LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_CONFIG,
					LJUD.CHANNEL.STREAM_READS_PER_SECOND, retrievalFrequency, 0, 0);

				// Give the driver a big enough buffer to cover random operating 
				// system latencies
				// (2 seconds * scan frequency) = retrievalFrequency
				LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_CONFIG, LJUD.CHANNEL.STREAM_BUFFER_SIZE,
					retrievalFrequency * AinCount, 0, 0);

				//Configure reads to retrieve the requested amount of data, or return nothing.
				LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_CONFIG, LJUD.CHANNEL.STREAM_WAIT_MODE,
					(double)LJUD.STREAMWAITMODES.ALL_OR_NONE, 0, 0);

				// If necessary, reduce the packet size to prevent the UD driver from 
				// waiting for multiple scans to complete before retrieving data from the u6.
				if (AinCount < 25)
					LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_CONFIG,
						LJUD.CHANNEL.STREAM_SAMPLES_PER_PACKET, AinCount, 0, 0);

                // Execute the pending requests
                LJUD.GoOne(lj.ljhandle);
                checkLjForErrors();
            }
            catch (LabJackUDException e)
			{
				MessageBox.Show("Labjack Configuration Error:" + "\r\n" + e.ToString());
				//handleLabJackException(e); 
			}
			if (LogEverything) log.Record("...Configured.");
		}

		void addToStream(Meter m)
		{
			if (m == null) return;

			setVoltageRange(m);
			if (m.AiMode == Meter.AiModeType.Differential)
				LJUD.AddRequest(lj.ljhandle, LJUD.IO.ADD_STREAM_CHANNEL_DIFF,
					m.Channel, 0, negTerminal(m.Channel), 0);
			else     // single-ended
				LJUD.AddRequest(lj.ljhandle, LJUD.IO.ADD_STREAM_CHANNEL,
					m.Channel, 0, 0, 0);

			// connect ADC to GND after every channel
			LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_AIN_RANGE,
				GND, (double)LJUD.RANGES.BIP10V, 0, 0);
			LJUD.AddRequest(lj.ljhandle, LJUD.IO.ADD_STREAM_CHANNEL,
				GND, 0, 0, 0);
		}

		// Sets the daq's PGIA gain for the given meter's analog input.
        // The PGIA gain is defined by the max voltage to be sensed,
        // like the Range setting on a multimeter. 
        // +/- 10V => Gain = 1
        // +/- 1V => Gain = 10
        // +/- 0.1V => Gain = 100
        // +/- 0.01V => Gain = 1000
        void setVoltageRange(Meter m)
        {
            if (m == null) return;
            double ljVrange;

            if (m.MaxVoltage == 0.01)
                ljVrange = (double)LJUD.RANGES.BIPP01V;
            else if (m.MaxVoltage == 0.1)
                ljVrange = (double)LJUD.RANGES.BIPP1V;
            else if (m.MaxVoltage == 1)
                ljVrange = (double)LJUD.RANGES.BIP1V;
            else
                ljVrange = (double)LJUD.RANGES.BIP10V;

            LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_AIN_RANGE,
                (LJUD.CHANNEL)m.Channel, ljVrange, 0, 0);
        }

        // Given the positive terminal of a LabJack differential analog input,
        // returns the corresponding negative terminal.
        int negTerminal(int posTerminal)
        {
            if (posTerminal < 16)
                return posTerminal + 1;
            else return posTerminal + 8;
        }

        void startStreaming()
        {
			try
            {
				if (LogEverything) log.Record("Starting stream...");

				LJUD.LJUDERROR ljError = LJUD.eGet(lj.ljhandle, 
					LJUD.IO.START_STREAM, 0, ref ljDummyDouble, 0);

				ScanFreq = (int)checkStatus(LJUD.CHANNEL.STREAM_SCAN_FREQUENCY);
				StreamSPP = (int)checkStatus(LJUD.CHANNEL.STREAM_SAMPLES_PER_PACKET);
				StreamRPS = (int)checkStatus(LJUD.CHANNEL.STREAM_READS_PER_SECOND);
				ResolutionIndex = (int)checkStatus(LJUD.CHANNEL.AIN_RESOLUTION);
				ExpectedScanTime = scanTimeMilliseconds(streamData.Length, ResolutionIndex);
				ExpectedStreamResponseTime = ExpectedScanTime + MinimumCRT;

				if (LogEverything)
					log.Record("\r\n\t" +
						"Scan Frequency: " + ScanFreq.ToString() + "\r\n\t" +
						"Stream samples per packet: " + StreamSPP.ToString() + "\r\n\t" +
						"Scan reads per second: " + StreamRPS.ToString()
						);

				if (ljError == LJUD.LJUDERROR.NOERROR)
				{
					ClearError();

					streamSignal.Reset();
					streamThread = new Thread(manageStream);
					streamThread.Name = Name + " streamThread";
					streamThread.IsBackground = true;
					streamThread.Start();
				}
				else
					throw new LabJackUDException(ljError);
			}
            catch (LabJackUDException e) { handleLabJackException(e); }
			if (LogEverything) log.Record("Streaming...");
		}

		void manageStream()
		{
			try
			{
				LJUD.LJUDERROR ljError;
				scanStopwatch.Restart();
				while (true)
				{
					double scans = 1;	// Attempt to retrieve one scan

					ljError = LJUD.eGet(lj.ljhandle, LJUD.IO.GET_STREAM_DATA,
							LJUD.CHANNEL.ALL_CHANNELS, ref scans, streamData);

					if (ljError != LJUD.LJUDERROR.NOERROR &&
							ljError != LJUD.LJUDERROR.NOTHING_TO_STREAM)
						handleLabJackException(new LabJackUDException(ljError));

					// Transfer the voltages to the meters.
					if (scans > 0)
					{
						ScansReceived += (long)scans;
						for (int i = 0; i < aiMeter.Count; i++)
							aiMeter[i].Voltage = streamData[i+i];
						DataAcquired = true;

						ScanMilliseconds = scanStopwatch.Elapsed.TotalMilliseconds;
						scanStopwatch.Restart();

						if (LogEverything) log.Record("Stream data received.");
					}

					if (streamSignal.WaitOne(RetrievalInterval))
						break;		// Signal received
					else
						continue;	// timed out
				}
				if (LogEverything) log.Record("Ending Stream Thread");
			}
			catch (Exception e) { MessageBox.Show(e.ToString()); }
		}

        void stopStreaming()
        {
			if (LogEverything) log.Record("Stopping stream...");
			streamSignal.Set();
			try { LJUD.ePut(lj.ljhandle, LJUD.IO.STOP_STREAM, 0, 0, 0); }
			catch { }
			DataAcquired = false;
			if (LogEverything) log.Record("Stream stopped.");
        }

		void checkLjForErrors()
		{
			double ljResult = 0;

			// Scan the configuration results for errors
			LJUD.GetFirstResult(lj.ljhandle,
				ref ljIOType, ref ljChannel, ref ljResult, ref ljDummyInt, ref ljDummyDouble);
			bool resultsAvailable = true;
			while (resultsAvailable)
			{
				try
				{
					LJUD.GetNextResult(lj.ljhandle,
						ref ljIOType, ref ljChannel, ref ljResult, ref ljDummyInt, ref ljDummyDouble);
				}
				catch (LabJackUDException e)
				{
					// If the 'error' is NO_MORE_DATA_AVAILABLE, we are done. Otherwise, it's a real error.
					if (e.LJUDError == U6.LJUDERROR.NO_MORE_DATA_AVAILABLE)
						resultsAvailable = false;
					else
						handleLabJackException(e);
				}
			}
			ClearError();
		}

		void handleLabJackException(LabJackUDException e)
		{
			if (LogEverything) log.Record(ErrorMessage(e));
			Error = e; 
		}
	}
}
