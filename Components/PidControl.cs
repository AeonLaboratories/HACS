using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Threading;

namespace HACS.Components
{
    /// <summary>
    /// Implements a rudimentary "parallel" or non-interacting
    /// PID control algorithm. This implementation was designed 
    /// for temperature management. Class properties GetSetpoint,
    /// GetProcessVariable, and SetControlOutput must be set
    /// prior to invoking Start().
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class PidControl : HacsComponent, IPidControl
    {
        #region HacsComponent

        [HacsConnect]
        protected virtual void Connect()
        {
            PidSetup = Find<PidSetup>(pidSetupName);
        }

        #endregion HacsComponent

        [JsonProperty("PidSetup")]
        string PidSetupName { get => PidSetup?.Name; set => pidSetupName = value; }
        string pidSetupName;
        public IPidSetup PidSetup
        {
            get => pidSetup;
            set => Ensure(ref pidSetup, value, NotifyPropertyChanged);
        }
        IPidSetup pidSetup;

        /// <summary>
        /// How often the control output should be updated after
        /// Start() is called. Values longer than about 25% of the 
        /// process dead time may cause sluggish responsiveness. 
        /// Values shorter than about 10% of the dead time generally 
        /// do not improve responsiveness.
        /// </summary>
        [JsonProperty, DefaultValue(1000)]
        public int MillisecondsUpdate
        {
            get => millisecondsUpdate;
            set => Ensure(ref millisecondsUpdate, value);
        }
        int millisecondsUpdate = 1000; // cycle time

        [JsonProperty, DefaultValue(100.0)]
        public double ControlOutputLimit
        {
            get => controlOutputLimit;
            set => Ensure(ref controlOutputLimit, value);
        }
        double controlOutputLimit = 100.0;        // %

        /// <summary>
        /// The PV value that the plant tends toward when CO is 0.
        /// </summary>
        [JsonProperty, DefaultValue(0.0)]
        public double ReferencePoint
        {
            get => referencePoint;
            set => Ensure(ref referencePoint, value);
        }
        double referencePoint = 0.0;

        /// <summary>
        /// The value of the process variable (e.g., temperature) 
        /// at the beginning of the prior control output Update.
        /// </summary>
        double priorPv;

        /// <summary>
        /// The accumulated error term. Ideally, this value approaches
        /// the control output value (e.g., power level) required to
        /// maintain the process variable (e.g., temperature) at
        /// the programmed setpoint.
        /// </summary>
        double integral;

        /// <summary>
        /// A method that returns the setpoint (SP, e.g.,
        /// the desired process temperature).
        /// </summary>
        public Func<double> GetSetpoint { get; set; }
        /// <summary>
        /// A method that returns the process variable (PV, e.g., 
        /// the process temperature).
        /// </summary>
        public Func<double> GetProcessVariable { get; set; }
        /// <summary>
        /// A method that accepts the control output (CO, e.g., a
        /// power level).
        /// </summary>
        public Action<double> UpdateControlOutput { get; set; }

        /// <summary>
        /// Kc = controller gain
        /// </summary>
        double Kc => PidSetup.Gain;

        /// <summary>
        /// Ci = 1 / Ti (note: no Kc)
        /// </summary>
        double Ci => PidSetup.Integral;

        /// <summary>
        /// Cd = Kc * Td
        /// </summary>
        double Cd => PidSetup.Derivative;

        /// <summary>
        /// Cpr = 1 / gp  (= step test dPV/dCO) 
        /// </summary>
        double Cpr => PidSetup.Preset;

        /// <summary>
        /// Resets the PID to its initial state.
        /// </summary>
        void Reset()
        {
            priorPv = GetProcessVariable();
            integral = -1;      // trigger a preset
        }

        /// <summary>
        /// Reads the current process variable PV, checks the setpoint SP, 
        /// and produces a new control output, CO.
        /// </summary>
        void Update()
        {
            if (GetProcessVariable == null || GetSetpoint == null || UpdateControlOutput == null) return;

            double pv = GetProcessVariable();
            double co = Kc * (GetSetpoint() - pv);        // the p term

            // The derivative term is meaningless on the first 
            // pass. Set priorPV = pv when entering auto mode.
            co += Cd * (priorPv - pv);               // add the d term

            // Ideally, the integral term always equals the co that
            // corresponds to the current pv.
            // Set integral to -1 when entering auto mode, to
            // trigger a preset.
            if (co > ControlOutputLimit || integral < 0.0)
                integral = Cpr * (pv - ReferencePoint);
            else
                integral += Ci * co;     // Note: Kc is in co; Ci should not include it

            co += integral;                         // add the i term
            if (co < 0.0) co = 0.0;
            if (co > ControlOutputLimit) co = ControlOutputLimit;
            priorPv = pv;
            UpdateControlOutput?.Invoke(co);
        }


        Thread autoThread;
        ManualResetEvent stopSignal = new ManualResetEvent(false);

        /// <summary>
        /// The minimum process variable (e.g., temperature) that the 
        /// plant is capable of controlling.
        /// </summary>
        [JsonProperty, DefaultValue(0.0)]
        public double MinimumControlledProcessVariable
        {
            get => minimumControlledProcessVariable;
            set => Ensure(ref minimumControlledProcessVariable, value);
        }
        double minimumControlledProcessVariable = 0.0;

        /// <summary>
        /// The control output value (e.g., power level) to maintain
        /// when the process variable (e.g., temperature) is below
        /// the MinimumControlledProcessVariable setting.
        /// </summary>
        [JsonProperty, DefaultValue(0.0)]
        public double BlindControlOutput
        {
            get => blindControlOutput;
            set => Ensure(ref blindControlOutput, value);
        }
        double blindControlOutput = 0.0;

        /// <summary>
        /// Whether the output is actively being controlled.
        /// </summary>
        public bool Busy => autoThread != null && autoThread.IsAlive;

        /// <summary>
        /// Start managing the control output
        /// </summary>
        public void Start()
        {
            if (Busy || GetProcessVariable == null || GetSetpoint == null || UpdateControlOutput == null) return;
            stopSignal.Reset();
            autoThread = new Thread(autoLoop)
            {
                Name = $"{Name} Auto Update Loop",
                IsBackground = true
            };
            autoThread.Start();
        }

        /// <summary>
        /// Stop managing the control output.
        /// Note: This does not turn the control output off; it
        /// remains at its current value.
        /// </summary>
        public void Stop() => stopSignal.Set();

        void autoLoop()
        {
            try
            {
                bool blind = true;      // can't see process variable
                bool stopAuto = false;
                while (!stopAuto)
                {
                    if (GetProcessVariable() < MinimumControlledProcessVariable)
                    {
                        UpdateControlOutput?.Invoke(BlindControlOutput);
                        blind = true;
                    }
                    else
                    {
                        if (blind) Reset();
                        Update();
                        blind = false;
                    }
                    stopAuto = stopSignal.WaitOne(MillisecondsUpdate);
                }
            }
            catch { }
        }
    }
}
