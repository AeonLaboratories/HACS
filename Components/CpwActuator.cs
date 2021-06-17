using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace HACS.Components
{
	/// <summary>
	/// An actuator whose position is defined by a control pulse width like an RC servo.
	/// </summary>
	public class CpwActuator : Actuator, ICpwActuator, CpwActuator.IDevice, CpwActuator.IConfig
	{
		#region static

		//public static string DefaultConfiguration = "l-10 l-11 i0 t0";
		public static string EnableLimit0 = "l10";
		public static string EnableLimit1 = "l11";
		public static string DisableLimit0 = "l-10";
		public static string DisableLimit1 = "l-11";
		public static string EncodeConfiguration(bool Limit0Enabled, bool Limit1Enabled, int CurrentLimit, double TimeLimit) =>
			string.Join(" ",
					$"{(Limit0Enabled ? EnableLimit0 : DisableLimit0)}",
					$"{(Limit1Enabled ? EnableLimit1 : DisableLimit1)}",
					$"i{CurrentLimit}",
					$"t{TimeLimit:0.00}");

		public struct OperationSettings
		{
			/// <summary>
			/// Command pulse width
			/// </summary>
			public int Cpw;
			/// <summary>
			/// Stop if limit switch 0 is engaged.
			/// </summary>
			public bool Limit0Enabled;
			/// <summary>
			/// Stop if limit switch 0 is engaged.
			/// </summary>
			public bool Limit1Enabled;
			/// <summary>
			/// Stop if the actuator draws this much current. (0 == ignore current)
			/// </summary>
			public int CurrentLimit;
			/// <summary>
			/// Stop if this much time elapses. (0 == ignore elapsed time)
			/// </summary>
			public double TimeLimit;

			public OperationSettings(
				int cpw = 0, bool limit0Enabled = false, bool limit1Enabled = false,
				int currentLimit = 0, double timeLimit = 0.0)
			{
				Cpw = cpw;
				Limit0Enabled = limit0Enabled;
				Limit1Enabled = limit1Enabled;
				CurrentLimit = currentLimit;
				TimeLimit = timeLimit;
			}

			public OperationSettings(int cpw, string config)
			{
				Cpw = cpw;
				Limit0Enabled = config.Includes(EnableLimit0);
				Limit1Enabled = config.Includes(EnableLimit0);
				int currentLimit = 0;
				double timeLimit = 0.0;

				if (!config.IsBlank())
				{
					foreach (var token in config.Split(' '))
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
				CurrentLimit = currentLimit;
				TimeLimit = timeLimit;
			}

			public bool Match(OperationSettings config) =>
				config.Cpw == Cpw && LimitsMatch(config);

			public bool LimitsMatch(OperationSettings config) =>
				config.Limit0Enabled == Limit0Enabled &&
				config.Limit1Enabled == Limit1Enabled &&
				config.CurrentLimit == CurrentLimit &&
				config.TimeLimit == TimeLimit;


			public override string ToString() =>
				EncodeConfiguration(
					Limit0Enabled,
					Limit1Enabled,
					CurrentLimit,
					TimeLimit);
		}

		#endregion static

		#region Device interfaces

		public new interface IDevice : Actuator.IDevice
		{
			OperationSettings Settings { get; set; }
			bool ControlPulseEnabled { get; set; }
			bool LimitSwitch0Engaged { get; set; }
			bool LimitSwitch1Engaged { get; set; }
			int Current { get; set; }
			ActuatorController.ErrorCodes Errors { get; set; }
		}

		public new interface IConfig : Actuator.IConfig
		{
			// CpwActuator.DefaultConfiguration and 
			// ActuatorOperation.Configuration are used
			// to configure the Actuator
			OperationSettings Settings { get; }
		}

		public new IDevice Device => this;
		public new IConfig Config => this;

		#endregion Device interfaces

		IActuatorOperation Actuator.IDevice.Operation
		{
			get { return Operation; }
			set
			{
				Operation = value;
				if (Operation != null)
					targetSettings = new OperationSettings(Operation.Value, Operation.Configuration);
			}
		}


		// No public accessors. TargetSettings is set only by
		// the Operation setter, when the controller begins
		// servicing the operation.
		OperationSettings targetSettings;
		OperationSettings IConfig.Settings => targetSettings;
		OperationSettings IDevice.Settings
		{
			get => settings;
			set
			{
				if (Ensure(ref settings, value))
				{
					TimeLimit = settings.TimeLimit;
				}
			}
		}
		OperationSettings settings;

		/// <summary>
		/// The actuator's control pulse signal is enabled.
		/// </summary>
		public bool ControlPulseEnabled
		{
			get => controlPulseEnabled;
			protected set => Ensure(ref controlPulseEnabled, value);
		}
		bool controlPulseEnabled;
		bool IDevice.ControlPulseEnabled
		{
			get => ControlPulseEnabled;
			set => ControlPulseEnabled = value;
		}

		/// <summary>
		/// The actuator position is detectable.
		/// </summary>
		public virtual bool PositionDetectable { get => LimitSwitchEnabled; protected set { } }

		/// <summary>
		/// Limit switch 0 is engaged.
		/// </summary>
		public bool LimitSwitch0Engaged
		{
			get => limitSwitch0Engaged;
			protected set => Ensure(ref limitSwitch0Engaged, value);

		}
		bool limitSwitch0Engaged;
		bool IDevice.LimitSwitch0Engaged
		{
			get => LimitSwitch0Engaged;
			set => LimitSwitch0Engaged = value;
		}

		/// <summary>
		/// Limit switch 0 is engaged.
		/// </summary>
		public bool LimitSwitch1Engaged
		{
			get => limitSwitch1Engaged;
			protected set => Ensure(ref limitSwitch1Engaged, value);

		}
		bool limitSwitch1Engaged;
		bool IDevice.LimitSwitch1Engaged
		{
			get => LimitSwitch1Engaged;
			set => LimitSwitch1Engaged = value;
		}

		/// <summary>
		/// The most recent actuator current measurement.
		/// </summary>
		public int Current
		{
			get => current;
			protected set => Ensure(ref current, value);
		}
		int current;
		int IDevice.Current
		{
			get => Current;
			set => Current = value;
		}

		/// <summary>
		/// Error codes reported by the controller.
		/// </summary>
		public ActuatorController.ErrorCodes Errors
		{
			get => errors;
			protected set => Ensure(ref errors, value);
		}
		ActuatorController.ErrorCodes errors;
		ActuatorController.ErrorCodes IDevice.Errors
		{
			get => Errors;
			set => Errors = value;
		}

		public new ActuatorController Manager => base.Manager as ActuatorController;

		/// <summary>
		/// The controller detected that an enabled limit switch 
		/// is engaged.
		/// Note that this value is false if the controller hasn't checked.
		/// </summary>
		public virtual bool LimitSwitchDetected
		{
			get => UpdatesReceived > 0 &&
				(Device.Settings.Limit0Enabled && LimitSwitch0Engaged ||
				 Device.Settings.Limit1Enabled && LimitSwitch1Engaged);
			protected set { }
		}

		[JsonProperty, DefaultValue(80)]
		/// <summary>
		/// The maximum current (milliamps) expected when the
		/// actuator is idle. If an operation contains a non-zero
		/// current limit, the controller will wait until the actual
		/// current is below the IdleCurrentLimit before initiating 
		/// the operation movement.
		/// </summary>
		public virtual int IdleCurrentLimit
		{
			get => idleCurrentLimit;
			set => Ensure(ref idleCurrentLimit, value);
		}
		int idleCurrentLimit;

		/// <summary>
		/// The controller detected that the current limit was reached.
		/// This value is false if CurrentLimit is 0 (no limit set).
		/// Note that this value is also false if the controller hasn't checked.
		/// </summary>
		public virtual bool CurrentLimitDetected
		{
			get => UpdatesReceived > 0 &&
				Device.Settings.CurrentLimit > 0 &&
				Current >= Device.Settings.CurrentLimit;
			protected set { }
		}

		/// <summary>
		/// The actuator is currently moving.
		/// </summary>
		public override bool InMotion
		{
			get => Linked && Device.ControlPulseEnabled;
			protected set { }
		}


		/// <summary>
		/// Motion is prevented by a condition detected by the controller:
		/// a limit switch was engaged, a CurrentLimit was detected, or
		/// the operation reached its TimeLimit.
		/// </summary>
		public override bool MotionInhibited
		{
			get => UpdatesReceived > 0 &&
				(LimitSwitchDetected || CurrentLimitDetected || TimeLimitDetected);
			protected set { }
		}

		/// <summary>
		/// The device settings match the desired configuration.
		/// </summary>
		/// <returns></returns>
		public virtual bool Configured
		{
			get =>
				Operation == null ||
				Operation.Name == "Select" ||
				(UpdatesReceived > 0 && Device.Settings.Match(Config.Settings));
			protected set { }
		}

		public CpwActuator(IHacsDevice d = null) : base(d) { }

		public override string ToString()
		{
			return $"{Name}:";
		}

		bool LimitSwitchEnabled =>
			Operation != null &&
			targetSettings.Limit0Enabled || targetSettings.Limit1Enabled;

	}
}
