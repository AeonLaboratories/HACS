using HACS.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Utilities;

namespace HACS.Components
{
	public interface INamedValue : INamedObject, IValue { }
	public interface IVoltage { double Voltage { get; } }
	public interface ITemperature { double Temperature { get; } }
	public interface IPressure { double Pressure { get; } }
	public interface IOnOffState { OnOffState OnOffState { get; } }
	public interface IIsOn
	{
		bool IsOn { get; }
		bool IsOff { get; }
	}

	/// <summary>
	/// The target value for a property that is controlled by a device.
	/// </summary>
	public interface ISetpoint { double Setpoint { get; set; } }

	/// <summary>
	/// The value of a property at which a device should change state.
	/// </summary>
	public interface ISwitchpoint { double Switchpoint { get; set; } }

	/// <summary>
	/// Operate the normally-automatic device in manual mode.
	/// </summary>
	public interface IManualMode { bool ManualMode { get; set; } }

	/// <summary>
	/// A device's controlled power level.
	/// </summary>
	public interface IPowerLevel { double PowerLevel { get; } }

	public interface IStopAction { StopAction StopAction { get; set; } }
	public interface ISwitchable : IOnOffState
	{
		/// <summary>
		/// Turn the device on.
		/// </summary>
		bool TurnOn();

		/// <summary>
		/// Turn the device off.
		/// </summary>
		bool TurnOff();

		/// <summary>
		/// Turns the device on or off according to the parameter.
		/// </summary>
		/// <param name="on">true => on, false => off</param>
		bool TurnOnOff(bool on);
	}

	public interface IOperatable
	{
		/// <summary>
		/// A list of the operations or actions a device can perform.
		/// </summary>
		List<string> Operations { get; }

		/// <summary>
		/// Make the device do the requested operation.
		/// </summary>
		/// <param name="operation"></param>
		void DoOperation(string operation);
	}


	//
	// IHacsComponents
	//
	public interface IStateManager : IHacsComponent
	{
		/// <summary>
		/// The device is ready to do work.
		/// </summary>
		bool Ready { get; }

		/// <summary>
		/// The device has unfinished tasks.
		/// </summary>
		bool HasWork { get; }

		/// <summary>
		/// Ready and doing work.
		/// </summary>
		bool Busy { get; }

		/// <summary>
		/// The StateManager is Stopping.
		/// </summary>
		bool Stopping { get; }

		/// <summary>
		/// The StateManager is Stopped.
		/// </summary>
		new bool Stopped { get; }

		void LogMessage(string message);
		LogFile Log { get; set; }
		bool LogEverything { get; set; }

		/// <summary>
		/// The "worker" method of the StateManager, called whenever its
		/// StateLoop times out.
		/// </summary>
		Action ManageState { get; set; }
		/// <summary>
		/// Maximum time (milliseconds) for idle state manager to wait before doing something.
		/// </summary>
		int IdleTimeout { get; set; }

		/// <summary>
		/// Maximum time (milliseconds) for the state manager to wait before doing something.
		/// This is set to IdleTimeout by default.
		/// </summary>
		int StateLoopTimeout { get; set; }

		/// <summary>
		/// This method interrupts the timed wait at the end of the StateLoop.
		/// </summary>
		void StopWaiting();
	}

	public interface IStateManager<TargetStates, States> : IStateManager
	{
		TargetStates TargetState { get; set; }
		States State { get; }
		long MillisecondsInState { get; }
		double MinutesInState { get; }
		void ChangeState(TargetStates targetState);
		void ChangeState(TargetStates targetState, Predicate<StateManager<TargetStates, States>> predicate);
	}

	public interface IColdfinger : IStateManager<Coldfinger.TargetStates, Coldfinger.States>
	{
		IValve LNValve { get; set; }
		IValve AirValve { get; set; }
		ILNManifold LNManifold { get; set; }
		IThermometer LevelSensor { get; set; }
		IHacsComponent AirThermometer { get; set; }

		int FrozenTemperature { get; set; }
		string Trickle { get; set; }
		int FreezeTrigger { get; set; }
		int RaiseTrigger { get; set; }
		int MaximumSecondsLNFlowing { get; set; }
		int SecondsToWaitAfterRaised { get; set; }
		double NearAirTemperature { get; set; }
		bool Thawing { get; }
		bool Frozen { get; }
		bool Raised { get; }
		bool IsNearAirTemperature { get; }
		bool IsActivelyCooling { get; }
		bool Thawed { get; }
		double Temperature { get; }
		double AirTemperature { get; }
		double Target { get; }
		void Standby();
		void Freeze();
		void Raise();
		void Thaw();
		void EnsureState(Coldfinger.TargetStates state);
		void FreezeWait();
		void RaiseLN();
		void WaitForLNpeak();
		Action SlowToFreeze { get; set; }
	}

	public interface IVTColdfinger : IStateManager<VTColdfinger.TargetStates, VTColdfinger.States>,
		IIsOn, ISwitchable, IStopAction
	{
		IHeater Heater { get; set; }
		IColdfinger Coldfinger { get; set; }
		IThermometer TopThermometer { get; set; }
		IThermometer WireThermometer { get; set; }
		int WireTemperatureLimit { get; set; }
		double Setpoint { get; set; }
		double MaximumHeaterPower { get; set; }
		IPidSetup HeaterPid { get; set; }
		double MaximumWarmHeaterPower { get; set; }
		IPidSetup WarmHeaterPid { get; set; }
		int ColdTemperature { get; set; }
		int CleanupTemperature { get; set; }
		string HeaterOnTrickle { get; set; }
		string HeaterOffTrickle { get; set; }
		bool Frozen { get; }
		double Temperature { get; }
		double ColdfingerTemperature { get; }

		void Standby();
		void Thaw();
		void Freeze();
		void Regulate();
		void Regulate(double setpoint);
		void EnsureState(VTColdfinger.TargetStates state);

	}

	// TODO: make this a DeviceManager
	public interface ILNManifold :
		IStateManager<LNManifold.TargetStates, LNManifold.States>,
		IIsOn, ISwitchable, IStopAction
	{
		IValve LNSupplyValve { get; set; }
		IMeter Liters { get; set; }
		IThermometer LevelSensor { get; set; }
		IThermometer OverflowSensor { get; set; }
		int OverflowTrigger { get; set; }
		int MinimumLiters { get; set; }
		int TargetTemperature { get; set; }
		int FillTrigger { get; set; }
		int SecondsSlowToFill { get; set; }
		int ColdTemperature { get; set; }
		bool IsCold { get; }

		Action OverflowDetected { get; set; }
		Action SlowToFill { get; set; }

		bool StayingActive { get; }
		bool OverflowIsDetected { get; }
		int SecondsFilling { get; }
		bool IsSlowToFill { get; }
		bool SupplyEmpty { get; }
		void Monitor();
		void StayActive();
		void ForceFill();

	}

	public interface ISerialController : IStateManager
	{
		SerialDevice SerialDevice { get; set; }
		bool LogCommands { get; set; }
		bool LogResponses { get; set; }
		bool TokenizeCommands { get; set; }
		bool IgnoreUnexpectedResponses { get; set; }
		int ResponseTimeout { get; set; }
		Func<SerialController.Command> SelectServiceHandler { get; set; }
		Func<string, int, bool> ResponseProcessor { get; set; }
		event EventHandler LostConnection;
		bool Responsive { get; }
		int TooManyResponseTimeouts { get; set; }
		bool Idle { get; }
		bool Free { get; }
		uint CommandCount { get; }
		uint ResponseCount { get; }
		bool WaitForIdle(int timeout = -1);
		string Escape(string s);
		bool Hurry { get; set; }
		string ServiceCommand { get; }
		string CommandMessage { get; }
		int ResponseTimeouts { get; }
		string Response { get; }
	}

	public interface ISensor : IValue, INotifyPropertyChanged { }

	public interface IDetector : INotifyPropertyChanged
	{
		/// <summary>
		/// The condition that raises the Detected event.
		/// </summary>
		Func<bool> Condition { get; set; }

		/// <summary>
		/// Raised whenever Condition becomes "met." I.e.,
		/// when Condition's evaluation changes to true.
		/// </summary>
		PropertyChangedEventHandler Detected { get; set; }

		/// <summary>
		/// The Condition was met when it was last checked.
		/// Accessing this value does not cause Condition to be
		/// re-evaluated. Returns null if Condition is null.
		/// </summary>
		bool State { get; }

		/// <summary>
		/// If Sensor is set, Condition is re-evaluted whenever 
		/// Sensor's Value changes. 
		/// </summary>
		Sensor Sensor { get; set; }

		/// <summary>
		/// Restarts whenever State changes.
		/// </summary>
		Stopwatch StateStopwatch { get; }

		/// <summary>
		/// The time elapsed since the last State change;
		/// </summary>
		long MillisecondsInState { get; }

		/// <summary>
		/// Evaluate Condition and raise the Detected event if its
		/// State changes to true.
		/// </summary>
		void Update();

		bool WaitForCondition(int timeout, int interval);
	}

	public interface IDetectorSwitch : INotifyPropertyChanged
	{
		/// <summary>
		/// The sensor whose Value to monitor.
		/// </summary>
		ISensor Sensor { get; set; }

		/// <summary>
		/// The Switch to operate in accordance with the Switchpoint.
		/// </summary>
		ISwitch Switch { get; set; }

		/// <summary>
		/// The comparator for the Sensor Value.
		/// </summary>
		double? Switchpoint { get; set; }

