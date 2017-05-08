using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Utilities;
using System.Xml.Serialization;

namespace HACS.Components
{    
    public class ThermalController : Controller
    {
		public static new List<ThermalController> List;
		public static new ThermalController Find(string name)
		{ return List?.Find(x => x.Name == name); }

		public static int HtrChannels = 6;
        public static int TcChannels = 16;
        static int totalChannels = HtrChannels + TcChannels;

		[XmlType(AnonymousType = true)]
		public enum States { Unknown, Initialized, Stopping, Stopped }
		[XmlIgnore] public States State = States.Unknown;
		[XmlIgnore] Thread stateThread;
		[XmlIgnore] ManualResetEvent stateSignal = new ManualResetEvent(false);
		public override bool Initialized { get { return State >= States.Initialized; } }

		public int SleepMilliseconds { get; set; }

        [XmlIgnore] public Heater[] Heater = new Heater[HtrChannels];
		[XmlIgnore] public TempSensor[] TC = new TempSensor[TcChannels];

		[XmlIgnore] public double CJ0Temperature { get; private set; }
		[XmlIgnore] public double CJ1Temperature { get; private set; }
		[XmlIgnore] public object AutomationControls { get; private set; }

		public ThermalController() : base() { }

		public ThermalController(string name, SerialPortSettings portSettings)
			: base(name, portSettings) { }

		public void Connect(Heater h)
		{
			Connect(h, h.Channel);
		}

		public void Connect(Heater h, int ch)
		{
			if (Heater[ch] != h)
			{
				Heater[ch] = h;
				h.Connect(this);
			}
		}

		public void Connect(TempSensor t)
		{
			Connect(t, t.TCChannel);
		}

		public void Connect(TempSensor t, int ch)
		{
			if (TC[ch] != t)
			{
				TC[ch] = t;
				t.Connect(this);
			}
		}

		public override void Initialize()
		{
			base.Initialize();
			ResponseProcessor = ProcessResponse;

			stateThread = new Thread(manageState);
			stateThread.Name = Name + " stateThread";
			stateThread.IsBackground = true;
			stateThread.Start();

			State = States.Initialized;
			if (LogEverything) log.Record("Initialized.");
		}

		public bool CheckConnectedHeater(int hch)
        {
            if (Heater[hch] == null) return false;
			Command(String.Format("n{0:0} r", hch));
			return true;
        }

        public bool CheckConnectedTC(int tch)
        {
            if (TC[tch] == null) return false;
			Command(String.Format("tn{0:0} tr", tch));
			return true;
        }

		public void Stop()
		{
			if (LogEverything) log.Record("Stopping...");
			State = States.Stopping;
			stateSignal.Set();
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
						if (LogEverything) log.Record("Stopped.");
						break;
					}
				}
				if (LogEverything) log.Record("Ending State Thread");
				Close();
			}
			catch (Exception e) { MessageBox.Show(e.ToString()); }
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
                if (s.Length == HACS.Components.TempSensor.ReportLength)
                {
                    // channel # is in first 2 bytes of the report
                    int tch = int.Parse(s.Substring(0, 2));
                    if (tch >= 0 && tch < TC.Length)
                    {
                        TempSensor t = TC[tch];
                        if (t != null)
                        {
                            t.Report = s.Substring(0, s.Length - 2);  // strip /r/n
                            if (tch < 8) CJ0Temperature = t.MuxTemperature;
							else CJ1Temperature = t.MuxTemperature;
							if (LogEverything) log.Record(t.ToString());
						}
                        else log.Record("No Temperature Sensor connected to Channel " + tch.ToString());
                    }
                    else log.Record("Invalid Temperature Sensor Channel " + tch.ToString());
                }
                else if (s.Length == HACS.Components.Heater.ReportLength)
                {
                    // channel # is first byte of the report
                    int hch = int.Parse(s.Substring(0, 1));
                    if (hch >= 0 && hch < Heater.Length)
                    {
                        Heater h = Heater[hch];
                        if (h != null)
                        {
                            h.Report = s.Substring(0, s.Length - 2);  // strip /r/n
                            if (h.TCChannel < 8) CJ0Temperature = h.MuxTemperature;
                            else CJ1Temperature = h.MuxTemperature;
							if (LogEverything) log.Record(h.ToString());
						}
                        else log.Record("No Heater connected to Channel " + hch.ToString());
                    }
                    else log.Record("Invalid Heater Channel " + hch.ToString());
                }
                else
                    log.Record("Unrecognized ThermalController response: \r\n" + s + ";\r\n Length = " + s.Length.ToString());
            }
            catch { log.Record("Bad ThermalController response: [" + s + "]"); }
        }
    }
}
