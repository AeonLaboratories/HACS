using HACS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
    public class MassFlowController : Component
    {
		public static new List<MassFlowController> List;
		public static new MassFlowController Find(string name)
		{ return List?.Find(x => x.Name == name); }

		[XmlElement("LabJack")]
		public string LabJackName { get; set; }
		LabJackDaq LabJack;

		public int Channel { get; set; }

		[XmlElement("FlowMeter")]
		public string FlowMeterName { get; set; }
		Meter FlowMeter;

		double _Setpoint;
        public double Setpoint
        {
            get { return _Setpoint; }
            set
            {
                if (value < SetpointMin)
                    _Setpoint = SetpointMin;
                else if (value > SetpointMax)
                    _Setpoint = SetpointMax;
                else
                    _Setpoint = value;
                SetOutput(_Setpoint);
            }
        }
        public double SetpointMin { get; set; }
        public double SetpointMax { get; set; }

        public double FlowRate { get { return FlowMeter; } }

		object flowTrackingLock = new object();
		public double TrackedFlow { get; set; }
		[XmlIgnore] Stopwatch flowTrackingStopwatch = new Stopwatch();
        [XmlIgnore] Thread flowTrackingThread;
        [XmlIgnore] ManualResetEvent flowTrackingSignal = new ManualResetEvent(false);

		public double OutputVoltage { get; set; }
		public OperationSet OutputConverter { get; set; }

        public MassFlowController() { }

		public override void Connect()
		{
			FlowMeter = Meter.Find(FlowMeterName);
			LabJack = LabJackDaq.Find(LabJackName);
		}

        public override void Initialize()
        {
			flowTrackingThread = new Thread(TrackFlow);
			flowTrackingThread.IsBackground = true;
			flowTrackingThread.Start();

			Initialized = true;

			SetOutput(Setpoint);
        }

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

				flowTrackingSignal.Reset();
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

        public void SetOutput(double sccm)
        {
            if (!Initialized) return;

			if (OutputConverter != null)
				OutputVoltage = OutputConverter.Execute(sccm);

            LabJack.SetAO(Channel, OutputVoltage);
        }

		public override string ToString()
		{
			return Name + ": " + "\r\n" +
				Utility.IndentLines(
					OutputVoltage.ToString("DAC: 0.000 V") + "\r\n" +
					FlowMeter.ToString()
				);
		}
    }
}
