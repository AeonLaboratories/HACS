using HACS.Core;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Text;
using Utilities;

namespace HACS.Components
{
	public class VTColdfinger : StateManager<VTColdfinger.TargetStates, VTColdfinger.States>, 
		IVTColdfinger
	{
		#region HacsComponent

		[HacsConnect]
		protected virtual void Connect()
		{
			Heater = Find<IHeater>(heaterName);
            HeaterPid = Find<PidSetup>(heaterPidName);
            WarmHeaterPid = Find<PidSetup>(warmHeaterPidName);
            Coldfinger = Find<IColdfinger>(coldfingerName);
			WireThermometer = Find<IThermometer>(wireThermometerName);
			TopThermometer = Find<IThermometer>(topThermometerName);
			AmbientThermometer = Find<IChamber>("Ambient").Thermometer;
		}

        [HacsPreStop]
        protected virtual void PreStop()
        {
			switch (StopAction)
            {
                case StopAction.TurnOff:
                    HeaterOff();
                    Coldfinger.Standby();
                    break;
                case StopAction.TurnOn:
                    // not wise
                    break;
                case StopAction.None:
                    break;
                default:
                    break;
            }
        }

		#endregion HacsComponent


		[JsonProperty("Heater")]
		string HeaterName { get => Heater?.Name; set => heaterName = value; }
		string heaterName;
        /// <summary>
        /// The heater inside the device, used to increase the coldfinger temperature.
        /// </summary>
		public IHeater Heater
		{
			get => heater;
			set => Ensure(ref heater, value, OnPropertyChanged);
		}
		IHeater heater;

		[JsonProperty("Coldfinger")]
		string ColdfingerName { get => Coldfinger?.Name; set => coldfingerName = value; }
		string coldfingerName;
        /// <summary>
        /// The FTColdfinger that manages the liquid nitrogen level in the device.
        /// </summary>
		public IColdfinger Coldfinger
		{
			get => coldfinger;
			set => Ensure(ref coldfinger, value, OnPropertyChanged);
		}
		IColdfinger coldfinger;

		[JsonProperty("TopThermometer")]
		string TopThermometerName { get => TopThermometer?.Name; set => topThermometerName = value; }
		string topThermometerName;
        /// <summary>
        /// The temperature sensor at the top of the device.
        /// </summary>
		public IThermometer TopThermometer
		{
			get => topThermometer;
			set => Ensure(ref topThermometer, value, NotifyPropertyChanged);
		}
		IThermometer topThermometer;

		[JsonProperty("WireThermometer")]
		string WireThermometerName { get => WireThermometer?.Name; set => wireThermometerName = value; }
		string wireThermometerName;
        /// <summary>
        /// The temperature sensor on the heater wires just outside the device body.
        /// </summary>
		public IThermometer WireThermometer
		{
			get => wireThermometer;
			set => Ensure(ref wireThermometer, value, NotifyPropertyChanged);
		}
		IThermometer wireThermometer;

		/// <summary>
		/// The maximum safe heater wire temperature. If the wires exceed
		/// this temperature, power is removed from the heater.
		/// </summary>
		[JsonProperty, DefaultValue(60)]
		public int WireTemperatureLimit
		{
			get => wireTemperatureLimit;
			set => Ensure(ref wireTemperatureLimit, value);
		}
		int wireTemperatureLimit = 60;      // degC

        /// <summary>
        /// The desired coldfinger setpoint (used only in Regulate mode).
        /// </summary>
		public double Setpoint
		{
			get => Heater?.Setpoint ?? 0;
			set { if (Heater is IHeater h) h.Setpoint = value; }
		}

        /// <summary>
        /// The maximum allowed Heater power level.
        /// </summary>
		[JsonProperty, DefaultValue(16.67)]
		public double MaximumHeaterPower
		{
			get => maximumHeaterPower;
			set => Ensure(ref maximumHeaterPower, value);
		}
		double maximumHeaterPower = 16.67;

        [JsonProperty("HeaterPid")]
        string HeaterPidName { get => HeaterPid?.Name; set => heaterPidName = value; }
        string heaterPidName;
        /// <summary>
        /// The PidSetup to use when device Temperature is below 0 °C.
        /// </summary>
		public IPidSetup HeaterPid
		{
			get => heaterPid;
			set => Ensure(ref heaterPid, value, NotifyPropertyChanged);
		}
		IPidSetup heaterPid;

		/// <summary>
		/// The maximum Heater power level permitted when the device Temperature is above 0 °C.
		/// </summary>
		[JsonProperty, DefaultValue(3.0)]
		public double MaximumWarmHeaterPower
		{
			get => maximumWarmHeaterPower;
			set => Ensure(ref maximumWarmHeaterPower, value);
		}
		double maximumWarmHeaterPower = 3.0;

        [JsonProperty("WarmHeaterPid")]
        string WarmHeaterPidName { get => WarmHeaterPid?.Name; set => warmHeaterPidName = value; }
        string warmHeaterPidName;
        /// <summary>
        /// The PidSetup to use when device Temperature is above 0 °C.
        /// </summary>
		public IPidSetup WarmHeaterPid
		{
			get => warmHeaterPid;
			set => Ensure(ref warmHeaterPid, value, NotifyPropertyChanged);
		}
		IPidSetup warmHeaterPid;


		/// <summary>
		/// Maximum temperature at which to use the trap for collecting CO2.
		/// </summary>
		[JsonProperty, DefaultValue(-170)]
        public int ColdTemperature
		{
			get => coldTemperature;
			set => Ensure(ref coldTemperature, value);
		}
		int coldTemperature = -170;

        /// <summary>
        /// Temperature to use for cleaning (drying) the VTT.
        /// </summary>
        [JsonProperty, DefaultValue(50)]
        public int CleanupTemperature
		{
			get => cleanupTemperature;
			set => Ensure(ref cleanupTemperature, value);
		}
		int cleanupTemperature = 50;

		/// <summary>
        /// This LN valve Operation determines the Coldfinger's liquid nitrogen trickle flow rate when the Heater is on.
        /// </summary>
        [JsonProperty]
		public string HeaterOnTrickle
		{
			get => heaterOnTrickle;
			set => Ensure(ref heaterOnTrickle, value);
		}
		string heaterOnTrickle;

		/// <summary>
		/// This LN valve Operation the Coldfinger's liquid nitrogen trickle flow rate when the Heater is off.
		/// </summary>
		[JsonProperty]
		public string HeaterOffTrickle
		{
			get => heaterOffTrickle;
			set => Ensure(ref heaterOffTrickle, value);
		}
		string heaterOffTrickle;

		/// <summary>
		/// The available target states for a VTColdfinger. The device
		/// is controlled by setting TargetState to one of these values.
		/// </summary>
		public enum TargetStates
		{
			/// <summary>
			/// Turn off active warming and cooling.
			/// </summary>
			Standby,
			/// <summary>
			/// Warm coldfinger until thawed, then switch to Standby.
			/// </summary>
			Thaw,
			/// <summary>
			/// Freeze if needed and raise the LN, to the level of a trickling overflow if possible.
			/// </summary>
			Freeze,
			/// <summary>
			/// Control the temperature using the Heater and FTColdinger as needed.
			/// </summary>
			Regulate
		}


        /// <summary>
        /// The possible states of a VTColdfinger. The device is always
        /// in one of these states.
        /// </summary>
		public enum States
		{
			/// <summary>
			/// Coldfinger temperature is not being actively controlled.
			/// </summary>
			Standby,
			/// <summary>
			/// Warming coldfinger to ambient temperature.
			/// </summary>
			Thawing,
			/// <summary>
			/// Cooling the coldfinger using liquid nitrogen, working to reach the maximum level liquid nitrogen.
			/// </summary>
			Freezing,
            /// <summary>
            /// Maintaining the maximum liquid nitrogen level, with a trickling overflow if possible.
            /// </summary>
			Frozen,
            /// <summary>
            /// Using the Heater to reach and maintain the programmed Setpoint temperature.
            /// </summary>
            Regulating
        }


        /// <summary>
        /// Whether this device has reached and is currently maintaining its maximum level of liquid nitrogen.
        /// </summary>
		public bool Frozen => State == States.Frozen;

        /// <summary>
        /// The process temperature, measured at the bottom of the coldfinger. This is the temperature
        /// regulated by the device to the programmed Setpoint.
        /// </summary>
		public double Temperature => Heater.Temperature;

        /// <summary>
        /// The temperature of the Coldfinger's liquid nitrogen level sensor. Indicates
        /// the level of liquid nitrogen in the device's reservoir.
        /// </summary>
		public double ColdfingerTemperature => Coldfinger.Temperature;


		IThermometer AmbientThermometer;
		double Ambient => AmbientThermometer?.Temperature ?? 22.0;


        /// <summary>
        /// The present state of the device.
        /// </summary>
        public override States State
		{
			get
			{
				if (TargetState == TargetStates.Standby)
					return States.Standby;
				if (TargetState == TargetStates.Thaw)
					return States.Thawing;
				if (TargetState == TargetStates.Regulate)
					return States.Regulating;

				//else TargetState is Freeze
				if (Coldfinger.State == Components.Coldfinger.States.Raised)
					return States.Frozen;
				else
					return States.Freezing;
			}
		}


		// TODO: shouldn't this also be true if the coldfinger is active?
		/// <summary>
		/// Whether the heater is on or off.
		/// </summary>
		public bool IsOn => Heater.IsOn;
		public bool IsOff => Heater.IsOff;
		public OnOffState OnOffState => Heater.OnOffState;

        /// <summary>
        /// Turn the Heater on, begin Regulating the Temperature to the programmed Setpoint.
        /// </summary>
        public bool TurnOn() { Regulate(); return true; }

        /// <summary>
        /// Turn the Heater off. Maintain cooling if it is presently active; otherwise,
        /// enter Standby mode.
        /// </summary>
        public bool TurnOff()
        {
			HeaterOff();
			if (Coldfinger.IsActivelyCooling)
				Freeze();
			else
				Standby();
            return true;
        }

        /// <summary>
        /// Turns the Heater on or off. Cooling is not affected.
        /// </summary>
        /// <param name="on">true => on, false => off</param>
        public bool TurnOnOff(bool on)
        {
            if (on) return TurnOn();
            return TurnOff();
        }

        /// <summary>
        /// What to do with the hardware device when this instance is Stopped.
        /// </summary>
        public StopAction StopAction { get; set; } = StopAction.TurnOff;


		/// <summary>
		/// Stop actively cooling and heating.
		/// </summary>
		public void Standby()
		{
			HeaterOff();
			Coldfinger.Standby();
			ChangeState(TargetStates.Standby);
		}

        /// <summary>
        /// Warm the coldfinger with forced air.
        /// </summary>
		public void Thaw() => ChangeState(TargetStates.Thaw);

        /// <summary>
        /// Reach and maintain the maximum level of liquid nitrogen in the
        /// reservoir, with a trickling overflow if possible.
        /// </summary>
		public void Freeze() => ChangeState(TargetStates.Freeze);

        /// <summary>
        /// Use the Heater to control the Temperature to the pre-programmed Setpoint.
        /// </summary>
        public void Regulate()
        {
            if (Heater is HC6HeaterB2.IConfig h)
                Regulate(h.Setpoint);
        }

        /// <summary>
        /// Use the Heater to control the Temperature to the provided setpoint.
        /// </summary>
        /// <param name="setpoint">the desired temperature</param>
        public void Regulate(double setpoint)
		{
			// override heater setpoint.
			Heater.Setpoint = setpoint;
			ChangeState(TargetStates.Regulate);
		}

        /// <summary>
        /// Ensures the desired TargetState is in effect.
        /// </summary>
        /// <param name="state">the desired TargetState</param>
        public void EnsureState(TargetStates state)
		{
			switch (state)
			{
				case TargetStates.Standby:
					Standby();
					break;
				case TargetStates.Thaw:
					Thaw();
					break;
				case TargetStates.Freeze:
					Freeze();
					break;
				case TargetStates.Regulate:
					Regulate();
					break;
			}
		}


		double heaterMaxPower => Heater.Temperature < 0 && 
			WireThermometer.Temperature < WireTemperatureLimit - 10 ?
				MaximumHeaterPower :
				MaximumWarmHeaterPower;

		IPidSetup pid =>
            Setpoint < Ambient ?
                HeaterPid :
                WarmHeaterPid;


        void configureHeater()
		{
			if (Heater is HC6HeaterB2 h)
            {
                h.Pid = pid;
				h.MaximumPowerLevel = heaterMaxPower;
            }
        }

        bool heaterConfigured()
        {
            if (Heater is HC6HeaterB2 h)
                return
                    h.PidConfigured() &&
                    h.MaximumPowerLevel == heaterMaxPower;
            else
                return false;
        }

        void HeaterOff()
		{
			Heater.TurnOff();
            updateTrickle(false);
		}

		void manageHeaterAndColdfinger()
		{
			bool safeToOperateHeater = heaterConfigured();

            bool NotManualMode =
                (Heater is HC6HeaterB2.IConfig h && !h.ManualMode);
			if (NotManualMode)
			{
				if (Heater.Setpoint < Coldfinger.AirTemperature)
				{
                    Coldfinger.Raise();
					if (Coldfinger.Temperature > -150)
						safeToOperateHeater = false;
				}
				else
				{
					if (!Coldfinger.Thawed)
						Coldfinger.Thaw();
					else
						Coldfinger.Standby();
				}
			}

			if (WireThermometer.Temperature > WireTemperatureLimit ||
				Temperature > WireTemperatureLimit ||
				TopThermometer.Temperature > WireTemperatureLimit)
				safeToOperateHeater = false;

			if (!safeToOperateHeater)
			{
				HeaterOff();
			}
			else if (!Heater.IsOn)
			{
                updateTrickle(true);
                Heater.TurnOn();
			}
		}

		void updateTrickle(bool heaterOn)
		{
            Coldfinger.Trickle = heaterOn ? HeaterOnTrickle : HeaterOffTrickle;
		}

		void ManageState()
		{
			if (!Connected || Hacs.Stopping) return;
			configureHeater();

			switch (TargetState)
			{
				case TargetStates.Regulate:
					manageHeaterAndColdfinger();
					break;
				case TargetStates.Freeze:
					HeaterOff();
					Coldfinger.Raise();
					break;
				case TargetStates.Thaw:
					HeaterOff();
					if (Coldfinger.Thawed)
						Standby();
					else if (Coldfinger.State != Components.Coldfinger.States.Thawing)
						Coldfinger.Thaw();
					break;
				case TargetStates.Standby:
					if (Heater.IsOn)
						Heater.TurnOff();
					if (Coldfinger.State != Components.Coldfinger.States.Standby)
						Coldfinger.Standby();
					break;
				default:
					break;
			}
		}


		protected virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var propertyName = e?.PropertyName;
			if (sender == Heater)
			{
				if (propertyName == nameof(IHeater.Temperature))
					NotifyPropertyChanged(nameof(IVTColdfinger.Temperature));
				else if (propertyName == nameof(IHeater.Setpoint))
					NotifyPropertyChanged(nameof(IVTColdfinger.Setpoint));
				else
					NotifyPropertyChanged(nameof(Heater));
			}
			else if (sender == Coldfinger)
			{
				if (propertyName == nameof(IColdfinger.Temperature))
					NotifyPropertyChanged(nameof(IVTColdfinger.ColdfingerTemperature));
				else
					NotifyPropertyChanged(nameof(Coldfinger));
			}
			else
				NotifyPropertyChanged(sender, e);
		}

		public VTColdfinger()
		{
			(this as IStateManager).ManageState = ManageState;
		}


		public override string ToString()
		{
			StringBuilder sb = new StringBuilder($"{Name}: {State}, {Temperature:0.#############} °C");
			switch (State)
			{
				case States.Standby:
					break;
				case States.Regulating:
					sb.Append($", Setpoint = {Setpoint} °C");
					break;
				default:
					sb.Append($", Target = {Coldfinger.Target} °C");
					break;
			}
            StringBuilder sb2 = new StringBuilder();
            sb2.Append($"\r\n{Heater}");
            sb2.Append($"\r\n{Coldfinger}");
            sb2.Append($"\r\n{WireThermometer}");
            sb2.Append($"\r\n{TopThermometer}");
            sb.Append(Utility.IndentLines(sb2.ToString()));
            return sb.ToString();
		}
	}

	public class VTFlowChamber : VTColdfinger
	{
		protected override void Connect()
		{
			base.Connect();
			FlowChamber = Find<FlowChamber>(flowChamberName);
		}

		[JsonProperty("FlowChamber")]
		string FlowChamberName { get => FlowChamber?.Name; set => flowChamberName = value; }
		string flowChamberName;
		public FlowChamber FlowChamber { get; set; }
	}
}
