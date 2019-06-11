using HACS.Core;
using System.Collections.Generic;
using System.Threading;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public class MassFlowController : HacsComponent
	{
		#region Component Implementation

		public static readonly new List<MassFlowController> List = new List<MassFlowController>();
		public static new MassFlowController Find(string name) { return List.Find(x => x?.Name == name); }

        protected void Initialize()
        {
            flowTrackingThread = new Thread(TrackFlow)
            {
                Name = $"{Name} TrackFlow",
                IsBackground = true
            };
            flowTrackingThread.Start();
        }

		protected void Start()
        {
            TurnOn(Setpoint);
        }

		public MassFlowController()
		{
			List.Add(this);
			OnInitialize += Initialize;
			OnStart += Start;
		}

		#endregion Component Implementation


		public HacsComponent<AnalogOutput> ControlSignalRef { get; set; }
		AnalogOutput ControlSignal => ControlSignalRef?.Component;
        public double OutputVoltage { get; set; }

        public OperationSet OutputConverter { get; set; }

        double _Setpoint;
		public double Setpoint
		{
			get { return _Setpoint; }
			set
			{
                if (!Initialized)
                {
                    _Setpoint = value;
                    return;
                }
				if (value < SetpointMin)
					_Setpoint = SetpointMin;
				else if (value > SetpointMax)
					_Setpoint = SetpointMax;
				else
					_Setpoint = value;
				TurnOn(_Setpoint);
			}
		}

        public HacsComponent<Meter> FlowMeterRef { get; set; }
        Meter FlowMeter => FlowMeterRef?.Component;

        public double FlowRate => FlowMeter;

        object flowTrackingLock = new object();
		public double TrackedFlow { get; set; }
		[XmlIgnore] Stopwatch flowTrackingStopwatch = new Stopwatch();
		[XmlIgnore] Thread flowTrackingThread;
		[XmlIgnore] AutoResetEvent flowTrackingSignal = new AutoResetEvent(false);

        public double SetpointMin { get; set; }
        public double SetpointMax { get; set; }

		void TrackFlow()
		{
			flowTrackingStopwatch.Restart();
			while (true)
			{
				lock (flowTrackingLock)
				{
					TrackedFlow += FlowRate * flowTrackingStopwatch.ElapsedMilliseconds / 60000;
					flowTrackingStopwatch.Restart();
				}

				flowTrackingSignal.WaitOne(500);
			}
		}

		public void ResetTrackedFlow()
		{
			lock (flowTrackingLock)
			{
				TrackedFlow = 0;
				flowTrackingStopwatch.Restart();
			}
		}


        /// <summary>
        /// Set the flow rate to the given value in standard cubic centimeters per minute.
        /// </summary>
        /// <param name="sccm"></param>
		public void TurnOn(double sccm)
		{
			if (!Initialized) return;

			if (OutputConverter != null)
				OutputVoltage = OutputConverter.Execute(sccm);

            ControlSignal.SetOutput(OutputVoltage);
		}

        public void TurnOff()
        {
            TurnOn(0);
        }

		public override string ToString()
		{

            return $"{Name}:\r\n" +
                Utility.IndentLines(
                    $"{FlowMeter}\r\n" +
                    $"SP: {OutputVoltage:0.000} DAC: {ControlSignal.OutputVoltage:0.000} V"
				);
		}
	}
}