		/// <summary>
		/// The rule for detection. When the Sensor.Value reaches
		/// this condition, the Switch is changed to the 
		/// DetectedState. Note that the converse does NOT occur,
		/// i.e., the Switch state is not changed by the
		/// Sensor.Value no longer meeting the the condition. If
		/// bi-directional operation is needed, use two 
		/// DetectorSwitches.
		/// </summary>
		DetectorSwitch.RuleCode SwitchpointRule { get; set; }

		/// <summary>
		/// What to do with the Switch when the SwitchpointRule
		/// is detected. Note that the Switch state is not changed
		/// when the 
		/// </summary>
		OnOffState DetectedState { get; set; }

	}

	public interface IXostat : INotifyPropertyChanged
	{
		/// <summary>
		/// The sensor whose Value to monitor.
		/// </summary>
		ISensor Sensor { get; set; }

		/// <summary>
		/// The switch to control based on the Sensor.Value
		/// and the OnSwitchpoint and OffSwitchpoint settings.
		/// </summary>
		ISwitch Switch { get; set; }

		/// <summary>
		/// The Sensor.Value at which to turn the switch on.
		/// </summary>
		double? OnSwitchpoint { get; set; }

		/// <summary>
		/// The Sensor.Value at which to turn the switch off.
		/// </summary>
		double? OffSwitchpoint { get; set; }

	}


	//
	// IHacsDevices - devices with data that is updated asynchronously
	// from outside the class.
	//
	public interface IHacsDevice : IHacsComponent
	{
		HacsDevice.IDevice Device { get; }
		HacsDevice.IConfig Config { get; }

		long UpdatesReceived { get; }
		void OnPropertyChanged(object sender, PropertyChangedEventArgs e);
		PropertyChangedEventHandler ConfigChanged { get; set; }
		void OnConfigChanged(object sender, PropertyChangedEventArgs e);
	}

	public interface IMeter : IHacsDevice, IDoubleUpdatable
	{
		new Meter.IDevice Device { get; }
		new Meter.IConfig Config { get; }

		/// <summary>
		/// The symbol representation of the output Value's units.
		/// </summary>
		string UnitSymbol { get; set; }

		/// <summary>
		///  The smallest meaningful (detectable) Value that is 
		///  distinctly different from zero.
		/// </summary>
		double Sensitivity { get; set; }

		/// <summary>
		/// The smallest meaningful difference between two output Values.
		/// </summary>
		double Resolution { get; set; }

		/// <summary>
		/// Whether the Resolution depends on the magnitude of the Value.
		/// </summary>
		bool ResolutionIsProportional { get; set; }

		DigitalFilter Filter { get; set; }

		OperationSet Conversion { get; set; }

		/// <summary>
		/// The rate at which the Value is changing (units per second).
		/// </summary>
		RateOfChange RateOfChange { get; set; }

		/// <summary>
		/// A rate of change. Value is considered stable if
		/// |Value's RateOfChange| is less than the specified
		/// number.
		/// </summary>
		double Stable { get; set; }

		/// <summary>
		/// Whether the value is stable.
		/// </summary>
		bool IsStable { get; }

		/// <summary>
		/// A rate of change, usually negative, below which Value is considered to be falling
		/// </summary>
		double Falling { get; set; }

		/// <summary>
		/// Whether the value is falling.
		/// </summary>
		bool IsFalling { get; }

		/// <summary>
		/// A rate of change, usually positive, above which Value is considered to be rising
		/// </summary>
		double Rising { get; set; }

		/// <summary>
		/// Whether the value is rising.
		/// </summary>
		bool IsRising { get; }

		bool OverRange { get; }
		bool UnderRange { get; }

		void WaitForStable(int seconds);
		double WaitForAverage(int seconds);
		void ZeroNow();
		bool Zeroing { get; }
	}

	public interface IVoltmeter : IMeter, IVoltage
	{
		new Voltmeter.IDevice Device { get; }
		new Voltmeter.IConfig Config { get; }

		double MaximumVoltage { get; set; }
		double MinimumVoltage { get; set; }
		new bool OverRange { get; }
		new bool UnderRange { get; }
	}

	public interface IManometer : IMeter, IPressure
	{
		new Manometer.IDevice Device { get; }
		new Manometer.IConfig Config { get; }

		new double Pressure { get; }
	}

	public interface IThermometer : IMeter, ITemperature
	{
		new Thermometer.IDevice Device { get; }
		new Thermometer.IConfig Config { get; }

		new double Temperature { get; }
	}

	public interface IThermocouple : IThermometer
	{
		new Thermocouple.IDevice Device { get; }
		new Thermocouple.IConfig Config { get; }

		ThermocoupleType Type { get; set; }
	}

	public interface IPyrometer : IThermometer, ISwitch
	{
		new Pyrometer.IDevice Device { get; }
		new Pyrometer.IConfig Config { get; }

		byte Address { get; set; }
		int LaserCooldownSeconds { get; set; }
		int LaserOnMaxSeconds { get; set; }
		double Emissivity { get; set; }
		double RatioCorrection { get; set; }
		double Transmission { get; set; }
		Pyrometer.TimeCode ResponseTime { get; set; }
		double MillimetersMeasuringDistance { get; set; }
		double TemperatureRangeMinimum { get;  }
		double TemperatureRangeMaximum { get;  }
		double MillimetersFocalLength { get;  }
		double MillimetersFieldDiameterMinimum { get; set; }
		double MillimetersAperture { get; set; }
		int StatusByte { get; }
		double InternalTemperature { get; }
		SerialController SerialController { get; set; }
		double MeasuringFieldDiameter { get; }

	}



	// A HacsDevice that may be in an On, Off or Unknown state.
	public interface IOnOff : IOnOffState, IIsOn, IHacsDevice
	{
		new OnOff.IDevice Device { get; }
		new OnOff.IConfig Config { get; }

		long MillisecondsOn { get; }
		long MillisecondsOff { get; }
		long MillisecondsInState { get; }
	}

	// A HacsDevice that can be turned on or off.
	public interface ISwitch : IOnOff, ISwitchable
	{
		new Switch.IDevice Device { get; }
		new Switch.IConfig Config { get; }

		SwitchState State { get; set; }
		StopAction StopAction { get; set; }
	}

	/// <summary>
	/// A device that autonomously controls an output to maintain a sensed value at a given setpoint.
	/// </summary>
	public interface IAuto : ISwitch, ISetpoint
	{
		new Auto.IDevice Device { get; }
		new Auto.IConfig Config { get; }

		/// <summary>
		/// Turns the device on with the given setpoint.
		/// </summary>
		/// <param name="setpoint"></param>
		void TurnOn(double setpoint);
	}

	/// <summary>
	/// A device capable of automatic temperature control.
	/// </summary>
	public interface IOven : IThermometer, IAuto
	{
		new Oven.IDevice Device { get; }
		new Oven.IConfig Config { get; }
	}

	public interface ITubeFurnace : IOven
	{
		new TubeFurnace.IDevice Device { get; }
		new TubeFurnace.IConfig Config { get; }

		SerialController SerialController { get; set; }
		double TimeLimit { get; set; }
		bool UseTimeLimit { get; set; }
		double RampingSetpoint { get; }
		double MinutesInState { get; }
		double MinutesOn { get; }
		double MinutesOff { get; }
		new bool TurnOn();
		new bool TurnOff();
		void TurnOn(double setpoint, double minutes);
		bool Ready { get; }
	}
	public interface IEurotherm818Furnace : ITubeFurnace
	{
		new Eurotherm818Furnace.IDevice Device { get; }
		new Eurotherm818Furnace.IConfig Config { get; }
		string InstrumentId { get; set; }
		int OutputPowerLimit { get; set; }
		int Error { get; }
		int WorkingSetpoint { get; }
		int OutputPower { get; }
	}

	public interface IEurothermFurnace : ITubeFurnace
	{
		new EurothermFurnace.IDevice Device { get; }
		new EurothermFurnace.IConfig Config { get; }

		EurothermFurnace.SetpointRateLimitUnitsCode SetpointRateLimitUnits { get; set; }
		int SetpointRateLimit { get; set; }
		int OutputRateLimit { get; set; }
		int ControlOutput { get; set; }
		int WorkingOutput { get; }
		EurothermFurnace.AutoManualCode OperatingMode { get; }
		bool ContactorDisengaged { get; set; }
		new void TurnOn(double setpoint);
	}

	public interface IMtiFurnace : ITubeFurnace 
	{
		new MtiFurnace.IDevice Device { get; }
		new MtiFurnace.IConfig Config { get; }

		byte InstrumentId { get; set; }
	}

	public interface IInductionFurnace : ITubeFurnace 
	{
		new InductionFurnace.IDevice Device { get; }
		new InductionFurnace.IConfig Config { get; }

		IPyrometer Pyrometer { get; set; }
		int MinimumControlledTemperature { get; }

		int PowerLevel { get; set; }
		int PowerLimit { get; }
		int Error { get; }
		int Voltage { get; }
		double Current { get; }
		int Frequency { get; }
		InductionFurnace.ControlModeCode ControlMode { get; }
		char DeviceAddress { get; }
		char HostAddress { get; }
		int Status { get; }
		string InterfaceBoardRevision { get; }

		string ErrorMessage();
		string ErrorMessage(int errorCode);

	}

