using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Utilities;

namespace HACS.Components
{
    public class FlowManager : HacsComponent
    {
        #region Component Implementation

        public static readonly new List<FlowManager> List = new List<FlowManager>();
        public static new FlowManager Find(string name) { return List.Find(x => x?.Name == name); }

        public FlowManager()
        {
            List.Add(this);
        }

		#endregion Component Implementation

		[JsonProperty]
		public HacsComponent<RS232Valve> FlowValveRef { get; set; }
        public RS232Valve FlowValve => FlowValveRef?.Component;

		[JsonProperty]
		public HacsComponent<DynamicQuantity> ValueRef { get; set; }
        public DynamicQuantity Value => ValueRef?.Component;

		[JsonProperty]//, DefaultValue(35)]
		public int MillisecondsTimeout { get; set; } = 35;              // loop timeout (milliseconds)

		// It may be possible to calculate some of these values dynamically,
		// based on the absolute change in Value and/or its RoC, with respect
		// to changes in FlowValve Position. See GasSupply.FlowPressurize for
		// ideas.

		[JsonProperty]//, DefaultValue(0.75)]
		public double SecondsCycle { get; set; } = 0.75;                // nominal time between valve movements
		[JsonProperty]//, DefaultValue(0)]
		public int StartingMovement { get; set; } = -0;
		[JsonProperty]//, DefaultValue(96)]
		public int MaxMovement { get; set; } = 96;
		[JsonProperty]//, DefaultValue(60)]
		public int Lag { get; set; } = 60;                              // dead time + lag (seconds)

		[JsonProperty]//, DefaultValue(0.02)]
		public double Deadband { get; set; } = 0.02;
		[JsonProperty]//, DefaultValue(true)]
		public bool DeadbandIsFractionOfTarget { get; set; } = true;    // otherwise deadband is fixed constant

		[JsonProperty]//, DefaultValue(1)]
		public double Gain { get; set; } = 1;                          // negative if opening motion corrects overshoot
		[JsonProperty]//, DefaultValue(true)]
		public bool DivideGainByDeadband { get; set; } = true;

		[JsonProperty]//, DefaultValue(false)]
		public bool StopOnFullyOpened { get; set; } = false;
		[JsonProperty]//, DefaultValue(false)]
		public bool StopOnFullyClosed { get; set; } = false;

		[JsonProperty]//, DefaultValue(0)]
		public double TargetValue { get; set; } = 0;
		[JsonProperty]//, DefaultValue(false)]
		public bool UseRoC { get; set; } = false;

        Thread managerThread;
        AutoResetEvent stopSignal = new AutoResetEvent(false);

        public bool Busy => managerThread != null && managerThread.IsAlive;

        public void Start() => Start(TargetValue);

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

        public void Stop() => stopSignal.Set();

        void manageFlow()
        {
            bool stopRequested = false;

            ActuatorOperation operation = new ActuatorOperation()
            {
                Name = "Move",
                Value = StartingMovement,
                Incremental = true,
                Configuration = FlowValve.FindOperation("Close").Configuration
            };

            Stopwatch actionStopwatch = new Stopwatch();

            // starting motion
            if (StartingMovement != 0)
            {
                FlowValve.DoOperation(operation);
                FlowValve.WaitForIdle();
            }
            actionStopwatch.Restart();

            var deadband = DeadbandIsFractionOfTarget ? Deadband * TargetValue : Deadband;
            var gain = DivideGainByDeadband ? Gain / deadband : Gain;
            if (FlowValve.OpenIsPositive) gain = -gain;     // usually, positive movement means closing

            while (!(stopRequested || StopOnFullyOpened && FlowValve.IsOpened || StopOnFullyClosed && FlowValve.IsClosed))
            {
                var secondsLeft = Math.Max(0, SecondsCycle - actionStopwatch.ElapsedMilliseconds / 1000);
                var waited = (secondsLeft == 0);
                var anticipatedValue = UseRoC ?
                    Value.RoC + Lag * Value.RoC.RoC:
                    Value + Lag * Value.RoC;
                var error = anticipatedValue - TargetValue;
                var Movement = gain * error;
                var MovementIsPositive = Movement >= 0;
                var MovementDirection = (MovementIsPositive == FlowValve.OpenIsPositive) ? ValveStates.Opening : ValveStates.Closing;

                if ((Math.Abs(error) > deadband) && (waited || MovementDirection != FlowValve.LastMotion))
                {
                    int amountToMove = (int)Math.Round(Math.Min(Math.Abs(MaxMovement), Math.Max(1, Math.Abs(Movement))));
                    if (!MovementIsPositive) amountToMove = -amountToMove;

                    operation.Value = amountToMove;
                    FlowValve.DoOperation(operation);
                    FlowValve.WaitForIdle();
                    actionStopwatch.Restart();
                }

                stopRequested = stopSignal.WaitOne(MillisecondsTimeout);
            }
        }
    }
}