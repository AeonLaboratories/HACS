using HACS.Components;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using Utilities;

namespace HACS
{

    // TODO: Where should this class reside?

    /// <summary>
    /// This class ramps the Setpoint to programmed levels at 
    /// the given rate.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class SetpointRamp
    {
        [JsonProperty]
        public double Setpoint
        {
            get { return setpoint; }
            set
            {
                setpoint = value;
                startNeeded = true;
            }
        }
        double setpoint;
        bool startNeeded = true;

        /// <summary>
        /// E.g., Process variable units per per minute. Default is 10.
        /// </summary>
        [JsonProperty, DefaultValue(10.0)]
        public double Rate { get; set; } = 10.0;

        /// <summary>
        /// If the process variable differs from the working setpoint by
        /// this amount or more, the ramp is recalculated.
        /// </summary>
        [JsonProperty, DefaultValue(30.0)]
        public double MaxError { get; set; } = 30.0; // default is 3 times ramp rate


        public ISwitch Device { get; set; }
        public Func<double> GetProcessVariable { get; set; }

        /// <summary>
        /// Provide this value if the ProcessVariable sensor
        /// is unable to distinquish values below a certain limit.
        /// </summary>
        public double MinimumStartValue { get; set; } = -273.15;
        public double StartValue;
        public Stopwatch StateStopwatch = new Stopwatch();

        public double WorkingSetpoint
        {
            get
            {
                if (!Device.IsOn) return Setpoint;
                if (startNeeded) StartRamp();
                double wsp = workingSetpoint();

                if (Math.Abs(GetProcessVariable() - wsp) > MaxError)
                {
                    StartRamp();
                    wsp = workingSetpoint();
                }
                return wsp;
            }
        }
        double workingSetpoint()
        {
            int dT = (StateStopwatch.Elapsed.TotalMinutes * Math.Abs(Rate)).ToInt();
            if (Setpoint > StartValue)
                return Math.Min(Setpoint, StartValue + dT);
            else
                return Math.Max(Setpoint, StartValue - dT);
        }

        void StartRamp()
        {
            StartValue = Math.Max(MinimumStartValue, GetProcessVariable());
            StateStopwatch.Restart();
            startNeeded = false;
        }
    }
}