	/// <summary>
	/// An automatic device that can be operated in manual mode.
	/// </summary>
	public interface IAutoManual : IAuto, IManualMode, IPowerLevel
	{
		new AutoManual.IDevice Device { get; }
		new AutoManual.IConfig Config { get; }

		new double PowerLevel { get; set; }
		double MaximumPowerLevel { get; set; }

		/// <summary>
		/// Set the operating mode to Auto.
		/// The PowerLevel is managed by the device to produce a desired setpoint.
		/// </summary>
		void Auto();

		/// <summary>
		/// Set the operating mode to Manual.
		/// The PowerLevel is set to a specified fixed value.
		/// </summary>
		void Manual();

		/// <summary>
		/// Set the power level to the specified value and enter Manual mode. 
		/// </summary>
		/// <param name="powerLevel">Power level [0..100%]</param>
		void Manual(double powerLevel);

		/// <summary>
		/// Enter Manual mode, keeping the power level at its current value.
		/// </summary>
		void Hold();
	}

	/// <summary>
	/// An oven that can be operated manually.
	/// </summary>
	public interface IHeater : IOven, IAutoManual
	{
		new Heater.IDevice Device { get; }
		new Heater.IConfig Config { get; }
	}

	public interface ISwitchedManometer : IManometer, ISwitch
	{
		new SwitchedManometer.IDevice Device { get; }
		new SwitchedManometer.IConfig Config { get; }

		int MillisecondsToValid { get; set; }
		int MinimumMillisecondsOff { get; set; }
		bool Valid { get; }
	}

	public interface IDualManometer : ISwitchedManometer, IManualMode
	{
		new DualManometer.IDevice Device { get; }
		new DualManometer.IConfig Config { get; }

		IManometer HighPressureManometer { get; set; }
		ISwitchedManometer LowPressureManometer { get; set; }
		/// <summary>
		/// Maximum pressure to read exclusively from LowPressureManometer
		/// </summary>
		double MaximumLowPressure { get; set; }
		/// <summary>
		/// Minimum pressure to read exclusively from HighPressureManometer
		/// </summary>
		double MinimumHighPressure { get; set; }
		/// <summary>
		/// On/off switchpoint pressure for LowPressureManometer
		/// </summary>
		double SwitchpointPressure { get; set; }

		void UpdatePressure();
	}

	//
	// IManagedDevices
	//

	public interface IManagedDevice : IHacsDevice
	{
		new ManagedDevice.IDevice Device { get; }
		new ManagedDevice.IConfig Config { get; }
		IDeviceManager Manager { get; }
	}

	/// <summary>
	/// A device manager or controller that<br></br>
	///	1. maintains a dictionary of IManagedDevices;<br></br>
	///	2. provides each IManagedDevice's "Device" values, which<br></br>
	///		generally represent real-world conditions, typically 
	///		determined via hardware communications;<br></br>
	///	3. monitors ConfigChanged events from its IManagedDevices;<br></br>
	///	4. works to bring its IManagedDevice's Device properties into<br></br>
	///		accord with their Config properties.<br></br>
	/// </summary>
	public interface IDeviceManager : IManagedDevice, IStateManager
	{
		new DeviceManager.IDevice Device { get; }
		new DeviceManager.IConfig Config { get; }

		/// <summary>
		/// A dictionary of the devices managed by the IDeviceManager,
		/// indexed by unique string "keys", e.g., channel numbers or IDs.
		/// The key format is IDeviceManager-dependent.
		/// </summary>
		Dictionary<string, IManagedDevice> Devices { get; }
		/// <summary>
		/// A "reverse-lookup" dictionary, which returns the key
		/// for a managed device.
		/// </summary>
		Dictionary<IManagedDevice, string> Keys { get; }

		bool IsSupported(IManagedDevice d, string key);
		void Connect(IManagedDevice d, string key);
		void Disconnect(IManagedDevice d);
	}

	public interface ISerialDeviceManager : IDeviceManager
	{
		new SerialDeviceManager.IDevice Device { get; }
		new SerialDeviceManager.IConfig Config { get; }

		SerialController SerialController { get; set; }
	}

	// TODO: create base Daq class?
	public interface IDaq : IDeviceManager
	{
		//new Daq.IDevice Device { get; }
		//new Daq.IConfig Config { get; }

		/// <summary>
		/// The DAQ is operating, data has been received, and there is no error.
		/// </summary>
		bool IsUp { get; }

		/// <summary>
		/// The DAQ AnalogInput stream is running.
		/// </summary>
		bool IsStreaming { get; }

		/// <summary>
		/// DAQ stream data has been received.
		/// </summary>
		bool DataAcquired { get; }

		/// <summary>
		/// Full channel scans per second.
		/// </summary>
		int ScanFrequency { get; }

		/// <summary>
		/// A string describing the error, or null if there is no error. This 
		/// value persists until cleared, even if the fault is gone before then.
		/// </summary>
		string Error { get; }

		/// <summary>
		/// Resets Error to default.
		/// </summary>
		void ClearError();
	}

	public interface ILabJackU6 : IDaq
	{
		new LabJackU6.IDevice Device { get; }
		new LabJackU6.IConfig Config { get; }

		int LocalId { get; set; }
		int MinimumRetrievalInterval { get; set; }
		int SettlingTimeIndex { get; set; }
		int ResolutionIndex { get; set; }
		int OutputPaceMilliseconds { get; set; }
		double HardwareVersion { get; }
		double SerialNumber { get; }
		double FirmwareVersion { get; }
		double BootloaderVersion { get; }
		double ProductId { get; }
		double U6Pro { get; }
		int StreamingBacklogHardware { get; }
		int StreamingBacklogDriver { get; }
		int StreamSamplesPerPacket { get; }
		int StreamReadsPerSecond { get; }
		long ScansReceived { get; }
		int RetrievalInterval { get; }
		long ScanMilliseconds { get; }
		int MinimumScanTime { get; }
		int MinimumCommandResponseTime { get; }
		int MinimumStreamResponseTime { get; }
		int ExpectedScanTime { get; }
		int ExpectedStreamResponseTime { get; }
	}

	public interface IActuatorController : ISerialDeviceManager
	{
		new ActuatorController.IDevice Device { get; }
		new ActuatorController.IConfig Config { get; }
		int Channels { get; set; }
		SerialController AeonServo { get; set; }
		string Model { get; }
		string Firmware { get; }
		int SerialNumber { get; }
		int MinimumControlPulseWidthMicroseconds { get; }
		int MaximumControlPulseWidthMicroseconds { get; }
		int SelectedActuator { get; }
		double Voltage { get; }
		ActuatorController.OperationState State { get; }
		ActuatorController.ErrorCodes Errors { get; }
		ActuatorController.AeonServoErrorCodes AeonServoErrors { get; }
	}

	public interface IHC6ControllerB2 : ISerialDeviceManager
	{
		new HC6ControllerB2.IDevice Device { get; }
		new HC6ControllerB2.IConfig Config { get; }		
		bool InterferenceSuppressionEnabled { get; set; }
		string Model { get; }
		string Firmware { get; }
		int SerialNumber { get; }
		int SelectedHeater { get; }
		int SelectedThermocouple { get; }
		int AdcCount { get; }
		double ColdJunction0Temperature { get; }
		double ColdJunction1Temperature { get; }
		double ReadingRate { get; }
		HC6ControllerB2.ErrorCodes Errors { get; }
	}

	public interface ISwitchBank : ISerialDeviceManager
	{
		new SwitchBank.IDevice Device { get; }
		new SwitchBank.IConfig Config { get; }
		int Channels { get; set; }
		string Model { get; }
		string Firmware { get; }
		int SelectedSwitch { get; }
		SwitchBank.ErrorCodes Errors { get; }
	}


	public interface IManagedThermocouple : IManagedDevice, IThermocouple
	{
		new ManagedThermocouple.IDevice Device { get; }
		new ManagedThermocouple.IConfig Config { get; }
	}

	public interface IManagedHeater : IManagedDevice, IHeater
	{
		new ManagedHeater.IDevice Device { get; }
		new ManagedHeater.IConfig Config { get; }
	}

	public interface IManagedSwitch : IManagedDevice, ISwitch
	{
		new ManagedSwitch.IDevice Device { get; }
		new ManagedSwitch.IConfig Config { get; }
	}

	public interface IDigitalOutput : IManagedSwitch
	{
		new DigitalOutput.IDevice Device { get; }
		new DigitalOutput.IConfig Config { get; }
	}
	public interface IDigitalInput : IManagedDevice, IOnOff
	{
		new DigitalInput.IDevice Device { get; }
		new DigitalInput.IConfig Config { get; }

	}
	public interface IAnalogOutput : IManagedDevice
	{
		new AnalogOutput.IDevice Device { get; }
		new AnalogOutput.IConfig Config { get; }

		double Voltage { get; set; }
		long MillisecondsInState { get; }
	}
	public interface IAnalogInput : IManagedDevice, IVoltage
	{
		new AnalogInput.IDevice Device { get; }
		new AnalogInput.IConfig Config { get; }

		AnalogInputMode AnalogInputMode { get; set; }
		double MaximumVoltage { get; set; }
		double MinimumVoltage { get; set; }
		bool OverRange { get; }
		bool UnderRange { get; }
	}


	public interface IAIVoltmeter : IVoltmeter, IAnalogInput
	{
		new AIVoltmeter.IDevice Device { get; }
		new AIVoltmeter.IConfig Config { get; }

