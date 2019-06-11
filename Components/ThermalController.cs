using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public class ThermalController : Controller
	{
		#region Component Implementation

		public static readonly new List<ThermalController> List = new List<ThermalController>();
		public static new ThermalController Find(string name) { return List.Find(x => x?.Name == name); }

		protected override void Initialize()
        {
            ResponseProcessor = ProcessResponse;

            stateThread = new Thread(manageState)
            {
                Name = $"{Name} manageState",
                IsBackground = true
            };
            stateThread.Start();

            State = States.Initialized;
            if (LogEverything) Log.Record("Initialized.");

            base.Initialize();
        }

		protected override void Stop()
        {
            if (LogEverything) Log.Record("Stopping...");
            State = States.Stopping;
            stateSignal.Set();
            while (stateThread != null && stateThread.IsAlive)
                Thread.Sleep(1);
            base.Stop();
        }

		// These two functions are not Component overrides. They are intended to be called by
		// the Heaters and TempSensors during their Connect() phase.
		public void Connect(Heater h)
		{
			if (h == null) return;
			int ch = h.Channel;
			if (Heaters[ch] != null)
				Log.Record($"Replacing {Heaters[ch].Name} on channel {ch} with {h.Name}");
			Heaters[ch] = h;
		}

		public void Connect(TempSensor t)
		{
			if (t == null) return;
			int ch = t.TCChannel;
			if (TCs[ch] != null)
				Log.Record($"Replacing {TCs[ch].Name} on channel {ch} with {t.Name}");
			TCs[ch] = t;
		}

		public ThermalController()
		{
			List.Add(this);
		}

		#endregion Component Implementation


		public static int HtrChannels = 6;
		public static int TcChannels = 16;
		static int totalChannels = HtrChannels + TcChannels;

		[XmlType(AnonymousType = true)]
		public enum States { Unknown, Initialized, Stopping, Stopped }
		[XmlIgnore] public States State = States.Unknown;
		[XmlIgnore] Thread stateThread;
		[XmlIgnore] AutoResetEvent stateSignal = new AutoResetEvent(false);
		public override bool Initialized => State >= States.Initialized;

		[XmlIgnore] public Heater[] Heaters = new Heater[HtrChannels];
		[XmlIgnore] public TempSensor[] TCs = new TempSensor[TcChannels];

		[XmlIgnore] public double CJ0Temperature { get; private set; }
		[XmlIgnore] public double CJ1Temperature { get; private set; }
		[XmlIgnore] public object AutomationControls { get; private set; }

		[JsonProperty]
        public int SleepMilliseconds { get; set; }

		public bool CheckConnectedHeater(int hch)
		{
			if (Heaters[hch] == null) return false;
			Command(String.Format("n{0:0} r", hch));
			return true;
		}

		public bool CheckConnectedTC(int tch)
		{
			if (TCs[tch] == null) return false;
			Command($"tn{tch} tr");
			return true;
		}

		void manageState()
		{
			try
			{
				while (true)
				{
					stateSignal.WaitOne(SleepMilliseconds);
					if (State == States.Initialized && Idle)
						checkaChannel();
					else if (State == States.Stopping)
					{
						State = States.Stopped;
						if (LogEverything) Log.Record("Stopped.");
						break;
					}
				}
				if (LogEverything) Log.Record("Ending State Thread");
            }
            catch (Exception e) { Notice.Send(e.ToString()); }
		}

		int checkCh = 0;
		public void checkaChannel()
		{
			int channelsChecked = 0;
			bool nothingChecked = true;
			while (nothingChecked && channelsChecked < totalChannels)
			{
				if (checkCh < HtrChannels)
				{
					if (CheckConnectedHeater(checkCh))
						nothingChecked = false;
				}
				else
				{
					if (CheckConnectedTC(checkCh - HtrChannels))
						nothingChecked = false;
				}
				channelsChecked++;
				if (++checkCh >= totalChannels) checkCh = 0;
			}
		}		

        public override string ToString() { return $"{Name}: {State}"; }

		// This is the base class's ResponseProcessor delegate.
		// It is executed in the base Controller's 
		// ResponseRecievedHandler thread, which ultimately is
		// the Controller's SerialDevice thread, prxThread.
		// Determines the Response's channel/device, stores
		// the Response in the device's state structure.
		void ProcessResponse(string s)
		{
			try
			{
				if (s.Length == TempSensor.ReportLength)
				{
					// channel # is in first 2 bytes of the report
					int tch = int.Parse(s.Substring(0, 2));
					if (tch >= 0 && tch < TCs.Length)
					{
						TempSensor t = TCs[tch];
						if (t != null)
						{
							t.Report = s.Substring(0, s.Length - 2);  // strip /r/n
							if (tch < 8) CJ0Temperature = t.MuxTemperature;
							else CJ1Temperature = t.MuxTemperature;
							if (LogEverything) Log.Record(t.ToString());
						}
						else Log.Record("No Temperature Sensor connected to Channel " + tch.ToString());
					}
					else Log.Record("Invalid Temperature Sensor Channel " + tch.ToString());
				}
				else if (s.Length == Heater.ReportLength)
				{
					// channel # is first byte of the report
					int hch = int.Parse(s.Substring(0, 1));
					if (hch >= 0 && hch < Heaters.Length)
					{
						Heater h = Heaters[hch];
						if (h != null)
						{
							h.Report = s.Substring(0, s.Length - 2);  // strip /r/n
							if (h.TCChannel < 8) CJ0Temperature = h.MuxTemperature;
							else CJ1Temperature = h.MuxTemperature;
							if (LogEverything) Log.Record(h.ToString());
						}
						else Log.Record("No Heater connected to Channel " + hch.ToString());
					}
					else Log.Record("Invalid Heater Channel " + hch.ToString());
				}
				else
					Log.Record("Unrecognized ThermalController response: \r\n" + s + ";\r\n Length = " + s.Length.ToString());
			}
			catch { Log.Record("Bad ThermalController response: [" + s + "]"); }
		}
	}
}
