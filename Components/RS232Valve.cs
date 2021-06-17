using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Text;
using System.Threading;
using Utilities;

namespace HACS.Components
{
	/// <summary>
	/// A valve actuated by one of Aeon's servos that use 
	/// RS232 serial communications.
	/// </summary>
	public class RS232Valve : CpwValve, IRS232Valve, RS232Valve.IDevice, RS232Valve.IConfig
	{
		#region HacsComponent
		#endregion HacsComponent

		#region Device interfaces
		public new interface IDevice : CpwValve.IDevice, RxValve.IDevice
		{
			new bool Active { get; set; }
			int ControlOutput { get; set; }
			long RS232UpdatesReceived { get; set; }
		}
		public new interface IConfig : CpwValve.IConfig, RxValve.IConfig { }
		public new IDevice Device => this;
		public new IConfig Config => this;
		RxValve.IDevice IRxValve.Device => this;
		RxValve.IConfig IRxValve.Config => this;

		#endregion Device interfaces

		#region Settings

		/// <summary>
		/// Represents the angular position of the valve, in
		/// units defined by the value of OneTurn. For example,
		/// if OneTurn = 96, Position 96 is one full turn away
		/// from Position 0.
		/// </summary>
		[JsonProperty]
		public override int Position { get => RxValve.Position; protected set => RxValve.Device.Position = value; }

		/// <summary>
		/// The minimum Position. Except during calibration, the 
		/// software constrains Valve movements to prevent the 
		/// Position from going below this value.
		/// </summary>
		[JsonProperty]
		public int MinimumPosition { get => RxValve.MinimumPosition; set => RxValve.MinimumPosition = value; }

		/// <summary>
		/// The maximum Position. Except during calibration, the 
		/// software constrains Valve movements to prevent the 
		/// Position from exceeding this value.
		/// </summary>
		[JsonProperty]
		public int MaximumPosition { get => RxValve.MaximumPosition; set => RxValve.MaximumPosition = value; }

		/// <summary>
		/// The number of unique positions in one full turn of the valve.
		/// This value is determined by the resolution of the servo's
		/// position sensor. Default is 96, which corresponds to an
		/// angular resolution of 3.75 degrees.
		/// </summary>
		[JsonProperty, DefaultValue(96)]
		public int PositionsPerTurn { get => RxValve.PositionsPerTurn; set => RxValve.PositionsPerTurn = value; }

		/// <summary>
		/// The Closed position is determined by taking
		/// the current-limited soft stop found during 
		/// calibration, and offsetting it by this value 
		/// toward the Opening direction.
		/// </summary>
		[JsonProperty, DefaultValue(10)]
		public virtual int ClosedOffset
		{
			get => closedOffset;
			set => Ensure(ref closedOffset, value);
		}
		int closedOffset;

		/// <summary>
		/// The valve has been calibrated.
		/// </summary>
		[JsonProperty, DefaultValue(true)]
		public virtual bool Calibrated
		{
			get => calibrated;
			protected set => Ensure(ref calibrated, value);
		}
		bool calibrated = true;

		#endregion Settings

		#region Retrieved device values

		/// <summary>
		/// The commanded movement. Initially, this is provided
		/// to the servo by the controller, based on the current 
		/// Position and the desired Position, constrained to 
		/// Min/Max limits. It may be subsequently altered by
		/// the servo (e.g., when a Stop command is issued).
		/// </summary>
		public virtual int CommandedMovement => Device.CommandedMovement;

		/// <summary>
		/// The amount that the physical position of the actuator
		/// has changed, in Position units, from the start of an 
		/// operation.
		/// </summary>
		public virtual int Movement => Device.Movement;


		/// <summary>
		/// The servo's Control Output value.
		/// </summary>
		public int ControlOutput
		{
			get => controlOutput;
			protected set => Ensure(ref controlOutput, value);
		}
		int controlOutput;
		int IDevice.ControlOutput
		{
			get => ControlOutput;
			set => ControlOutput = value;
		}

		/// <summary>
		/// The number of status updates received from the 
		/// servo since the last operation was started.
		/// </summary>
		public long RS232UpdatesReceived
		{
			get => rs232UpdatesReceived;
			protected set => Ensure(ref rs232UpdatesReceived, value);
		}
		long rs232UpdatesReceived;
		long IDevice.RS232UpdatesReceived
		{
			get => RS232UpdatesReceived;
			set => RS232UpdatesReceived = value;
		}

		#endregion Retrieved device values

		public override bool Linked => base.Linked && RS232UpdatesReceived > 0;

		/// <summary>
		/// The servo is initialized and ready for a movement command.
		/// </summary>
		public virtual bool WaitingForGo => Linked && CommandedMovement == 0;

		/// <summary>
		/// The controller has detected or anticipates actuator motion.
		/// </summary>
		public override bool InMotion => Linked &&
			(Movement != CommandedMovement || !EnoughMatches);

		bool IDevice.Active
		{
			get { return base.Active; }
			set
			{
				base.Device.Active = value;
				if (value)
					Device.RS232UpdatesReceived = 0;
			}
		}