		new double MaximumVoltage { get; set; }
		new double MinimumVoltage { get; set; }
		new bool OverRange { get; }
		new bool UnderRange { get; }
	}

	public interface IAIManometer : IAIVoltmeter, IManometer
	{
		new AIManometer.IDevice Device { get; }
		new AIManometer.IConfig Config { get; }
	}

	public interface IAIThermometer : IAIVoltmeter, IThermometer
	{
		new AIThermometer.IDevice Device { get; }
		new AIThermometer.IConfig Config { get; }
	}


	public interface IActuator : IManagedDevice, IOperatable
	{
		new Actuator.IDevice Device { get; }
		new Actuator.IConfig Config { get; }

		/// <summary>
		/// A collection of the supported actuator operations.
		/// </summary>
		ObservableItemsCollection<ActuatorOperation> ActuatorOperations { get; set; }

		/// <summary>
		/// The controller is Ready to support device operations.
		/// </summary>
		bool Ready { get; }

		/// <summary>
		/// Its controller is currently operating this device.
		/// </summary>
		bool Active { get; }

		/// <summary>
		/// The communications link has been established and
		/// data has been received.
		/// </summary>
		bool Linked { get; }

		/// <summary>
		/// The number of operations which have been submitted 
		/// to the Controller for this valve, but which have
		/// not been completed.
		/// </summary>
		int PendingOperations { get; }

		/// <summary>
		/// The actuator has no pending operations.
		/// </summary>
		bool Idle { get; }

		/// <summary>
		/// The current or most recent actuator operation undertaken.
		/// </summary>
		IActuatorOperation Operation { get; }

		/// <summary>
		/// The time (seconds) elapsed for the present or prior actuator movement.
		/// </summary>
		double Elapsed { get; }

		/// <summary>
		/// The maximum time (seconds) allowed for the operation to complete.
		/// A value of 0 means no time limit.
		/// </summary>
		double TimeLimit { get; set; }

		/// <summary>
		/// The time Elapsed reached the TimeLimit.
		/// </summary>
		bool TimeLimitDetected { get; }

		/// <summary>
		/// The actuator is currently moving.
		/// </summary>
		bool InMotion { get; }

		/// <summary>
		/// Motion is inhibited by a programmed stop condition 
		/// (e.g., travel limit detected, time elapsed, overcurrent, etc.).
		/// </summary>
		bool MotionInhibited { get; }

		/// <summary>
		/// The actuator received a Stop() request after the 
		/// current or prior operation started.
		/// </summary>
		bool StopRequested { get; }

		/// <summary>
		/// The actuator is not moving.
		/// </summary>
		new bool Stopped { get; }

		/// <summary>
		/// The prior operation completed successfully.
		/// </summary>
		bool ActionSucceeded { get; }

		/// <summary>
		/// Finds an ActuatorOperation by name.
		/// </summary>
		/// <param name="operationName">The name of the actuator operation</param>
		/// <returns></returns>
		IActuatorOperation FindOperation(string operationName);

		/// <summary>
		/// Validates the operation. Returns the supplied operation if 
		/// it is valid, or a valid alternative if not. Note: null is 
		/// always considered valid.
		/// </summary>
		/// <param name="operation"></param>
		/// <returns></returns>
		IActuatorOperation ValidateOperation(IActuatorOperation operation);

		/// <summary>
		/// Requests the controller to schedule the provided operation.
		/// </summary>
		/// <param name="operation">Note: The null operation represents a "select" functionality.</param>
		void DoOperation(IActuatorOperation operation);

		/// <summary>
		/// Interrupt the actuator motion; make it stop.
		/// </summary>
		void Stop();

		/// <summary>
		/// Wait until the actuator has no pending operations.
		/// </summary>
		void WaitForIdle();

	}

	public interface IActuatorOperation : INamedObject
	{
		/// <summary>
		/// Actuator position or movement amount.
		/// </summary>
		int Value { get; set; }

		/// <summary>
		/// Whether Value is a movement amount (i.e., not a position).
		/// </summary>
		bool Incremental { get; set; }

		/// <summary>
		/// A space-delimited list of controller commands.
		/// </summary>
		string Configuration { get; set; }
	}

	public interface ICpwActuator : IActuator
	{
		new CpwActuator.IDevice Device { get; }
		new CpwActuator.IConfig Config { get; }

		/// <summary>
		/// The device settings match the desired configuration.
		/// </summary>
		bool Configured { get; }

		/// <summary>
		/// The actuator's control pulse signal is enabled.
		/// </summary>
		bool ControlPulseEnabled { get; }

		/// <summary>
		/// The actuator position is detectable.
		/// </summary>
		bool PositionDetectable { get; }

		/// <summary>
		/// Limit switch 0 is engaged.
		/// </summary>
		bool LimitSwitch0Engaged { get; }

		/// <summary>
		/// Limit switch 1 is engaged.
		/// </summary>
		bool LimitSwitch1Engaged { get; }

		/// <summary>
		/// A limit switch is engaged.
		/// </summary>
		bool LimitSwitchDetected { get; }

		/// <summary>
		/// The most recent actuator current measurement.
		/// </summary>
		int Current { get; }

		/// <summary>
		/// The maximum expected current when the actuator is not moving.
		/// </summary>
		int IdleCurrentLimit { get; set; }

		/// <summary>
		/// The current limit was detected.
		/// </summary>
		bool CurrentLimitDetected { get; }

		/// <summary>
		/// Error codes reported by the controller.
		/// </summary>
		ActuatorController.ErrorCodes Errors { get; }

	}


	public interface IValve : IHacsDevice, IOperatable
	{
		new Valve.IDevice Device { get; }
		new Valve.IConfig Config { get; }

		/// <summary>
		/// The state of the valve (e.g., Opened, Closed, Unknown, etc.).
		/// </summary>
		ValveState ValveState { get; }

		/// <summary>
		/// Absolute position "Value"
		/// </summary>
		int Position { get; }

		/// <summary>
		/// The change in its internal volume when the
		/// valve moves from the Closed position to Opened.
		/// </summary>
		double OpenedVolumeDelta { get; set; }

		/// <summary>
		/// Whether the valve's Manager is ready to accept commands.
		/// </summary>
		bool Ready { get; }
		bool Idle { get; }


		/// <summary>
		/// The valve is fully opened.
		/// </summary>
		bool IsOpened { get; }

		/// <summary>
		/// The valve is fully closed.
		/// </summary>
		bool IsClosed { get; }

		/// <summary>
		/// Open the valve.
		/// </summary>
		void Open();

		/// <summary>
		/// Close the valve.
		/// </summary>
		void Close();

		/// <summary>
		/// Interrupt the valve motion; make it stop.
		/// </summary>
		void Stop();

		/// <summary>
		/// Open the valve and wait for the operation to complete.
		/// </summary>
		void OpenWait();

		/// <summary>
		/// Close the valve and wait for the operation to complete.
		/// </summary>
		void CloseWait();

		/// <summary>
		/// Wait until the valve has no pending operations.
		/// </summary>
		void WaitForIdle();

		/// <summary>
		/// Give the valve a light workout.
		/// </summary>
		void Exercise();
	}

	public interface IManagedValve : IValve, IManagedDevice
	{
		new ManagedValve.IDevice Device { get; }
		new ManagedValve.IConfig Config { get; }

	}

	public interface ISolenoidValve : IManagedSwitch, IValve
	{
		new SolenoidValve.IDevice Device { get; }
		new SolenoidValve.IConfig Config { get; }

		ValveState PoweredState { get; set; }
		int MillisecondsToChangeState { get; set; }
		bool InMotion { get; }
	}

	public interface IPneumaticValve : ISolenoidValve
	{
		new PneumaticValve.IDevice Device { get; }
		new PneumaticValve.IConfig Config { get; }

	}
	public interface IDualActionValve : IHacsDevice, IValve
	{
		new DualActionValve.IDevice Device { get; }
		new DualActionValve.IConfig Config { get; }

		IValve OpenValve { get; set; }
		IValve CloseValve { get; set; }
	}


	public interface ICpwValve : ICpwActuator, IValve
	{
		new CpwValve.IDevice Device { get; }
		new CpwValve.IConfig Config { get; }

		int OpenedValue { get; }
		int ClosedValue { get; }
		int CenterValue { get; }
		bool OpenIsPositive { get; }

		void DoWait(ActuatorOperation operation);
		ValveState LastMotion { get; }

		// new, to resolve ambiguity between IActuator and IValve versions
		// (by hiding IValve version)
		new void WaitForIdle();
		new void Stop();
	}

	/// <summary>
	/// A valve that sends position and movement data back to the controller.
	/// </summary>
	public interface IRxValve : IValve
	{
		new RxValve.IDevice Device { get; }
		new RxValve.IConfig Config { get; }

		/// <summary>
		/// The amount the valve moved from the beginning of the most recent motion.
		/// </summary>
		int Movement { get; }

		int CommandedMovement { get; }

		int ConsecutiveMatches { get; set; }

		bool EnoughMatches { get; }

		/// <summary>
		/// Lowest valid position value.
		/// </summary>
		int MinimumPosition { get; set; }

		/// <summary>
		/// Highest valid position value.
		/// </summary>
		int MaximumPosition { get; set; }

		/// <summary>
		/// The number of unique positions in one full turn of the valve.
		/// This value is determined by the resolution of the servo's
		/// position sensor. Default is 96, which corresponds to an
		/// angular resolution of 3.75 degrees.
		/// </summary>
		int PositionsPerTurn { get; set; }

	}

