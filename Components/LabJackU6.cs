using HACS.Core;
using LabJack.LabJackUD;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Utilities;

namespace HACS.Components
{
	public class LabJackU6 : DeviceManager, ILabJackU6,
		LabJackU6.IConfig, LabJackU6.IDevice
	{
		#region Device constants

		#region LJUD interface

		LJUD.IO ljIOType = 0;
		LJUD.CHANNEL ljChannel = 0;
		double ljDummyDouble = 0;   // dummy variables to satisfy LJUD driver signatures
		int ljDummyInt = 0;
		double[] ljDummyDoubleArray = { };

		#endregion LJUD interface

		//static readonly int LJ_SETTLINGTIME_AUTO = 0;
		//static readonly int LJ_SETTLINGTIME_20uS = 1;
		//static readonly int LJ_SETTLINGTIME_50uS = 2;
		static readonly int LJ_SETTLINGTIME_100uS = 3;
		//static readonly int LJ_SETTLINGTIME_200uS = 4;
		//static readonly int LJ_SETTLINGTIME_500uS = 5;
		//static readonly int LJ_SETTLINGTIME_1mS = 6;
		//static readonly int LJ_SETTLINGTIME_2mS = 7;
		//static readonly int LJ_SETTLINGTIME_5mS = 8;
		//static readonly int LJ_SETTLINGTIME_10mS = 9;
		static readonly double[] settlingTimes = { 0.0, 20e-6, 50e-6, 100e-6, 200e-6, 500e-6, 1e-3, 2e-3, 5e-3, 10e-3 };

		// The maximum sampling rate (samples/s) is limited by the resolution index
		// Both of these arrays are indexed by LJ_RESOLUTION_INDEX, below.
		static readonly double[] maxSampleRates = { 50000.0, 50000.0, 30000.0, 16000.0, 8400.0, 4000.0, 2000.0, 1000.0, 500.0 };
		static readonly double[] intersampleDelays = { 15e-6, 15e-6, 30e-6, 40e-6, 110e-6, 220e-6, 440e-6, 875e-6, 1740e-6 };

		#endregion Device constants

		#region Class interface properties and methods

		#region Device interfaces
		public new interface IDevice : DeviceManager.IDevice
		{
			int LocalId { get; set; }
			double HardwareVersion { get; set; }
			double SerialNumber { get; set; }
			double FirmwareVersion { get; set; }
			double BootloaderVersion { get; set; }
			double ProductID { get; set; }
			double U6Pro { get; set; }
			int StreamingBacklogHardware { get; set; }
			int StreamingBacklogDriver { get; set; }
			int ScanFrequency { get; set; }
			int ResolutionIndex { get; set; }
			int StreamSamplesPerPacket { get; set; }
			int StreamReadsPerSecond { get; set; }
			long ScansReceived { get; set; }
		}
		public new interface IConfig : DeviceManager.IConfig
		{
			/// <summary>
			/// This value selects the LabJack with the
			/// specified Local ID.
			/// </summary>
			int LocalId { get; }
		}
		public new IDevice Device => this;
		public new IConfig Config => this;

		#endregion Device interfaces

		#region IDeviceManager
		public override bool IsSupported(IManagedDevice d, string key)
		{
			if (
				IsValidKey(key, "ai", 127) && d is IAnalogInput ||
				IsValidKey(key, "ao", 1) && d is IAnalogOutput ||
				IsValidKey(key, "do", 7) && d is IDigitalOutput
			   )
				return true;

			// Note: FIO0..7 drive open-collector transistors on the HACS-U6 interface CCA
			// IDigitalInput is not presently supported
			Log?.Record($"Connect: {d.Name}'s key \"{key}\" and type ({d.GetType()}) are not supported together." +
				$"\r\n\tOne of them is invalid or they are not compatible.");
			return false;
		}

		public override bool Ready => 
			base.Ready && 
			ConnectedToDaq;
		public override bool HasWork => 
			base.HasWork ||
			!serviceQ.IsEmpty;

		#endregion IDeviceManager

		#region IDaq

		/// <summary>
		/// The DAQ is connected, data has been acquired, and there is no error.
		/// </summary>
		public bool IsUp => !Stopping && ConnectedToDaq && DataAcquired && LJError == null;

		/// <summary>
		/// The DAQ analog input stream is running.
		/// </summary>
		public bool IsStreaming
		{ 
			get => isStreaming;
			protected set => Ensure(ref isStreaming, value);
		} 
		bool isStreaming = false;

		/// <summary>
		/// Data has been received from the DAQ analog input stream.
		/// </summary>
		public bool DataAcquired
		{
			get => dataAcquired;
			protected set => Ensure(ref dataAcquired, value);
		}
		bool dataAcquired = false;

		/// <summary>
		/// The number of scans per second in streaming mode. Each 
		/// scan includes one new data value for each analog and 
		/// digital input.
		/// </summary>
		public int ScanFrequency => scanFrequency;
		int IDevice.ScanFrequency
		{
			get => scanFrequency;
			set
			{
				Set(ref scanFrequency, value);
				// Digital filters on analog inputs may depend on the 
				// DAQ scan frequency, which is obtained when the DAQ
				// starts streaming.
				foreach (var d in Devices.Values)
				{
					if (d is Meter dq &&
						dq.Filter is ButterworthFilter f &&
						f.SamplingFrequency != scanFrequency)
						f.SamplingFrequency = scanFrequency;
				}
			}
		}
		int scanFrequency;

		public string Error => LJError is null ? null :
			$"{Name} Error: {LJError.LJUDError}: {LJError.ToString().TrimEnd(new[] { '\0' })}";

		public void ClearError() => ClearLJError();

		#endregion IDaq

		#region Settings

		/// <summary>
		/// The LabJack's Local ID code. This value may be programmed
		/// into the LabJack using the LJControlPanel utility.
		/// </summary>
		public int LocalId
		{
			get => localId;
			set => Ensure(ref TargetLocalId, value, NotifyConfigChanged, nameof(TargetLocalId));
		}
		[JsonProperty("LocalId")]
		int TargetLocalId;
		int IConfig.LocalId => TargetLocalId;
		int IDevice.LocalId
		{
			get => localId;
			set => Ensure(ref localId, value);
		}
		int localId;

		/// <summary>
		/// Minimum stream data retrieval interval, in milliseconds.
		/// Default 40.
		/// </summary>
		[JsonProperty, DefaultValue(40)]
		public int MinimumRetrievalInterval
		{
			get => minimumRetrievalInterval;
			set => Ensure(ref minimumRetrievalInterval, value);
		}
		int minimumRetrievalInterval = 40;

		// If the AIN_SETTLING_TIME "channel" is not explicitly configured 
		// via ePut(), the driver adjusts settling time based on the resolution 
		// index and gain settings.
		[JsonProperty, DefaultValue(3)]     // LJ_SETTLINGTIME_100uS == 3
		public int SettlingTimeIndex
		{
			get => settlingTimeIndex;
			set => Ensure(ref settlingTimeIndex, value);
		}
		int settlingTimeIndex = LJ_SETTLINGTIME_100uS;

		/// <summary>
		/// This is the ResolutionIndex value specified in the LabJack
		/// DataSheet. It must be a number from 0 to 8, where 0 is equivalent
		/// to 1.
		/// </summary>
		[JsonProperty, DefaultValue(0)]
		public int ResolutionIndex
		{
			get => resolutionIndex;
			set => Ensure(ref resolutionIndex, value);
		}
		int resolutionIndex = 0;

		/// <summary>
		/// Milliseconds between output signal changes. Default 1.
		/// </summary>
		[JsonProperty, DefaultValue(1)]
		public int OutputPaceMilliseconds
		{
			get => outputPace;
			set => Ensure(ref outputPace, value);
		}
		int outputPace = 1;

		#endregion Settings

		#region Retrieved device values

		/// <summary>
		/// The hardware version reported by the device.
		/// </summary>
		public double HardwareVersion
		{
			get => hardwareVersion;
			protected set => Ensure(ref hardwareVersion, value);
		}
		double hardwareVersion;
		double IDevice.HardwareVersion
		{
			get => HardwareVersion;
			set => HardwareVersion = value;
		}

		/// <summary>
		/// The serial number reported by the device.
		/// </summary>
		public double SerialNumber
		{
			get => serialNumber;
			protected set => Set(ref serialNumber, value);
		}
		double serialNumber;
		double IDevice.SerialNumber
		{
			get => SerialNumber;
			set => SerialNumber = value;
		}

		/// <summary>
		/// The firmware version reported by the device.
		/// </summary>
		public double FirmwareVersion
		{
			get => firmwareVersion;
			protected set => Set(ref firmwareVersion, value);
		}
		double firmwareVersion;
		double IDevice.FirmwareVersion
		{
			get => FirmwareVersion;
			set => FirmwareVersion = value;
		}

		/// <summary>
		/// The bootloader version reported by the device.
		/// </summary>
		public double BootloaderVersion
		{
			get => bootloaderVersion;
			protected set => Set(ref bootloaderVersion, value);
		}
		double bootloaderVersion;
		double IDevice.BootloaderVersion
		{
			get => BootloaderVersion;
			set => BootloaderVersion = value;
		}

		/// <summary>
		/// The product id reported by the device.
		/// </summary>
		public double ProductId
		{
			get => productID;
			protected set => Set(ref productID, value);
		}
		double productID;
		double IDevice.ProductID
		{
			get => ProductId;
			set => ProductId = value;
		}

		/// <summary>
		/// The U6 Pro value reported by the device.
		/// </summary>
		public double U6Pro
		{
			get => u6Pro;
			protected set => Set(ref u6Pro, value);
		}
		double u6Pro;
		double IDevice.U6Pro
		{
			get => U6Pro;
			set => U6Pro = value;
		}


		/// <summary>
		/// The amount of data currently buffered by the device.
		/// This value should stay near zero in normal operation.
		/// </summary>
		public int StreamingBacklogHardware
		{
			get => streamingBacklogHardware;
			protected set => Set(ref streamingBacklogHardware, value);
		}
		int streamingBacklogHardware;
		int IDevice.StreamingBacklogHardware
		{
			get => StreamingBacklogHardware;
			set => StreamingBacklogHardware = value;
		}

		/// <summary>
		/// The amount of data currently buffered by the UD driver.
		/// If this is increasing over time, the application is not 
		/// retrieving the data often enough.
		/// </summary>
		public int StreamingBacklogDriver
		{
			get => streamingBacklogDriver;
			protected set => Set(ref streamingBacklogDriver, value);
		}
		int streamingBacklogDriver;
		int IDevice.StreamingBacklogDriver
		{
			get => StreamingBacklogDriver;
			set => StreamingBacklogDriver = value;
		}


		/// <summary>
		/// The number of stream samples per packet. 
		/// Range 1..25. Default 25.
		/// </summary>
		public int StreamSamplesPerPacket
		{
			get => streamSamplesPerPacket;
			protected set => Set(ref streamSamplesPerPacket, value);
		}
		int streamSamplesPerPacket;
		int IDevice.StreamSamplesPerPacket
		{
			get => StreamSamplesPerPacket;
			set => StreamSamplesPerPacket = value;
		}

		/// <summary>
		/// The number of stream reads per second. Default 25.
		/// </summary>
		public int StreamReadsPerSecond
		{
			get => streamReadsPerSecond;
			protected set => Set(ref streamReadsPerSecond, value);
		}
		int streamReadsPerSecond;
		int IDevice.StreamReadsPerSecond
		{
			get => StreamReadsPerSecond;
			set => StreamReadsPerSecond = value;
		}

		/// <summary>
		/// Stream scans received.
		/// </summary>
		public long ScansReceived
		{
			get => scansReceived;
			protected set => Set(ref scansReceived, value);
		}
		long scansReceived;
		long IDevice.ScansReceived
		{
			get => ScansReceived;
			set => ScansReceived = value;
		}

		#endregion Retrieved device values

		#region

		// These values are produced by the code, perhaps determined
		// by consequence of configuration settings, limitations, 
		// operating conditions, etc.

		/// <summary>
		/// How often to attempt retrieving stream data from the driver, in milliseconds.
		/// </summary>
		public int RetrievalInterval
		{
			get => retrievalInterval;
			protected set => Set(ref retrievalInterval, value);
		}
		int retrievalInterval;
		Stopwatch scanStopwatch = new Stopwatch();
		public long ScanMilliseconds
		{
			get => scanMilliseconds;
			protected set => Set(ref scanMilliseconds, value);
		}
		long scanMilliseconds;
		public int MinimumScanTime
		{
			get => minimumScanTime;
			protected set => Set(ref minimumScanTime, value);
		}
		int minimumScanTime;
		/// <summary>
		/// The minimum command response time is ~0.6 ms if the LabJack 
		/// goes through a USB2 hub to the USB2 host, i.e., 
		/// LJ -&gt; hub -&gt; host).
		/// If the LJ is connected directly to a USB2 host port, or if any USB
		/// component in the path is &lt; USB 2.0, the minimum CRT goes up to 4 ms.
		/// </summary>
		public int MinimumCommandResponseTime
		{
			get => minimumCommandResponseTime;
			protected set => Set(ref minimumCommandResponseTime, value);
		}
		int minimumCommandResponseTime = 1; // rounded up to the nearest ms
		public int MinimumStreamResponseTime
		{
			get => minimumStreamResponseTime;
			protected set => Set(ref minimumStreamResponseTime, value);
		}
		int minimumStreamResponseTime;
		public int ExpectedScanTime
		{
			get => expectedScanTime;
			protected set => Set(ref expectedScanTime, value);
		}
		int expectedScanTime;
		public int ExpectedStreamResponseTime
		{
			get => expectedStreamResponseTime;
			protected set => Set(ref expectedStreamResponseTime, value);
		}
		int expectedStreamResponseTime;

		#endregion


		public override string ToString()
		{
			StringBuilder sb = new StringBuilder($"{Name}: ");

			StringBuilder sb2 = new StringBuilder();
			sb2.Append($"\r\nHardware Version: {HardwareVersion}");
			sb2.Append($"\r\nSerial Number: {SerialNumber}");
			sb2.Append($"\r\nFirmware Version: {FirmwareVersion}");
			sb2.Append($"\r\nBootloader Version: {BootloaderVersion}");
			sb2.Append($"\r\nProduct ID: {ProductId}");
			sb2.Append($"\r\nU6 Pro: {(U6Pro != 0).YesNo()}");
			sb2.Append($"\r\nIsUp: {IsUp}");

			sb2.Append($"\r\nAnalog inputs: {ai.Count}");
			sb2.Append($"\r\nStream data length: {(streamData?.Length ?? 0)}");
			sb2.Append($"\r\nTarget settling time: {settlingTimes[SettlingTimeIndex] * 1000000} µs");

			sb2.Append($"\r\nRetrieval interval: {RetrievalInterval} ms (minimum: {MinimumRetrievalInterval} ms");
			sb2.Append($"\r\nScan frequency: {ScanFrequency}");
			sb2.Append($"\r\nResolution index: {ResolutionIndex}");

			double msr = maxSampleRates[ResolutionIndex];
			sb2.Append($"\r\nSettling time: ~{1000000 / msr} µs");
			sb2.Append($"\r\nMaximum samples per second: {msr}");

			sb2.Append($"\r\nExpected scan time: {ExpectedScanTime} ms (minimum: {MinimumScanTime} ms)");
			sb2.Append($"\r\nCommand-response time: ~{MinimumCommandResponseTime} ms");
			sb2.Append($"\r\nExpected stream response time: {ExpectedStreamResponseTime} ms (minimum: {MinimumStreamResponseTime} ms)");

			sb2.Append($"\r\nStream samples per packet: {StreamSamplesPerPacket}");
			sb2.Append($"\r\nStream reads per second: {StreamReadsPerSecond}");

			sb2.Append($"\r\nIs Streaming: {IsStreaming}");
			sb2.Append($"\r\nStream backlog (hardware): {StreamingBacklogHardware:3}");
			sb2.Append($"\r\nStream backlog (driver): {StreamingBacklogDriver:3}");
			sb2.Append($"\r\nData Acquired: {DataAcquired}");
			sb2.Append($"\r\nScans received: {ScansReceived}");
			sb2.Append($"\r\nMost recent retrieval period: {ScanMilliseconds:0.000} ms");
			sb2.Append($"\r\nServiceQ: {serviceQ.Count}");

			return sb.Append(Utility.IndentLines(sb2.ToString())).ToString();
		}

		#endregion Class interface properties and methods


		#region IDeviceManager

		protected override IManagedDevice FindSupportedDevice(string name)
		{
			if (Find<IAnalogInput>(name) is IAnalogInput ain)
				return ain;
			if (Find<IAnalogOutput>(name) is IAnalogOutput aout)
				return aout;
			if (Find<IDigitalInput>(name) is IDigitalInput din) 
				return din;
			if (Find<IDigitalOutput>(name) is IDigitalOutput dout)
				return dout;
			return null;
		}

		/// <summary>
		/// Enqueue a device for a service call.
		/// </summary>
		/// <param name="sender">The device needing service</param>
		protected override void DeviceConfigChanged(object sender, PropertyChangedEventArgs e)
		{
			if (!(sender is IManagedDevice d && Keys.ContainsKey(d)))
				return;

			var arg = e.PropertyName;
			if (LogEverything)
				Log?.Record($"Noticed {d.Name}'s {arg} event.");

			if (d is IDigitalOutput dout && arg == nameof(IDigitalOutput.Config.State))
			{
				serviceQ.Enqueue(new ObjectPair(d, dout.Config.State.IsOn()));
				StopWaiting();
			}
			else if (d is IAnalogOutput aout && arg == nameof(IAnalogOutput.Config.Voltage))
			{
				serviceQ.Enqueue(new ObjectPair(d, aout.Config.Voltage));
				StopWaiting();
			}
			else if (d is IAnalogInput &&
				(arg == nameof(IAnalogInput.Config.AnalogInputMode) ||
				 arg == nameof(IAnalogInput.Config.MaximumVoltage)))
			{
				streamConfigured = false;
			}
		}


        #endregion IDeviceManager

        #region State management

        #region State Manager
        protected override void ManageState()
		{
			try
			{
				if (!ConnectedToDaq)
					ConnectToDaq();
				if (ConnectedToDaq)
				{
					Stream();
					if (serviceQ.IsEmpty)
						CheckStatus();
					else
						SetOutput();
				}
				else if (LogEverything)
					Log?.Record("Not Connected");
			}
			catch (Exception e) { if (LogEverything) LogMessage(e.ToString()); }
			(this as IStateManager).StateLoopTimeout = StateLoopTimeout;
		}

		/// <summary>
		/// State-dependent StateLoop timeout
		/// </summary>
		protected int StateLoopTimeout
		{
			get
			{
				int timeout;
				//TODO this should be unnecessary. the serviceQ should not accept requests until the daq is connected.
				if (!ConnectedToDaq)
					timeout = IdleTimeout;
				else if (!serviceQ.IsEmpty)
					timeout = OutputPaceMilliseconds;
				else if (!IsStreaming)
					timeout = IdleTimeout;
				else if (StreamingBacklogDriver < ai.Count)
					timeout = RetrievalInterval;    // Note: doesn't need to be precise
				else // falling behind
					timeout = OutputPaceMilliseconds;

				if (LogEverything)
				{
					var elapsed = scanStopwatch.IsRunning ? (int)scanStopwatch.ElapsedMilliseconds : 0;
					Log?.Record($"StateLoop timeout = {timeout}, scan timer = {elapsed}");
				}

				return timeout;
			}
		}

		#endregion State Manager

		U6 lj;                              // device handle
		LabJackUDException LJError
		{
			get => ljError;
			set
			{
				ljError = value;
				if (ljError == null) return;
				switch (ljError.LJUDError)
				{
					case LJUD.LJUDERROR.INVALID_DEVICE_TYPE:
					case LJUD.LJUDERROR.INVALID_HANDLE:
					case LJUD.LJUDERROR.DEVICE_NOT_OPEN:
					case LJUD.LJUDERROR.LABJACK_NOT_FOUND:
					case LJUD.LJUDERROR.COMM_FAILURE:
					case LJUD.LJUDERROR.USB_DRIVER_NOT_FOUND:
					case LJUD.LJUDERROR.INVALID_CONNECTION_TYPE:
					case LJUD.LJUDERROR.INVALID_MODE:
					case LJUD.LJUDERROR.DISCONNECT:
						lj = null;
						break;
					default:
						break;
				}
			}
		}
		LabJackUDException ljError;
		void HandleLabJackException(LabJackUDException e)
		{
			LJError = e;
			if (LogEverything) Log?.Record($"LabJack exception: {Error.ToString().TrimEnd()}");
		}
		void ClearLJError() => LJError = default;

		bool ConnectedToDaq => lj != null && Device.UpdatesReceived > 0;
		void ConnectToDaq()
		{
			if (LogEverything) Log?.Record($"Connecting...");
			try
			{
				lj = new U6(LJUD.CONNECTION.USB, $"{Config.LocalId}", false);
				Device.LocalId = (int)CheckStatus(LJUD.CHANNEL.LOCALID);
				Device.HardwareVersion = CheckStatus(LJUD.CHANNEL.HARDWARE_VERSION);
				Device.SerialNumber = CheckStatus(LJUD.CHANNEL.SERIAL_NUMBER);
				Device.FirmwareVersion = CheckStatus(LJUD.CHANNEL.FIRMWARE_VERSION);
				Device.BootloaderVersion = CheckStatus(LJUD.CHANNEL.BOOTLOADER_VERSION);
				Device.ProductID = CheckStatus(LJUD.CHANNEL.PRODUCTID);
				Device.U6Pro = CheckStatus(LJUD.CHANNEL.U6_PRO);
				Device.UpdatesReceived++;

				if (LogEverything)
					Log?.Record("\r\n\t" +
						"HardwareVersion: " + HardwareVersion.ToString() + "\r\n\t" +
						"SerialNumber: " + SerialNumber.ToString() + "\r\n\t" +
						"FirmwareVersion: " + FirmwareVersion.ToString() + "\r\n\t" +
						"BootloaderVersion: " + BootloaderVersion.ToString() + "\r\n\t" +
						"ProductID: " + ProductId.ToString() + "\r\n\t" +
						"U6Pro: " + U6Pro.ToString()
						);

				ClearLJError();
				if (LogEverything) Log?.Record($"Connected.");
			}
			catch
			{
				LJError = new LabJackUDException(LJUD.LJUDERROR.DEVICE_NOT_OPEN);
				return;
			}
		}

		ConcurrentQueue<ObjectPair> serviceQ = new ConcurrentQueue<ObjectPair>();

		double CheckStatus(LJUD.CHANNEL configChannel)
		{
			double ljResult = 0;
			LJUD.eGet(lj.ljhandle, LJUD.IO.GET_CONFIG, configChannel, ref ljResult, 0);
			return ljResult;
		}

		void CheckStatus()
		{
			if (Stopping) return;
			if (LogEverything) Log?.Record("Checking status...");
			Device.StreamingBacklogHardware = (int)CheckStatus(LJUD.CHANNEL.STREAM_BACKLOG_COMM);
			Device.StreamingBacklogDriver = (int)CheckStatus(LJUD.CHANNEL.STREAM_BACKLOG_UD);
			Device.UpdatesReceived++;
			if (LogEverything)
			{
				Log?.Record($"...UpdatesReceived: {UpdatesReceived}; Backlogs: {StreamingBacklogHardware} HW, {StreamingBacklogDriver} SW");
			}
		}

		bool SetOutput()
		{
			if (LogEverything) Log?.Record("Setting outputs...");
			if (!ConnectedToDaq || !serviceQ.TryDequeue(out ObjectPair op))
				return false;
			if (!(op.x is IManagedDevice d)) return false;
			if (!Keys.ContainsKey(d)) return false;
			int ch = ExtractChannelNumber(Keys[d]);
			if (ch < 0) return false;

			if (d is IDigitalOutput dout)
			{
				// digital output channel numbers 0..7 are FIO0..FIO7
				var value = dout.Config.State.IsOn() ? 1 : 0;
				var ljError = LJUD.eDO(lj.ljhandle, ch, value);
				if (ljError != LJUD.LJUDERROR.NOERROR)
					HandleLabJackException(new LabJackUDException(ljError));
				dout.Device.OnOffState = (value != 0).ToOnOffState();
				dout.Device.UpdatesReceived++;
			}
			else if (d is IAnalogOutput aout)
			{
				var value = aout.Config.Voltage;
				var ljError = LJUD.eDAC(lj.ljhandle, ch, value, 0, 0, 0);
				if (ljError != LJUD.LJUDERROR.NOERROR)
					HandleLabJackException(new LabJackUDException(ljError));
				aout.Device.Voltage = value;
				aout.Device.UpdatesReceived++;
			}
			else
				return false;

			return true;
		}

        #region Streaming

        // Determine the highest usable ResolutionIndex for a given settling time
        int determineResolutionIndex(double settlingTime)
		{
			for (int i = intersampleDelays.Length - 1; i > 0; --i)
				if (intersampleDelays[i] < settlingTime)
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

		bool streamConfigured = false;
		void ConfigureStream()
		{
			if (LogEverything) Log?.Record("Configuring...");
			try
			{
				streamConfigured = IsStreaming = DataAcquired = false;

				ai.Clear();
				var gnd = -1;
				foreach (var d in Devices.Values)
				{
					if (d is IAnalogInput ai)
					{
						this.ai.Add(ai);
						if (gnd < 0 && d.Name == "GND")
							gnd = ExtractChannelNumber(Keys[d]);
					}
				}
				if (gnd < 0)
					throw new NotSupportedException("At least one analog input must be connected to, and named, GND");

				// room for grounding the "input" after every real channel
				int ainCount = ai.Count * 2;
				streamData = new double[ainCount];

				// Start with the minimum resolution index that would achieve the target settling time
				int minResolutionIndex = determineResolutionIndex(settlingTimes[SettlingTimeIndex]);

				// Find the number of milliseconds required to complete 1 scan of all the analog inputs
				MinimumScanTime = scanTimeMilliseconds(ainCount, minResolutionIndex);
				MinimumStreamResponseTime = MinimumScanTime + MinimumCommandResponseTime;
				int targetScanTime = MinimumStreamResponseTime + 3; //	provide a few ms idle time for unscheduled activities

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
				// corresponds to the maximum possible intersample delay.
				double resolutionIndex = determineResolutionIndex(1 / scanFrequency / ainCount);
				resolutionIndex = 0;
				// Cancel any streaming in progress
				try { LJUD.ePut(lj.ljhandle, LJUD.IO.STOP_STREAM, 0, 0, 0); }
				catch { }

				// Clear any existing list of streamed channels
				LJUD.ePut(lj.ljhandle, LJUD.IO.CLEAR_STREAM_CHANNELS, 0, 0, 0);

				// define and configure the channels to be streamed
				foreach (var ai in ai)
					AddToStream(ai, gnd);

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
					retrievalFrequency * ainCount, 0, 0);

				//Configure reads to retrieve the requested amount of data, or return nothing.
				LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_CONFIG, LJUD.CHANNEL.STREAM_WAIT_MODE,
					(double)LJUD.STREAMWAITMODES.ALL_OR_NONE, 0, 0);

				// If necessary, reduce the packet size to prevent the UD driver from 
				// waiting for multiple scans to complete before retrieving data from the u6.
				if (ainCount < 25)
					LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_CONFIG,
						LJUD.CHANNEL.STREAM_SAMPLES_PER_PACKET, ainCount, 0, 0);

				// Execute the pending requests
				LJUD.GoOne(lj.ljhandle);
				CheckLjForErrors();
			}
			catch (LabJackUDException e)
			{
				LogMessage("Labjack Configuration Error:" + "\r\n" + e.ToString());
				//handleLabJackException(e); 
			}
			streamConfigured = true;
			if (LogEverything) Log?.Record("...Configured.");
		}

		void AddToStream(IAnalogInput ai, int gnd)
		{
			var ch = ExtractChannelNumber(Keys[ai as IManagedDevice]);

			LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_AIN_RANGE,
				(LJUD.CHANNEL)ch, LJVoltageRange(ai), 0, 0);

			if (ai.Config.AnalogInputMode == AnalogInputMode.Differential)
				LJUD.AddRequest(lj.ljhandle, LJUD.IO.ADD_STREAM_CHANNEL_DIFF,
					ch, 0, NegTerminal(ch), 0);
			else    // single-ended
				LJUD.AddRequest(lj.ljhandle, LJUD.IO.ADD_STREAM_CHANNEL,
					ch, 0, 0, 0);

			// connect ADC to GND after every channel
			LJUD.AddRequest(lj.ljhandle, LJUD.IO.PUT_AIN_RANGE,
				gnd, (double)LJUD.RANGES.BIP10V, 0, 0);
			LJUD.AddRequest(lj.ljhandle, LJUD.IO.ADD_STREAM_CHANNEL,
				gnd, 0, 0, 0);
		}

		// Sets the daq's PGIA gain for the given meter's analog input.
		// The PGIA gain is defined by the max voltage to be sensed,
		// like the Range setting on a multimeter. 
		// +/- 10V => Gain = 1
		// +/- 1V => Gain = 10
		// +/- 0.1V => Gain = 100
		// +/- 0.01V => Gain = 1000
		double LJVoltageRange(IAnalogInput ai)
		{
			if (ai.Config.MaximumVoltage <= 0.01)
				return (double)LJUD.RANGES.BIPP01V;
			else if (ai.Config.MaximumVoltage <= 0.1)
				return (double)LJUD.RANGES.BIPP1V;
			else if (ai.Config.MaximumVoltage <= 1)
				return (double)LJUD.RANGES.BIP1V;
			else
				return (double)LJUD.RANGES.BIP10V;
		}

		// Given the positive terminal of a LabJack differential analog input,
		// returns the corresponding negative terminal.
		int NegTerminal(int posTerminal)
		{
			if (posTerminal < 16)
				return posTerminal + 1;
			else return posTerminal + 8;
		}

		void CheckLjForErrors()
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
						HandleLabJackException(e);
				}
			}
			ClearLJError();
		}

		/// <summary>
		/// refreshed whenever stream is configured
		/// </summary>
		List<IAnalogInput> ai = new List<IAnalogInput>();
		double[] streamData;

		void StartStreaming()
		{
			if (LogEverything) Log?.Record("Starting stream...");
			try
			{
				if (!streamConfigured)
					ConfigureStream();

				if (!streamConfigured)
					return;

				var ljError = LJUD.eGet(lj.ljhandle,
					LJUD.IO.START_STREAM, 0, ref ljDummyDouble, 0);

				Device.ScanFrequency = (int)CheckStatus(LJUD.CHANNEL.STREAM_SCAN_FREQUENCY);
				Device.StreamSamplesPerPacket = (int)CheckStatus(LJUD.CHANNEL.STREAM_SAMPLES_PER_PACKET);
				Device.StreamReadsPerSecond = (int)CheckStatus(LJUD.CHANNEL.STREAM_READS_PER_SECOND);
				Device.ResolutionIndex = (int)CheckStatus(LJUD.CHANNEL.AIN_RESOLUTION);
				Device.UpdatesReceived++;
				ExpectedScanTime = scanTimeMilliseconds(streamData.Length, ResolutionIndex);
				ExpectedStreamResponseTime = ExpectedScanTime + MinimumCommandResponseTime;

				if (ljError == LJUD.LJUDERROR.NOERROR)
					ClearLJError();
				else
					throw new LabJackUDException(ljError);
			}
			catch (LabJackUDException e) { HandleLabJackException(e); }

			IsStreaming = true;
			scanStopwatch.Restart();
			if (LogEverything) Log?.Record("...Streaming.");
		}

		void Stream()
		{
			if (LogEverything) Log?.Record($"Checking stream...");
			if (Stopping)
			{
				if (IsStreaming)
				{
					try { LJUD.ePut(lj.ljhandle, LJUD.IO.STOP_STREAM, 0, 0, 0); }
					catch { }
					DataAcquired = false;
					IsStreaming = false;
				}
				return;
			}

			if (!streamConfigured || !IsStreaming)
			{
				StartStreaming();
				return;
			}

			if (scanStopwatch.IsRunning && scanStopwatch.ElapsedMilliseconds < RetrievalInterval)
				return;

			double scans = 1;   // Attempt to retrieve one scan

			try
			{
				var ljError = LJUD.eGetPtr(lj.ljhandle, LJUD.IO.GET_STREAM_DATA,
					LJUD.CHANNEL.ALL_CHANNELS, ref scans, streamData);

				if (ljError != LJUD.LJUDERROR.NOERROR &&
						ljError != LJUD.LJUDERROR.NOTHING_TO_STREAM)
					HandleLabJackException(new LabJackUDException(ljError));
			}
			catch { IsStreaming = false; }

			if (LogEverything) 
				Log?.Record($"Retrieved {Utility.ToUnitsString(scans, "scan")}");

			// Transfer the voltages to the analog input devices.
			if (scans > 0)
			{
//				Meter.MetersLog?.Record("Voltages received from DAQ");
				
				ScanMilliseconds = scanStopwatch.ElapsedMilliseconds;
				scanStopwatch.Restart();

				Device.ScansReceived += (long)scans;
				++Device.UpdatesReceived;

				for (int i = 0; i < ai.Count; i++)
					ai[i].Device.Voltage = streamData[i + i];
				DataAcquired = true;

				if (LogEverything) Log?.Record("...Stream data received.");
			}
		}

		#endregion Streaming

		#endregion State management
	}
}