		public virtual bool ControllerStopped => !Ready || base.Stopped;

		public virtual bool ActuatorStopped => !Ready || 
			(RS232UpdatesReceived > 0 && 
			Movement == CommandedMovement && 
			EnoughMatches);

		public override bool MotionInhibited => base.MotionInhibited || ActuatorStopped;

		public override bool Stopped => ControllerStopped && ActuatorStopped;

		// this override ignores any Cpw value difference
		public override bool Configured =>
			Operation == null ||
			Operation.Name == "Select" ||
			(UpdatesReceived > 0 && Device.Settings.LimitsMatch(Config.Settings));

		//public override bool ActionSucceeded => base.ActionSucceeded && CommandedMovement == Operation.Value;
		public override IActuatorOperation ValidateOperation(IActuatorOperation operation)
		{
			if (isCalibrating) return operation;       // trust the calibration routine
			return base.ValidateOperation(operation);
		}

		public override void DoOperation(string operationName)
		{
			if (operationName == "Stop")
			{
				Stop();
				return;
			}

			if (!isCalibrating && (!Calibrated || 
				operationName == "Calibrate" ||
				(!IsClosed && operationName == "Close" && ClosedOffset == 0)))
			{
				Calibrate();
				if (!Calibrated || operationName == "Calibrate" || operationName == "Close")
					return;
			}

			base.DoOperation(operationName);
		}

		protected override void UpdateValveState()
		{
			ValveState =
				Active ? OperationDirection(Operation) :
				Position == OpenedValue ? ValveState.Opened :
				Position == ClosedValue ? ValveState.Closed :
				ValveState.Other;
		}


		/// <summary>
		/// Exercising these valves is disabled.
		/// </summary>
		public override void Exercise() { }


		// override to allow Calibration to be aborted
		public override void Stop()
		{
			base.Stop();
			isCalibrating = false;
		}

		private bool isCalibrating = false;

		void Configure(ActuatorOperation operation, int value, int currentLimit, double timeLimit, int multiplier = 1)
		{
			operation.Value = multiplier * value;
			// assuming 2 seconds per turn, add time for movement past the minimum "value"
			timeLimit += 2 / PositionsPerTurn * (Math.Abs(multiplier)-1) * value;
			operation.Configuration = $"i{currentLimit} t{timeLimit:0.00}";
		}

		/// <summary>
		/// Finds the valve's Closed position based on the torque required to turn it.
		/// Returns with the valve in the calibrated closed position.
		/// </summary>
		public void Calibrate()
		{
			int value = 1;
			int currentLimit = 400;
			double timeLimit = 0.4;

			var operationName = "Calibrate";
			ActuatorOperation operation = FindOperation(operationName) as ActuatorOperation;
			if (operation != null)
			{
				value = operation.Value;
				currentLimit = 0;
				timeLimit = 0.0;
				if (!operation.Configuration.IsBlank())
				{
					foreach (var token in operation.Configuration.Split(' '))
					{
						if (token.Length > 0)
						{
							if (token[0] == 'i')
								int.TryParse(token.Substring(1), out currentLimit);
							else if (token[0] == 't')
								double.TryParse(token.Substring(1), out timeLimit);
						}
					}
				}
			}
			//else ... create a default Calibrate operation?

			operationName = "_Calibrate";   // temporary
			operation = FindOperation(operationName) as ActuatorOperation;
			if (operation != null) ActuatorOperations.Remove(operation);
			operation = new ActuatorOperation()
			{
				Name = operationName,
				Incremental = true
			};
			Configure(operation, value, currentLimit, timeLimit);
			ActuatorOperations.Add(operation);

			StopRequested = false;
			isCalibrating = true;
			if (Calibrated)
				CloseWait();

			var multiplier = 1;
			while (multiplier < 8 && (!CurrentLimitDetected || multiplier > 3))
			{
				if (CurrentLimitDetected)
				{
					multiplier = 1;
					Configure(operation, value, currentLimit, timeLimit, -3);
					DoWait(operation);
					if (StopRequested || !isCalibrating)
					{
						ActuatorOperations.Remove(operation);
						return;
					}
					if (CurrentLimitDetected || TimeLimitDetected)
					{
						// The valve is stuck trying to open. Calibrate() cannot continue
						// because there is no way to tell whether it is stuck opened or closed
						Alert.Warn($"{Name} is stuck.", "Manually free it, then click OK to continue.");
					}
				}

				Configure(operation, value, currentLimit, timeLimit, multiplier);
				if (!FindClosedPosition(operation, 5 * multiplier))
				{
					ActuatorOperations.Remove(operation);
					return;
				}
				if (!CurrentLimitDetected)
					++multiplier;
			} ;

			if (!CurrentLimitDetected)
				Alert.Warn("Valve failure", $"Cannot find closed position for {Name}."); 

			int closedPosition = FindOperation("Close")?.Value ?? MaximumPosition - ClosedOffset;
			Position = MaximumPosition = closedPosition + ClosedOffset;
			ClosedValue = closedPosition;

			if (Position == closedPosition)
				UpdateValveState();
			else
			{
				// move to closed position;
				//CloseWait();	// this could over-current, because the valve is "jammed"
				operation.Value = ClosedValue;
				operation.Incremental = false;
				operation.Configuration = "t1.0";	// no current limit
				DoWait(operation);
			}
			ActuatorOperations.Remove(operation);
			isCalibrating = false;
			Calibrated = true;     
		}