	public interface IRS232Valve : ICpwValve, IRxValve
	{
		new RS232Valve.IDevice Device { get; }
		new RS232Valve.IConfig Config { get; }
		bool WaitingForGo { get; }
		bool ControllerStopped { get; }
		bool ActuatorStopped { get; }

		/// <summary>
		/// Positional difference between Closed and the current-limited stop.
		/// </summary>
		int ClosedOffset { get; set; }

		/// <summary>
		/// The valve has been calibrated.
		/// </summary>
		bool Calibrated { get; }
		int ControlOutput { get; }
		long RS232UpdatesReceived { get; }

		/// <summary>
		/// Automatically determine the valve's closed position.
		/// </summary>
		void Calibrate();

	}

	public interface IHC6HeaterB2 : IManagedHeater
	{
		new HC6HeaterB2.IDevice Device { get; }
		new HC6HeaterB2.IConfig Config { get; }

		int ThermocoupleChannel { get; set; }
		IPidSetup Pid { get; set; }
		new HC6ControllerB2 Manager { get; }
		HC6HeaterB2.Modes Mode { get; }
		HC6ThermocoupleB2 Thermocouple { get; }
		int PidGain { get; }
		int PidIntegral { get; }
		int PidDerivative { get; }
		int PidPreset { get; }
		HC6ControllerB2.ErrorCodes Errors { get; }
		bool PidConfigured();
	}

	public interface IHC6ThermocoupleB2 : IManagedThermocouple
	{
		new HC6ThermocoupleB2.IDevice Device { get; }
		new HC6ThermocoupleB2.IConfig Config { get; }

		HC6ControllerB2.ErrorCodes Errors { get; }
	}


	public interface ISampleOwner : INamedObject
	{
		Dictionary<string, Sample> Samples { get; set; }
		Dictionary<string, ProcessSequence> ProcessSequences { get; set; }
	}

	public interface ISample : IHacsComponent
	{
		/// <summary>
		/// Typically assigned by the laboratory to identify and track the sample.
		/// </summary>
		string LabId { get; set; }
		IInletPort InletPort { get; set; }
		string Process { get; set; }
		bool SulfurSuspected { get; set; }
		bool Take_d13C { get; set; }
		/// <summary>
		/// Sample size
		/// </summary>
		double Grams { get; set; }
		/// <summary>
		/// Sample size
		/// </summary>
		double Milligrams { get; set; }
		/// <summary>
		/// Sample size
		/// </summary>
		double Micrograms { get; set; }
		/// <summary>
		/// Sample size
		/// </summary>
		double Micromoles { get; set; }

		/// <summary>
		/// Added dilution (dead) carbon
		/// </summary>
		double MicrogramsDilutionCarbon { get; set; }
		/// <summary>
		/// Carbon extracted from the sample
		/// </summary>
		double TotalMicrogramsCarbon { get; set; }
		/// <summary>
		/// Carbon extracted from the sample
		/// </summary>
		double TotalMicromolesCarbon { get; set; }

		/// <summary>
		/// Extracted carbon selected for analysis
		/// </summary>
		double SelectedMicrogramsCarbon { get; set; }
		/// <summary>
		/// Extracted carbon selected for analysis
		/// </summary>
		double SelectedMicromolesCarbon { get; set; }
		/// <summary>
		/// Extracted carbon selected for d13C analysis
		/// </summary>
		double Micrograms_d13C { get; set; }
		/// <summary>
		/// Carbon concentration in the d13C split after adding carrier gas.
		/// </summary>
		double d13CPartsPerMillion { get; set; }
		List<IAliquot> Aliquots { get; set; }
		List<string> AliquotIds { get; set; }
		int AliquotsCount { get; set; }
		int AliquotIndex(IAliquot aliquot);
	}

	public interface IAliquot : INamedObject, INotifyPropertyChanged
	{
		ISample Sample { get; set; }
		string GraphiteReactor { get; set; }
		double MicrogramsCarbon { get; set; }
		double MicromolesCarbon { get; }
		double InitialGmH2Pressure { get; set; }
		double FinalGmH2Pressure { get; set; }
		double H2CO2PressureRatio { get; set; }
		double ExpectedResidualPressure { get; set; }
		double ResidualPressure { get; set; }
		bool ResidualMeasured { get; set; }
		int Tries { get; set; }
	}


	public interface IChamber : IHacsComponent
	{
		double Pressure { get; }				// Torr?
		double Temperature { get; }				// Celsius?
		double MilliLiters { get; set; }		// Volume?

		IManometer Manometer { get; set; }
		IThermometer Thermometer { get; set; }
		IHeater Heater { get; set; }
		IColdfinger Coldfinger { get; set; }
		IVTColdfinger VTColdfinger { get; set; }
		bool Dirty { get; set; }
		Action Clean { get; set; }
	}

	public interface IFlowChamber : IChamber
	{
		IFlowManager FlowManager { get; set; }
		IRxValve FlowValve { get; }
	}

	public interface IPort : IChamber
	{
		IValve Valve { get; set; }
		void Open();
		void Close();
		bool IsOpened { get; }
		bool IsClosed { get; }
	}
	public interface ILinePort : IPort
	{
		LinePort.States State { get; set; }
		ISample Sample { get; set; }
		IAliquot Aliquot { get; set; }
		string Contents { get; }
	}

	public interface IInletPort: ILinePort
	{
		List<InletPort.Type> SupportedPortTypes { get; set; }
		InletPort.Type PortType { get; set; }
		bool NotifySampleFurnaceNeeded { get; set; }
		int WarmTemperature { get; set; }
		IHeater QuartzFurnace { get; set; }
		IHeater SampleFurnace { get; set; }
		ISwitch Fan { get; set; }
		List<IValve> PathToFirstTrap { get; set; }
		void TurnOffFurnaces();
		void Update();
	}

	public interface IGraphiteReactor : IPort
	{
		GraphiteReactor.States State { get; set; }
		GraphiteReactor.Sizes Size { get; set; }
		ISample Sample { get; set; }
		IAliquot Aliquot { get; set; }
		int GraphitizingTemperature { get; set; }
		int SampleTemperatureOffset { get; set; }
		Stopwatch StateStopwatch { get; }
		Stopwatch ProgressStopwatch { get; }
		double PressureMinimum { get; set; }
		int PressurePeak { get; set; }
		string Contents { get; }
		bool Busy { get; }
		bool Prepared { get; }
		double HeaterTemperature { get; }
		double SampleTemperature { get; }
		double ColdfingerTemperature { get; }
		bool FurnaceUnresponsive { get; }
		bool ReactionNotStarting { get; }
		bool ReactionNotFinishing { get; }
		void TurnOn(double sampleSetpoint);
		void TurnOff();
		void Start();
		void Stop();
		void Reserve(IAliquot aliquot);
		void Reserve(string contents);
		void ServiceComplete();
		void PreparationComplete();
		void Update();
	}

	public interface Id13CPort : ILinePort { }

	public interface ISection : IChamber 
	{
		/// <summary>
		/// The Chambers that together make up the Section.
		/// </summary>
		List<IChamber> Chambers { get; set; }

		/// <summary>
		/// The Ports that are connected to the Section.
		/// </summary>
		List<IPort> Ports { get; set; }

		/// <summary>
		/// The VacuumSystem used to evacuate the Section.
		/// </summary>
		IVacuumSystem VacuumSystem { get; set; }

		/// <summary>
		/// The ordered list of valves that isolate the Section and define
		/// its volume perimeter. 
		/// Usually, port valves should be omitted here (use the Ports list, 
		/// instead). Valves listed here are always closed to isolate the 
		/// section, whereas port valves are only operated explicitly as 
		/// such, and otherwise can be omitted from or included in normal 
		/// Section operations by managing them in the calling code, depending 
		/// on whether any or all should be treated as part of the Section 
		/// according to the needs of the caller.
		/// </summary>
		List<IValve> Isolation { get; set; }

		/// <summary>
		/// The ordered list of valves that join the Section chambers into a single volume.
		/// </summary>
		List<IValve> InternalValves { get; set; }

		/// <summary>
		/// The ordered list of valves that join the Section to its VacuumSystem manifold.
		/// The last valve in the list is on the VacuumSystem manifold.
		/// </summary>
		List<IValve> PathToVacuum { get; set; }

		/// <summary>
		/// The ordered list of valves that isolate the PathToVacuum.
		/// If PathToVacuum is null, PathToVacuumIsolation should be as well.
		/// </summary>
		List<IValve> PathToVacuumIsolation { get; set; }


		/// <summary>
		/// The measured volume of the joined chambers, or the
		/// CurrentVolume() if no measurement has been stored.
		/// The measured value may differ slightly from calculated
		/// sum of the individual chamber volumes, due to small
		/// movements of sub-volumes within the valves.
		/// </summary>
		new double MilliLiters { get; }

		/// <summary>
		/// The approximate volume of the section, the sum of its
		/// individual chamber volumes. This may differ slightly 
		/// from the measured value for the joined chambers, due 
		/// to small movements of sub-volumes within the valves.
		/// </summary>
		double CurrentVolume();

		/// <summary>
		/// The sum of the Chamber volumes and optionally the volumes of opened Ports.
		/// </summary>
		/// <param name="includePorts">include the opened port volumes?</param>
		double CurrentVolume(bool includePorts);

		/// <summary>
		/// If the Section comprises a single FlowChamber, this is its flow valve.
		/// </summary>
		IRxValve FlowValve { get; }

