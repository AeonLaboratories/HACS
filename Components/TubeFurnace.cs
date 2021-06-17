using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Text;
using Utilities;

namespace HACS.Components
{
    // TODO: is this, properly, SerialTubeFurnace?
    // All the TubeFurnaces we've encountered so far are operated by
    // serial comms..
    public class TubeFurnace : Oven, ITubeFurnace,
        TubeFurnace.IDevice, TubeFurnace.IConfig
    {

        #region HacsComponent

        [HacsConnect]
        protected virtual void Connect()
        {
            if (SetpointRamp != null)
            {
                SetpointRamp.Device = this;
                SetpointRamp.GetProcessVariable = () => Temperature;
            }
        }

        #endregion HacsComponent

        #region Class interface properties and methods

        #region Device interfaces

        public new interface IDevice : Oven.IDevice
        {
            new double Setpoint { get; set; }
        }
        public new interface IConfig : Oven.IConfig
        {
            new double Setpoint { get; }
        }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        #region Settings

        /// <summary>
        /// The Setpoint temperature in °C.
        /// </summary>
        [JsonProperty("Setpoint")]
        public override double Setpoint
        {
            get => base.Setpoint;
            set
            {
                if (value < MinimumSetpoint)
                    value = MinimumSetpoint;
                else if (value > MaximumSetpoint)
                    value = MaximumSetpoint;
                if (base.Setpoint != value)
                {
                    base.Setpoint = value;
                    SerialController.Hurry = true;
                }
            }
        }

        /// <summary>
        /// Trying to set the Setpoint below this 
        /// causes the Setpoint to be this value instead.
        /// </summary>
        [JsonProperty, DefaultValue(1.0)]
        public double MinimumSetpoint
        {
            get => minimumSetpoint;
            set => Ensure(ref minimumSetpoint, value);
        }
        double minimumSetpoint = 1.0;

        /// <summary>
        /// Trying to set the Setpoint above this 
        /// causes the Setpoint to be this value instead.
        /// </summary>
        [JsonProperty, DefaultValue(1200.0)]
        public double MaximumSetpoint
        {
            get => maximumSetpoint;
            set => Ensure(ref maximumSetpoint, value);
        }
        double maximumSetpoint = 1200.0;

        /// <summary>
        /// Set to null if Setpoint ramping is not used.
        /// </summary>
        [JsonProperty]
        public SetpointRamp SetpointRamp
        {
            get => setpointRamp;
            set => Ensure(ref setpointRamp, value);
        }
        SetpointRamp setpointRamp;

        [JsonProperty]
        public virtual SerialController SerialController
        {
            get => serialController;
            set
            {
                serialController = value;
                if (serialController != null)
                {
                    serialController.SelectServiceHandler = SelectService;
                    serialController.ResponseProcessor = ValidateResponse;
                    serialController.LostConnection -= OnControllerLost;
                    serialController.LostConnection += OnControllerLost;
                }
                NotifyPropertyChanged();
            }
        }
        SerialController serialController;


        #endregion Settings

        public virtual double TimeLimit
        {
            get => timeLimit;
            set => Ensure(ref timeLimit, value);
        }
        double timeLimit;
        public virtual bool UseTimeLimit
        {
            get => useTimeLimit;
            set => Ensure(ref useTimeLimit, value);
        }
        bool useTimeLimit;

        /// <summary>
        /// The "SetpointRamped" working setpoint. If no SetpointRamp 
        /// has been defined, this is the same as Setpoint.
        /// </summary>
        public double RampingSetpoint => SetpointRamp?.WorkingSetpoint ?? Setpoint;

        public virtual double MinutesInState => MillisecondsInState / 60000.0;
        public virtual double MinutesOn => IsOn ? MinutesInState : 0;
        public virtual double MinutesOff => !IsOn ? MinutesInState : 0;

        /// <summary>
        /// Turn the furnace on.
        /// </summary>
        /// <returns></returns>
        public new virtual bool TurnOn()
        {
            if (base.TurnOn())
            {
                SerialController.Hurry = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Turn the furnace off.
        /// </summary>
        /// <returns></returns>
        public new virtual bool TurnOff()
        {
            if (base.TurnOff())
            {
                SerialController.Hurry = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Set the furnace temperature and turn it on.
        /// Later, if the furnace is still on when the specified time 
        /// elapses, it is automatically turned off.
        /// </summary>
        /// <param name="setpoint">Desired furnace temperature (°C)</param>
        /// <param name="minutes">Maximum number of minutes to remain on</param>
        public virtual void TurnOn(double setpoint, double minutes)
        {
            TimeLimit = minutes;
            UseTimeLimit = true;
            TurnOn(setpoint);
        }


        #region State management

        public virtual bool Ready => SerialController.Ready;

        #endregion State management

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder($"{Name}:");
            //if (Type != ThermocoupleType.None)
            //    sb.Append($" {Value:0.0} {UnitSymbol} ({Type})");

            //StringBuilder sb2 = new StringBuilder();
            //if (manager != null)
            //    sb2.Append($"\r\n{manager.Name}[{manager.Keys[this]}]");
            //if (Errors != 0)
            //    sb2.Append($"\r\nError = {Errors}");
            //sb.Append(Utility.IndentLines(sb2.ToString()));
            return sb.ToString();
        }

        #endregion Class interface properties and methods


        protected Stopwatch StateStopwatch = new Stopwatch();


        #region Controller interactions

        protected virtual bool LogEverything => SerialController?.LogEverything ?? false;
        protected virtual LogFile Log => SerialController?.Log;

        // to be overridden by derived class
        /// <summary>
        /// The SerialController invokes this method to obtain the next
        /// SerialController.Command.
        /// The Command contains a string message (the "command"), the 
        /// number of responses to expect in return, and whether to 
        /// "Hurry". Hurry tells the controller to check back here 
        /// for another command as soon as the expected responses 
        /// have been received and validated. Otherwise, the controller 
        /// will check again after a timeout period.
        /// </summary>
        /// <returns></returns>
        protected virtual SerialController.Command SelectService()
        {
            return new SerialController.Command("", 0, false);
        }

        // to be overridden by derived class
        /// <summary>
        /// Accepts a response string from the SerialController
        /// and returns whether it is a valid response or not.
        /// </summary>
        /// <param name="response"></param>
        /// <returns>true if the response is valid</returns>
        protected virtual bool ValidateResponse(string response, int which)
        {
            return true;
        }

        protected virtual void OnControllerLost(object sender, EventArgs e) { }

        #endregion Controller interactions

    }
}