		/// <summary>
		///	Tries to find the closed position of the valve by the current required
		///	to turn it. Success (i.e., the closed position was found) is indicated 
		///	by CurrentLimitDetected, not by the return value.
		/// </summary>
		/// <returns>True if the operation was not externally interrupted.</returns>
		protected virtual bool FindClosedPosition(ActuatorOperation operation, int maxTries = 5)
		{
			Calibrated = false;
			var tries = 0;
			do
			{
				ClosedValue = MaximumPosition = Position + operation.Value + 1;
				DoWait(operation);
				++tries;
				if (TimeLimitDetected) operation.Value += 1;                    
				if (StopRequested || !isCalibrating) return false;
			} while (tries < maxTries && !CurrentLimitDetected);
			return true;
		}

		int RxValve.IDevice.Position { get => RxValve.Device.Position; set => RxValve.Device.Position = value; }
		int RxValve.IDevice.Movement { get => RxValve.Device.Movement; set => RxValve.Device.Movement = value; }
		int RxValve.IDevice.CommandedMovement { get => RxValve.Device.CommandedMovement; set => RxValve.Device.CommandedMovement = value; }

		public virtual int ConsecutiveMatches { get => RxValve.ConsecutiveMatches; set => TargetConsecutiveMatches = value; }
		[JsonProperty("ConsecutiveMatches"), DefaultValue(3)]
		int TargetConsecutiveMatches { get => RxValve.Config.ConsecutiveMatches; set => RxValve.ConsecutiveMatches = value; }
		int RxValve.IConfig.ConsecutiveMatches => TargetConsecutiveMatches;
		int RxValve.IDevice.ConsecutiveMatches { get => RxValve.Device.ConsecutiveMatches; set => RxValve.Device.ConsecutiveMatches = value; }
		public virtual bool EnoughMatches => RxValve.EnoughMatches;

		public override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (sender == RxValve)
				NotifyPropertyChanged(e?.PropertyName);
			else
				base.OnPropertyChanged(sender, e);
		}

		public override void OnConfigChanged(object sender, PropertyChangedEventArgs e)
		{
			if (sender == RxValve)
				NotifyConfigChanged(e?.PropertyName);
			else
				base.OnConfigChanged(sender, e);
		}

		RxValve RxValve;
		public RS232Valve(IHacsDevice d = null) : base(d)
		{
			RxValve = new RxValve(d ?? this);
		}


		string ValveStateString(ValveState state) =>
			state == ValveState.Other ? "Between" : $"{state}";
		public override string ToString()
		{
			var sb = new StringBuilder($"{Name}: {ValveStateString(ValveState)}, Position = {Position}");
			if (isCalibrating)
				sb.Append(" (Calibrating)");
			else if (!Calibrated)
				sb.Append(" (Calibration needed)");
			var sb2 = new StringBuilder();
			sb2.Append($"\r\nPending Operations: {PendingOperations}");
			sb2.Append(Active ? $", Motion: {LastMotion}" : $", Last Motion: {LastMotion}");
			if (LastMotion != ValveState.Unknown)
			{
				if (Active)
					sb2.Append(StopRequested ? ", Stopping" : ", Active");
				else
					sb2.Append(StopRequested ? ", Stopped" : ActionSucceeded ? ", Succeeded" : ", Failed");
			}

			if (Operation != null)
			{
				var which = Active ? "Current" : "Prior";
				sb2.Append($"\r\n{which} Operation: \"{Operation.Name}\", Value: {Operation.Value}, Updates Received: {UpdatesReceived}");

				if (UpdatesReceived > 0)
				{
					var mv = $"Movement: {Device.Movement} / {Device.CommandedMovement}";
					var si = Device.Settings.CurrentLimit > 0 ? $"Current: {Current} / {Device.Settings.CurrentLimit} mA" : "";
					var st = Device.Settings.TimeLimit > 0 ? $"Time: {Elapsed} / {Device.Settings.TimeLimit} s" : "";
					var slim0 = Device.Settings.Limit0Enabled ? $"Limit0: {LimitSwitch0Engaged.ToString("Engaged", "Enabled")}" : "";
					var slim1 = Device.Settings.Limit1Enabled ? $"Limit1: {LimitSwitch1Engaged.ToString("Engaged", "Enabled")}" : "";
					var all = string.Join(" ", mv, si, st, slim0, slim1);
					if (all.Length > 0)
						sb2.Append($"\r\n{all}");
				}
			}
			if (Manager != null)
				sb2.Append($"\r\n{Manager.Name}[{Manager.Keys[this]}]");
			if (Linked)
				sb2.Append($"\r\nServo ControlOutput: {ControlOutput}, Updates: {RS232UpdatesReceived}");

			sb.Append(Utility.IndentLines(sb2.ToString()));
				return sb.ToString();
		}
	}
}