		/// <summary>
		/// If the Section comprises a single FlowChamber, this is its FlowManager.
		/// </summary>
		IFlowManager FlowManager { get; }

		/// <summary>
		/// Close the valves that form the Section boundary.
		/// </summary>
		void Isolate();

		/// <summary>
		/// Close the valves that form the Section boundary except for the given list.
		/// </summary>
		/// <param name="valves"></param>
		void IsolateExcept(IEnumerable<IValve> valves);

		/// <summary>
		/// Open the Section's internal valves (join all the Chambers).
		/// </summary>
		void Open();

		/// <summary>
		/// Close the Section's internal valves (separate all the Chambers).
		/// </summary>
		void Close();

		/// <summary>
		/// Close PathToVacuum. If PathToVacuum is null, invoke VacuumSystem.Isolate() instead.
		/// </summary>
		void IsolateFromVacuum();

		/// <summary>
		/// Open PathToVacuum. If PathToVacuum is null, invoke VacuumSystem.Evacuate() instead.
		/// Warning: No vacuum state or pressure checking is done.
		/// </summary>
		void JoinToVacuum();

		/// <summary>
		/// Isolate the section and connect it to the Vacuum Manifold
		/// if possible. If there is no PathToVacuum, isolate the
		/// section and the VacuumSystem Manifold.
		/// </summary>
		void IsolateAndJoinToVacuum();

		/// <summary>
		/// Isolate the section, join all chambers together, and evacuate them.
		/// Wait 3 seconds after evacuation commences.
		/// Port valves are not moved.
		/// </summary>
		void OpenAndEvacuate();

		/// <summary>
		/// Isolate the section, join all chambers together, and evacuate them.
		/// No port valves are moved if no port is specified. If a port is given,
		/// it is opened and all others are closed.
		/// If pressure is 0, wait until pressure_baseline is reached.
		/// If pressure &lt; 0, wait 3 seconds after evacuation commences.
		/// Otherwise, wait until the given pressure is reached.
		/// </summary>
		/// <param name="pressure">-1 is the default if no pressure is provided</param>
		/// <param name="port">also evacuate this port and no others</param>
		void OpenAndEvacuate(double pressure = -1, IPort port = null);

		/// <summary>
		/// Isolate the section, join all chambers together, open all ports,
		/// and evacuate them.
		/// WARNING: Do not use this method if any of the ports might be
		/// open to atmosphere or otherwise exposed to an essentially infinite 
		/// supply of gas.
		/// If pressure is 0, wait until pressure_baseline is reached.
		/// If pressure &lt; 0, wait 3 seconds after evacuation commences.
		/// Otherwise, wait until the given pressure is reached.
		/// </summary>
		/// <param name="pressure">-1 is the default if no pressure is provided</param>
		void OpenAndEvacuateAll(double pressure = -1);

		/// <summary>
		/// Isolate the Section and begin evacuating it. All other 
		/// valves on the VacuumSystem manifold are closed first.
		/// Wait three seconds after evacuation commences, then return.
		/// </summary>
		void Evacuate();

		/// <summary>
		/// Isolate the Section and evacuate it to the given pressure. All other 
		/// valves on the VacuumSystem manifold are closed first.
		/// </summary>
		/// <param name="pressure">wait until this pressure is reached</param>
		void Evacuate(double pressure);

		/// <summary>
		/// All internal valves are opened and the section is joined to the vacuum manifold.
		/// Note: the section need not be evacuated, nor evacuating, just connected 
		/// to the vacuum manifold.
		/// </summary>
		bool IsOpened { get; }

		/// <summary>
		/// Open all of the Ports on the Section.
		/// </summary>
		void OpenPorts();

		/// <summary>
		/// Close all of the Ports on the Section.
		/// </summary>
		void ClosePorts();

		/// <summary>
		/// Close all of the Ports except the given one.
		/// </summary>
		void ClosePortsExcept(IPort port);

		/// <summary>
		/// All of the valves that connect this Section directly to the given Section.
		/// </summary>
		List < IValve> Connections(ISection s);

		/// <summary>
		/// Joins this Section to the given Section by opening
		/// a valve between them.
		/// </summary>
		/// <returns>true if successful, false if no joining valve was found</returns>
		bool JoinTo(ISection s);

		/// <summary>
		/// Isolates the given Section from this one by closing all
		/// valves between them.
		/// </summary>
		/// <returns>true if successful, false if no joining valves were found</returns>
		bool IsolateFrom(ISection s);

		/// <summary>
		/// A clone of this Section, but without a Name.
		/// </summary>
		Section Clone();
	}

	public interface IGasSupply : IHacsComponent
	{
		IRS232Valve FlowValve { get; set; }
		IValve SourceValve { get; set; }
		IMeter Meter { get; set; }
		ISection Destination { get; set; }
		ISection Path { get; set; }
		IFlowManager FlowManager { get; set; }
		double PurgePressure { get; set; }
		int SecondsToPurge { get; set; }
		string GasName { get; set; }
		void IsolateAndJoin();
		void JoinToVacuumManifold();
		void IsolateFromVacuum();
		void EvacuatePath();
		void EvacuatePath(double pressure);
		void ShutOff();
		void ShutOff(bool alsoCloseFlow);
		void WaitForPressure(double pressure);
		void Admit();
		void Admit(double pressure);
		void Admit(double pressure, bool thenCloseFlow);
		void Flush(double pressureHigh, double pressureLow);
		void Flush(double pressureHigh, double pressureLow, int flushes);
		void Flush(double pressureHigh, double pressureLow, int flushes, IPort port);
		void Pressurize(double pressure);
		bool NormalizeFlow(bool calibrate = false);
		void FlowPressurize(double targetValue);
	}


	public interface IDataLog : IHacsComponent, IHacsLog
	{
		ObservableList<DataLog.Column> Columns { get; set; }
		Func<DataLog.Column, bool> Changed { get; set; }
		long ChangeTimeoutMilliseconds { get; set; }
		bool OnlyLogWhenChanged { get; set; }
	}

	public interface IAlertManager : IHacsComponent
	{
		new bool Stopped { get; }
		string PriorAlertMessage { get; set; }
		ContactInfo ContactInfo { get; set; }
		SmtpInfo SmtpInfo { get; set; }
		void Send(string subject, string message);
		void Announce(string subject, string message);
		void Pause(string subject, string message);
		void Warn(string subject, string message);
		void ClearLastAlertMessage();

	}

	public interface IVacuumSystem : IHacsComponent
	{
		new bool Stopped { get; }

		/// <summary>
		/// Vacuum Manifold pressure gauge
		/// </summary>
		IManometer Manometer { get; set; }

		/// <summary>
		/// Vacuum Manifold pressure
		/// </summary>
		double Pressure { get; }

		/// <summary>
		/// Foreline pressure gauge (rough, backing, or "low" vacuum)
		/// </summary>
		IManometer ForelineManometer { get; set; }

		/// <summary>
		/// This valve connects the Vacuum Manifold to the high-vacuum pump inlet. 
		/// The high-vacuum pump is typically a turbomolecular pump.
		/// </summary>
		IValve HighVacuumValve { get; set; }

		/// <summary>
		/// This valve connects the Foreline to the Vacuum Manifold.
		/// </summary>
		IValve LowVacuumValve { get; set; }

		/// <summary>
		/// This valve connects the high-vacuum pump outlet to the Foreline
		/// </summary>
		IValve BackingValve { get; set; }

		/// <summary>
		/// This valve connects the Foreline to the low-vacuum or "roughing" 
		/// pump inlet. The low-vacuum pump is typically a scroll, diaphragm, 
		/// or rotary-vane pump.
		/// </summary>
		IValve RoughingValve { get; set; }

		/// <summary>
		/// This Section of the line connects the sample processing Sections to the 
		/// vacuum system.
		/// </summary>
		ISection VacuumManifold { get; set; }

		/// <summary>
		/// The Vacuum system controls whether Manometer is on or off.
		/// </summary>
		bool AutoManometer { get; set; }

		/// <summary>
		/// This is the value that VacuumManifold pressure must reach before
		/// the VacuumSystem will return from its WaitForPressure() method.
		/// TargetPressure can be altered dynamically while WaitForPressure() is
		/// active; that's its intended use.
		/// </summary>
		double TargetPressure { get; set; }

		/// <summary>
		/// Typical maximum pressure for auto-zeroing pressure gauges.
		/// </summary>
		double BaselinePressure { get; set; }

		/// <summary>
		/// How long the VacuumSystem has been in high-vacuum mode with a
		/// VacuumManifold pressure &lt; BaselinePressure and a stable
		/// Foreline pressure.
		/// </summary>
		TimeSpan TimeAtBaseline { get; }

		/// <summary>
		/// Open or close the BackingValve when Foreline pressure is less than this
		/// </summary>
		double GoodBackingPressure { get; set; }

		/// <summary>
		/// High vacuum mode is preferred below this pressure.
		/// </summary>
		double HighVacuumPreferredPressure { get; set; }

		/// <summary>
		/// Do not use low vacuum mode below this pressure.
		/// </summary>
		double HighVacuumRequiredPressure { get; set; }

		/// <summary>
		/// Do not use high vacuum mode above this pressure.
		/// </summary>
		double LowVacuumRequiredPressure { get; set; }

		VacuumSystem.StateCode State { get; }
		long MillisecondsInState { get; }

