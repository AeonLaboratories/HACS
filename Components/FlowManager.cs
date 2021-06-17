using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Threading;
using Utilities;

namespace HACS.Components
{
    /// <summary>
    /// Controls a FlowValve to reach and maintain a specific condition monitored by the Meter.
    /// </summary>
    public class FlowManager : HacsComponent, IFlowManager
    {
		#region HacsComponent

		[HacsConnect]
		protected virtual void Connect()
		{
			FlowValve = Find<RS232Valve>(flowValveName);
			Meter = Find<IMeter>(meterName);
		}

		#endregion HacsComponent

		[JsonProperty("FlowValve")]
		string FlowValveName { get => FlowValve?.Name; set => flowValveName = value; }
		string flowValveName;
        /// <summary>
        /// The valve that adjusts the flow which changes the DynamicQuantity Value.
        /// </summary>
		public IRS232Valve FlowValve
        {
            get => flowValve;
            set => Ensure(ref flowValve, value, NotifyPropertyChanged);
        }
        IRS232Valve flowValve;

		[JsonProperty("Meter")]
		string MeterName { get => Meter?.Name; set => meterName = value; }
		string meterName;
        /// <summary>
        /// The Meter that provides the Value that varies as the flow valve is adjusted.
        /// </summary>
		public IMeter Meter
        {
            get => meter;
            set => Ensure(ref meter, value, NotifyPropertyChanged);
        }
        IMeter meter;

        /// <summary>
        /// Minimum control loop period.
        /// </summary>
		[JsonProperty, DefaultValue(35)]
		public int MillisecondsTimeout
        {
            get => millisecondsTimeout;
            set => Ensure(ref millisecondsTimeout, value);
        }
        int millisecondsTimeout = 35;


        /// <summary>
        /// Nominal time between valve movements
        /// </summary>
		[JsonProperty, DefaultValue(0.75)]
		public double SecondsCycle
        {
            get => secondsCycle;
            set => Ensure(ref secondsCycle, value);
        }
        double secondsCycle = 0.75;

        /// <summary>
        /// An initial valve movement, to make before entering the flow
        /// management control loop. Sometimes used to "crack open" the flow valve.
        /// </summary>
		[JsonProperty, DefaultValue(0)]
		public int StartingMovement
        {
            get => startingMovement;
            set => Ensure(ref startingMovement, value);
        }
        int startingMovement = -0;       // usually negative

        /// <summary>
        /// Maximum valve movement for any single adjustment, in valve Position units
        /// </summary>
		[JsonProperty, DefaultValue(24)]
		public int MaximumMovement
        {
            get => maximumMovement;
            set => Ensure(ref maximumMovement, value);
        }
        int maximumMovement = 24;

        /// <summary>
        /// A limiting Meter.Value rate of change, to regulate the flow or ramp rate while working
        /// toward the TargetValue.
        /// </summary>
        [JsonProperty, DefaultValue(10)]
        public int MaximumRate
        {
            get => maximumRate;
            set => Ensure(ref maximumRate, value);
        }
        int maximumRate = 10;

        /// <summary>
        /// Dead time plus lag, in seconds. The time expected to pass
        /// between a valve movement and the end of its effect on Meter.Value.
        /// </summary>
		[JsonProperty, DefaultValue(60)]
		public int Lag
        {
            get => lag;
            set => Ensure(ref lag, value);
        }
        int lag = 60;

        /// <summary>
        /// The tolerable deviation between Value and TargetValue, for which
        /// no valve adjustment will be made.
        /// </summary>
		[JsonProperty, DefaultValue(0.02)]
		public double Deadband
        {
            get => deadband;
            set => Ensure(ref deadband, value);
        }
        double deadband = 0.02;

        /// <summary>
        /// If false, Deadband is a fixed constant in units of TargetValue; 
        /// if true, the dead band is the product of Deadband and TargetValue.
        /// </summary>
		[JsonProperty, DefaultValue(true)]
		public bool DeadbandIsFractionOfTarget
        {
            get => deadbandIsFractionOfTarget;
            set => Ensure(ref deadbandIsFractionOfTarget, value);
        }
        bool deadbandIsFractionOfTarget = true;

        /// <summary>
        /// Relates expected error (TargetValue vs. anticipated Value) to valve movement (in Positions).
        /// Negative if Value is reduced by an Opening movement.
        /// </summary>
		[JsonProperty, DefaultValue(1)]
		public double Gain
        {
            get => gain;
            set => Ensure(ref gain, value);
        }
        double gain = 1;

        /// <summary>
        /// Divide Gain by Deadband when computing the amount to move the valve.
        /// </summary>
		[JsonProperty, DefaultValue(true)]
		public bool DivideGainByDeadband
        {
            get => divideGainByDeadband;
            set => Ensure(ref divideGainByDeadband, value);
        }
        bool divideGainByDeadband = true;

        /// <summary>
        /// Stop the flow manager if FlowValve reaches its fully opened position.
        /// </summary>
		[JsonProperty, DefaultValue(false)]
		public bool StopOnFullyOpened
        {
            get => stopOnFullyOpened;
            set => Ensure(ref stopOnFullyOpened, value);
        }
        bool stopOnFullyOpened = false;

        /// <summary>
        /// Stop the flow manager if FlowValve reaches its fully closed position.
        /// </summary>
        [JsonProperty, DefaultValue(false)]
		public bool StopOnFullyClosed
        {
            get => stopOnFullyClosed;
            set => Ensure(ref stopOnFullyClosed, value);
        }
        bool stopOnFullyClosed = false;