		/// <summary>
		/// Isolate the VacuumManifold.
		/// </summary>
		void IsolateManifold();

		/// <summary>
		/// IsolateManifold() but skip the specified valves.
		/// </summary>
		/// <param name="valve">Skip these valves</param>
		void IsolateExcept(IEnumerable<IValve> valves);

		/// <summary>
		///  Disables all automatic control of VacuumSystem.
		/// </summary>
		void Standby();

		/// <summary>
		/// Isolates the pumps from the vacuum manifold.
		/// Returns only after isolation is complete.
		/// </summary>
		void Isolate();

		/// <summary>
		/// Isolates the pumps from the vacuum manifold.
		/// </summary>
		/// <param name="waitForState">If true, returns only after isolation is complete.</param>
		void Isolate(bool waitForState);

		/// <summary>
		/// Requests Evacuation mode. Initiates pumping on the vacuum manifold and attempts to bring it to high vacuum.
		/// </summary>
		void Evacuate();

		/// <summary>
		/// Requests Evacuate mode. Returns when the target pressure is reached.
		/// </summary>
		/// <param name="pressure">Target pressure</param>
		void Evacuate(double pressure);

		/// <summary>
		/// Waits 3 seconds, then until the given pressure is reached.
		/// Use 0 to wait for baseline, &lt;0 to just wait 3 seconds.
		/// </summary>
		/// <param name="pressure">Use 0 to wait for baseline, &lt;0 to just wait 3 seconds.</param>
		void WaitForPressure(double pressure);

		/// <summary>
		/// Wait until the TimeAtBaseline timer reaches at least 10 seconds
		/// </summary>
		void WaitForStableBaselinePressure();

		/// <summary>
		/// Request to evacuate vacuum manifold using low-vacuum pump only.
		/// Vacuum Manifold will be roughed and isolated alternately
		/// to maintain VM pressure between pressure_HV_required
		/// and pressure_LV_required
		/// </summary>
		void Rough();
		void DisableManometer();
		void EnableManometer();
	}

	public interface IPower : IHacsComponent
	{
		IVoltmeter DC5V { get; set; }
		IVoltmeter MainsDetect { get; set; }
		double MainsDetectMinimumVoltage { get; set; }
		bool MainsIsDown { get; }
		bool MainsHasFailed { get; }
		Action MainsDown { get; set; }
		Action MainsRestored { get; set; }
		Action MainsFailed { get; set; }
		void Update();
	}

	public interface IPidControl : IHacsComponent
	{
		IPidSetup PidSetup { get; set; }
		int MillisecondsUpdate { get; set; }
		double ControlOutputLimit { get; set; }
		double ReferencePoint { get; set; }
		Func<double> GetSetpoint { get; set; }
		Func<double> GetProcessVariable { get; set; }
		Action<double> UpdateControlOutput { get; set; }
		double MinimumControlledProcessVariable { get; set; }
		double BlindControlOutput { get; set; }
		bool Busy { get; }
		void Start();
		void Stop();
	}

	public interface IPidSetup : IHacsComponent
	{
		double Gain { get; set; }
		double Integral { get; set; }
		double Derivative { get; set; }
		double Preset { get; set; }
		int GainPrecision { get; set; }
		int IntegralPrecision { get; set; }
		int DerivativePrecision { get; set; }
		int PresetPrecision { get; set; }
		int EncodedGain { get; set; }
		int EncodedIntegral { get; set; }
		int EncodedDerivative { get; set; }
		int EncodedPreset { get; set; }
	}

	public interface IFlowManager : IHacsComponent
	{
		IRS232Valve FlowValve { get; set; }
		IMeter Meter { get; set; }
		int MillisecondsTimeout { get; set; }
		double SecondsCycle { get; set; }
		int StartingMovement { get; set; }
		int MaximumMovement { get; set; }
		int Lag { get; set; }
		double Deadband { get; set; }
		bool DeadbandIsFractionOfTarget { get; set; }
		double Gain { get; set; }
		bool DivideGainByDeadband { get; set; }
		bool StopOnFullyOpened { get; set; }
		bool StopOnFullyClosed { get; set; }
		double TargetValue { get; set; }
		bool UseRateOfChange { get; set; }
		bool Busy { get; }
		void Start();
		void Start(double targetValue);
		void Stop();
	}


	public interface IProcessManager : IHacsBase
	{
		AlertManager AlertManager { get; set; }
		Dictionary<string, ThreadStart> ProcessDictionary { get; }
		List<string> ProcessNames { get; }
		Dictionary<string, ProcessSequence> ProcessSequences { get; set; }
		ProcessManager.ProcessStateCode ProcessState { get; }
		TimeSpan ProcessTime { get; }
		StepTracker ProcessStep { get; }
		StepTracker ProcessSubStep { get;  }
		string ProcessToRun { get; set; }
		ProcessManager.ProcessTypeCode ProcessType { get; }

		bool RunCompleted { get; }
		bool Busy { get; }
		void RunProcess(string processToRun);
		void AbortRunningProcess();
		void WaitMinutes(int minutes);
	}

	public interface IProcessSequence : IHacsComponent
	{
		InletPort.Type PortType { get; set; }
		List<string> CheckList { get; set; }
		List<ProcessSequenceStep> Steps { get; set; }
		ProcessSequence Clone();
	}

	public interface IProcessSequenceStep : INamedObject
	{
		ProcessSequenceStep Clone();
	}
	public interface ICombustionStep : IProcessSequenceStep
	{
		int Temperature { get; set; }
		int Minutes { get; set; }
		bool AdmitO2 { get; set; }
		bool OpenLine { get; set; }
		bool WaitForSetpoint { get; set; }
	}
	public interface IWaitMinutesStep : IProcessSequenceStep
	{
		int Minutes { get; set; }
	}

	public interface IVolumeCalibration : IHacsComponent
	{
		IGasSupply GasSupply { get; set; }
		double CalibrationPressure { get; set; }
		int CalibrationMinutes { get; set; }
		List<VolumeExpansion> Expansions { get; set; }
		bool ExpansionVolumeIsKnown { get; set; }
		StepTracker ProcessStep { get; set; }
		StepTracker ProcessSubStep { get; set; }
		Action OpenLine { get; set; }
		double OkPressure { get; set; }
		HacsLog Log { get; set; }
		void Calibrate(int repeats = 5);
	}

	public interface IVolumeExpansion : IHacsComponent
	{
		IChamber Chamber { get; set; }
		List<IValve> ValveList { get; set; }
	}


	// TODO: Carefully reconsider exactly which CEGS properties 
	// and methods should be public.
	public interface ICegs : IProcessManager, ISampleOwner
	{
		CegsPreferences Preferences { get; set; }

		// TODO shouldn't these be overrides?
		new bool Started { get; }
		new bool Stopped { get; }

		public Dictionary<string, IDeviceManager> DeviceManagers { get; set; }
		public Dictionary<string, IManagedDevice> ManagedDevices { get; set; }
		public Dictionary<string, IMeter> Meters { get; set; }
		public Dictionary<string, IValve> Valves { get; set; }
		public Dictionary<string, ISwitch> Switches { get; set; }
		public Dictionary<string, IHeater> Heaters { get; set; }
		public Dictionary<string, IPidSetup> PidSetups { get; set; }

		public Dictionary<string, ILNManifold> LNManifolds { get; set; }
		public Dictionary<string, IColdfinger> Coldfingers { get; set; }
		public Dictionary<string, IVTColdfinger> VTColdfingers { get; set; }

		public Dictionary<string, IVacuumSystem> VacuumSystems { get; set; }
		public Dictionary<string, IChamber> Chambers { get; set; }
		public Dictionary<string, ISection> Sections { get; set; }
		public Dictionary<string, IGasSupply> GasSupplies { get; set; }
		public Dictionary<string, IFlowManager> FlowManagers { get; set; }

		public Dictionary<string, IVolumeCalibration> VolumeCalibrations { get; set; }
		public Dictionary<string, IHacsLog> Logs { get; set; }

		// TODO: make internal component references protected set?
		Power Power { get; set; }
		//...
		TimeSpan Uptime { get; }
		bool SampleIsRunning { get; }
		Func<bool, List<IInletPort>> SelectSamples { get; set; }
		string PriorAlertMessage { get; }
		ISample Sample { get; }
	}

	public interface ICegsPreferences : INotifyPropertyChanged
	{

		/// <summary>
		/// Enable watchdog services like Power and Vacuum System failure monitors
		/// </summary>
		static bool EnableWatchdogs { get; set; }

		/// <summary>
		/// Pressure gauges automatically zero themselves when they are under
		/// sufficient vacuum.
		/// </summary>
		static bool EnableAutozero { get; set; }

		/// <summary>
		/// The last graphite reactor used. The next one that will
		/// be used is determined by starting at the one after this 
		/// and searching forward for an available reactor, wrapping
		/// back to the first when the end of the bank is reached.
		/// </summary>
		static string LastGR { get; set; }

		/// <summary>
		/// A counter for the total number of graphite reactions. This
		/// number is recorded in the Sample Information data log
		/// along with summary reaction parameters and process 
		/// values.
		/// </summary>
		static int NextGraphiteNumber { get; set; }