        /// <summary>
        /// The Value that the flow manager works to achieve.
        /// </summary>
		public double TargetValue
        {
            get => targetValue;
            set => Ensure(ref targetValue, value);
        }
        double targetValue = 0;

        /// <summary>
        /// Use Meter's rate of change instead of its absolute value.
        /// </summary>
		[JsonProperty, DefaultValue(false)]
		public bool UseRateOfChange
        {
            get => useRateOfChange;
            set => Ensure(ref useRateOfChange, value);
        }
        bool useRateOfChange = false;

        /// <summary>
        /// A StepTracker to receive ongoing process state messages.
        /// </summary>
        public StepTracker ProcessStep
        {
            get => processStep ?? StepTracker.Default;
            set => Ensure(ref processStep, value);
        }
        StepTracker processStep;


        Thread managerThread;
        AutoResetEvent stopSignal = new AutoResetEvent(false);

        /// <summary>
        /// Flow is actively being controlled.
        /// </summary>
        public bool Busy => managerThread != null && managerThread.IsAlive;

        /// <summary>
        /// Start managing the flow with the current TargetValue
        /// </summary>
        public void Start() => Start(TargetValue);

        /// <summary>
        /// Start managing the flow with this new TargetValue
        /// </summary>
        public void Start(double targetValue)
        {
            TargetValue = targetValue;
            if (Busy) return;

            managerThread = new Thread(manageFlow)
            {
                Name = $"{FlowValve.Name} FlowManager",
                IsBackground = true
            };
            managerThread.Start();
        }

        /// <summary>
        /// Stop managing the flow
        /// </summary>
        public void Stop() => stopSignal.Set();

        void manageFlow()
        {
            bool stopRequested = false;

            //ProcessStep.Start("");

            var operationName = "_Move";   // temporary
            var operation = FlowValve.FindOperation(operationName) as ActuatorOperation;
            if (operation != null) FlowValve.ActuatorOperations.Remove(operation);
            operation = new ActuatorOperation()
            {
                Name = operationName,
                Value = StartingMovement,
                Incremental = true,
                Configuration = FlowValve.FindOperation("Close").Configuration
            };
            FlowValve.ActuatorOperations.Add(operation);

            Stopwatch actionStopwatch = new Stopwatch();

            // starting motion
            if (StartingMovement != 0)
                FlowValve.DoWait(operation);
            actionStopwatch.Restart();

            var deadband = DeadbandIsFractionOfTarget ? Deadband * TargetValue : Deadband;
            var gain = DivideGainByDeadband ? Gain / deadband : Gain;
            if (FlowValve.OpenIsPositive) gain = -gain;     // usually, positive movement means closing

            var pos = FlowValve.Position;
            var roc = Meter.RateOfChange.Value;
            var ppr = gain;         // Positions per dRateOfChange
            var priorPos = pos;
            var priorRate = roc;

            while (!(stopRequested || StopOnFullyOpened && FlowValve.IsOpened || StopOnFullyClosed && FlowValve.IsClosed))
            {
                pos = FlowValve.Position;
                roc = Meter.RateOfChange.Value;

                var dpos = pos - priorPos;
                var drate = roc - priorRate;
                var newPpr = drate == 0 ? 0 : dpos / drate;
                if (Math.Abs(drate) > 0.5 && Math.Abs(dpos) >= 2 && (ppr > 0) == (newPpr > 0))
                    ppr = DigitalFilter.WeightedUpdate(dpos / drate, ppr, 0.9);

                var anticipatedValue = UseRateOfChange ?
                    roc + Lag * Meter.RateOfChange.RoC :
                    Meter.Value + Lag * roc;
                var error = anticipatedValue - TargetValue;

                // avoid overshoot?
                // var targetRate = error > 0 ? -error : -error / Lag;
                var targetRate = -error / Lag;
                if (Math.Abs(targetRate) > MaximumRate)
                    targetRate = targetRate < 0 ? -MaximumRate : MaximumRate;

                var movement = ppr * (targetRate - roc);

                var movementIsPositive = movement >= 0;
                var movementDirection = (movementIsPositive == FlowValve.OpenIsPositive) ? ValveState.Opening : ValveState.Closing;

                //ProcessStep.CurrentStep.Description = $"{Name}: error={error:0} roc={roc:0.0}/{targetRate:0.0} ppr={ppr:0.0} m={movement:0}";

                var secondsLeft = Math.Max(0, SecondsCycle - actionStopwatch.ElapsedMilliseconds / 1000);
                var waited = (secondsLeft == 0);

                if ((Math.Abs(error) > deadband) && waited) //(waited || movementDirection == ValveState.Closing))
                {
                    int amountToMove = (Math.Min(Math.Abs(MaximumMovement), Math.Max(1, Math.Abs(movement)))).ToInt();
                    if (!movementIsPositive) amountToMove = -amountToMove;

                    operation.Value = amountToMove;
                    FlowValve.DoWait(operation);
                    actionStopwatch.Restart();
                    priorPos = pos;
                    priorRate = roc;
                }

                stopRequested = stopSignal.WaitOne(MillisecondsTimeout);
            }
            FlowValve.ActuatorOperations.Remove(operation);

            //ProcessStep.End();
        }
    }
}