		/// <summary>
		/// Usually about 10 Torr over standard; should be slightly greater 
		/// than the atmospheric pressure at any lab that will handle 
		/// gaseous samples in septum-sealed vials (including external labs).
		/// </summary>
		static double PressureOverAtm { get; set; }
		/// <summary>
		/// clean enough to join sections for drying 
		/// </summary>
		static double OkPressure { get; set; }
		/// <summary>
		/// clean enough to start a new sample
		/// </summary>
		static double CleanPressure { get; set; }
		/// <summary>
		/// An initial GM He pressure that will result in pressure_over_atm when expanded into an LN-frozen vial
		/// </summary>
		static double VPInitialHePressure { get; set; }
		/// <summary>
		/// The maximum value for abs(pVP - pressure_over_atm) that is considered nominal.
		/// </summary>
		static double VPErrorPressure { get; set; }
		/// <summary>
		/// An initial IM O2 pressure that will expand sufficient oxygen into the inlet port to fully combust
		/// any sample. Typically, this means enough to fully oxidize about three mg C to CO2.
		/// </summary>
		static double IMO2Pressure { get; set; }
		/// <summary>
		/// The VTT pressure to maintain when trapping a sample out of a carrier gas
		/// by "bleeding" the mixture through the VTT.
		/// </summary>
		static double VttSampleBleedPressure { get; set; }
		/// <summary>
		/// The VTT pressure to maintain when bleeding a drying gas through a wet VTT.
		/// </summary>
		static double VttCleanupPressure { get; set; }
		/// <summary>
		/// The VTT pressure at which the process should continue once the bypass valve has been opened;
		/// </summary>
		static double VttNearEndOfBleedPressure { get; set; }
		/// <summary>
		/// The maximum upstream (IM) pressure at which the VTT flow bypass valve should be opened, after the VTT
		/// flow valve is already fully opened. Choose this pressure so that opening the bypass valve
		/// will not produce an excessive downstream pressure spike.
		/// </summary>
		static double VttFlowBypassPressure { get; set; }
		/// <summary>
		/// The initial H2 pressure to be used when preparing Fe in the GRs for use as the graphitization catalyst.
		/// </summary>
		static double FePreconditionH2Pressure { get; set; }

		/// <summary>
		/// The maximum VTT flow rate (Torr/s) at which the VTT flow bypass valve should be opened, after the VTT
		/// flow valve is already fully opened. This should be a negative number. (Note that a *more* negative number
		/// represents a faster rate of flow, i.e., a faster drop in pressure.
		/// </summary>
		static double VttPressureFallingVerySlowlyRateOfChange { get; set; }
		/// <summary>
		/// The maximum falling VTT flow rate (Torr/s) that is considered to be essentially stable. 
		/// </summary>
		static double VttPressureBarelyFallingRateOfChange { get; set; }
		/// <summary>
		/// The IM pressure rate of change, following a stable flow, used to detect 
		/// that something has been put onto the IP needle (a vial or stopper).
		/// </summary>
		static double IMPluggedPressureRateOfChange { get; set; }
		/// <summary>
		/// An IM pressure rate of change, following "plugged" detection, that indicates the 
		/// delayed slowing of the pressure increase characteristic of a vial being filled, 
		/// as distinct from the rapid drop to stability that occurs when the needle is 
		/// plugged by a stopper.
		/// </summary>
		static double IMLoadedPressureRateOfChange { get; set; }

		/// <summary>
		/// Typical room temperature in the lab.
		/// </summary>
		static int RoomTemperature { get; set; }

		// TODO: MOVE THESE TO VTC class
		/// <summary>
		/// Maximum temperature at which to use the trap for collecting CO2.
		/// </summary>
		static int VttColdTemperature { get; set; }
		/// <summary>
		/// Temperature to use for cleaning (drying) the VTT.
		/// </summary>
		static int VttCleanupTemperature { get; set; }

		// TODO: move these to the GR class
		/// <summary>
		/// Reaction temperature for trapping trace sulfur contamination in a CO2 sample onto iron powder.
		/// </summary>
		static int SulfurTrapTemperature { get; set; }
		/// <summary>
		/// Reaction temperature for preparing iron powder with H2 for use as a CO2 reduction catalyst.
		/// </summary>
		static int IronPreconditioningTemperature { get; set; }
		/// <summary>
		/// Less than this error is near enough to the Fe Prep reaction temperature to begin the process.
		/// </summary>
		static int IronPreconditioningTemperatureCushion { get; set; }

		/// <summary>
		/// Duration of iron powder preparation process.
		/// </summary>
		static int IronPreconditioningMinutes { get; set; }
		/// <summary>
		/// How long after turn-on the quartz bed takes to reach its functional temperature range.
		/// </summary>
		static int QuartzFurnaceWarmupMinutes { get; set; }
		/// <summary>
		/// Duration of the sulfur trapping process.
		/// </summary>
		static int SulfurTrapMinutes { get; set; }
		/// <summary>
		/// System "tick" time. Determines how often the system checks polled conditions and updates passive devices.
		/// </summary>
		static int UpdateIntervalMilliseconds { get; set; }

		/// <summary>
		/// Avogadro's number (particles/mol)
		/// </summary>
		static double AvogadrosNumber { get; set; }
		/// <summary>
		/// Boltzmann constant (Pa * m^3 / K)
		/// </summary>
		static double BoltzmannConstant { get; set; }
		/// <summary>
		/// Pascals per atm
		/// </summary>
		static double Pascal { get; set; }
		/// <summary>
		/// Torr per atm
		/// </summary>
		static double Torr { get; set; }
		/// <summary>
		/// milliliters per liter
		/// </summary>
		static double MilliLiter { get; set; }
		/// <summary>
		/// cubic meters per liter
		/// </summary>
		static double CubicMeter { get; set; }
		/// <summary>
		/// 0 °C in kelvins
		/// </summary>
		static double ZeroDegreesC { get; set; }
		/// <summary>
		/// mass of carbon per mole, in micrograms, assuming standard isotopic composition
		/// </summary>
		static double MicrogramsCarbonPerMole { get; set; }

		/// <summary>
		/// Stoichiometric ratio of hydrogen and carbon dioxide for CO2 reduction reactions.
		/// </summary>
		static double H2_CO2StoichiometricRatio { get; set; }
		/// <summary>
		/// Target H2:CO2 ratio for graphitization, providing excess hydogen to speed reaction.
		/// </summary>
		static double H2_CO2GraphitizationRatio { get; set; }
		/// <summary>
		/// Estimated appropriate initial GM H2 pressure reduction to compensate for 
		/// higher density of H2 in frozen GR coldfinger.
		/// </summary>
		static double H2DensityAdjustment { get; set; }
		/// <summary>
		/// Below this measured CO2 sample size, the system will add 14C-free dilution CO2
		/// to ensure there is enough mass for AMS measurement.
		/// </summary>
		static int SmallSampleMicrogramsCarbon { get; set; }
		/// <summary>
		/// The minimum final mass for a diluted sample.
		/// </summary>
		static int DilutedSampleMicrogramsCarbon { get; set; }
		/// <summary>
		/// Above this measured CO2 sample size, a fraction will be discarded to
		/// bring the mass closer to the 1000 ug nominal for AMS measurement;
		/// </summary>
		static int MaximumSampleMicrogramsCarbon { get; set; }
	}

	public interface IExtractionLine : IProcessManager
	{
		CEGS CEGS { get; set; }
		TubeFurnace TubeFurnace { get; set; }
		VacuumSystem VacuumSystem { get; set; }
		IManometer TubeFurnaceManometer { get; set; }
		MassFlowController MFC { get; set; }
		GasSupply O2GasSupply { get; set; }
		GasSupply HeGasSupply { get; set; }
		IValve CegsValve { get; set; }
		IValve v_TF_VM { get; set; }
		IRS232Valve v_TF_flow { get; set; }
		IValve v_TF_flow_shutoff { get; set; }
		ISection TubeFurnaceSection { get; set; }
		ILinePort TubeFurnacePort { get; set; }
		IFlowManager TubeFurnaceRateManager { get; set; }
		IFlowManager TubeFurnacePressureManager { get; set; }
		double OkPressure { get; set; }
		double CleanPressure { get; set; }
		IManometer AmbientManometer { get; set; }
		HacsLog SampleLog { get; set; }
	}


	public interface IEdwardsAimX : ISwitchedManometer, IAnalogInput
	{
		new EdwardsAimX.IDevice Device { get; }
		new EdwardsAimX.IConfig Config { get; }
		new bool OverRange { get; }
		new bool UnderRange { get; }

	}

	// TODO make this a managed device and Xgs600 a device manager
	public interface IImg100 : ISwitchedManometer
	{
		new Img100.IDevice Device { get; }
		new Img100.IConfig Config { get; }

		Xgs600 Controller { get; set; }
		string UserLabel { get; set; }

	}

	public interface IMassFlowController : IAnalogOutput, ISetpoint
	{
		new MassFlowController.IDevice Device { get; }
		new MassFlowController.IConfig Config { get; }
		OperationSet OutputConverter { get; set; }
		double FlowRate { get; }
		double TrackedFlow { get; set; }
		double MinimumSetpoint { get; set; }
		double MaximumSetpoint { get; set; }
		void ResetTrackedFlow();
	}

	public interface IXgs600 : ISerialController
	{
		List<IMeter> Gauges { get; set; }
		Xgs600.PressureUnits Units { get; }
		Xgs600.PressureUnits TargetUnits { get; set; }
		void SetPressureUnits(Xgs600.PressureUnits pressureUnits);
		void TurnOn(string userLabel, Action<string> returnResponse = default);
		void TurnOff(string userLabel, Action<string> returnResponse = default);

	}
}
