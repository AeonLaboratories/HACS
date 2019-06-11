using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public class SwitchBank : Controller
	{
		#region Component Implementation

		public static readonly new List<SwitchBank> List = new List<SwitchBank>();
		public static new SwitchBank Find(string name) { return List.Find(x => x?.Name == name); }

		protected override void Initialize()
        {
            if (LogComms) Log.Record("Initializing...");

            ResponseProcessor = ProcessResponse;

            sqThread = new Thread(serviceQ)
            {
                Name = $"{Name} ProcessCommands",
                IsBackground = true
            };
            sqThread.Start();

            stateThread = new Thread(ManageState)
            {
                Name = $"{Name} ManageState",
                IsBackground = true
            };
            stateThread.Start();

            base.Initialize();
        }

		protected void Start()
        {
            ResetAll();
            CheckAll();
        }

		// This method is not a Component override. It is intended to be called by
		// the OnOffDevices during their Connect() phase.
		public void Connect(OnOffDevice s)
		{
			if (s == null) return;
			if (Switches == null) Switches = new OnOffDevice[Channels];

			int ch = s.Channel;
			if (Switches[ch] != null)
				Log.Record($"Replacing {Switches[ch].Name} on channel {ch} with {s.Name}");
			Switches[ch] = s;
		}

		public SwitchBank()
		{
			List.Add(this);
			OnStart += Start;
		}

		#endregion Component Implementation

		[JsonProperty]
		public int Channels { get; set; }   // hardware limit/config
		[XmlIgnore] public OnOffDevice[] Switches;

		public void ResetAll()
		{
			Command("x");
		}

		public void CheckAll()
		{
			foreach(OnOffDevice s in Switches)
			{
				if (s != null && s.IsReallyOn != s.IsOn)
					Command(command(s));
			}
        }

        string onOffString(bool onOff) => onOff ? "1" : "0";
        string command(OnOffDevice s) => $"n{s.Channel} {onOffString(s.IsOn)} r";
        string command(OnOffDevice s, string onOff) => $"n{s.Channel} {onOff} r";


        public void RequestService(OnOffDevice s)
		{
            ObjectPair op = new ObjectPair(s, onOffString(s.IsOn));
            lock (ServiceQ) ServiceQ.Enqueue(op);
            sqThreadSignal.Set();
        }

        Thread stateThread;
		AutoResetEvent stateSignal = new AutoResetEvent(false);

		void ManageState()
		{
			try
			{
				while (true)
				{
					int timeout = 300;				// TODO: move this into settings.xml?
					if (ServiceQ.Count == 0)
						CheckAll();

					if (stateSignal.WaitOne(timeout) && LogComms)
						Log.Record("Signal received");
					else if (LogComms)
						Log.Record(timeout.ToString() + " ms timeout");
				}
			}
			catch (Exception e)
			{
				if (LogComms)
					Log.Record("Exception in ManageState(): " + e.ToString());
				else
					Notice.Send(e.ToString());
			}
        }

		Queue<ObjectPair> ServiceQ = new Queue<ObjectPair>();
		Thread sqThread;
		AutoResetEvent sqThreadSignal = new AutoResetEvent(false);

		AutoResetEvent responseSignal = new AutoResetEvent(false);

		void serviceQ()
		{
            SolenoidValve sv = null;
            bool onOff;

			try
			{
                while (true)
				{
                    if (sv != null)
                    {
                        // wait until the switch is in the requested state
                        while (sv.IsReallyOn != sv.ActiveState)
                            responseSignal.WaitOne(10);

                        // then wait until any motion is complete
                        var timeleft = sv.MillisecondsToChangeState - sv.MillisecondsInState;
                        if (timeleft > 0)
                            Thread.Sleep((int)timeleft);
                        sv.Active = false;
                        sv = null;
                    }

                    int count;
                    lock (ServiceQ) count = ServiceQ.Count;
                    if (count > 0 && !SerialDevice.Disconnected)
                    {
                        ObjectPair op;
                        lock (ServiceQ) op = ServiceQ.Dequeue();
                        var device = op.x as OnOffDevice;
                        var onOffStr = op.y as string;

                        var cmd = command(device, onOffStr);

                        if (LogComms) Log.Record("out: " + cmd);
						if (!Command(cmd))
							Log.LogParsimoniously("Couldn't transmit command: [" + cmd + "]");

                        sv = device as SolenoidValve;
                        if (sv != null)
                        {
                            onOff = onOffStr == "1";
                            sv.ActiveState = onOff;
                            sv.Active = true;
                        }
						responseSignal.WaitOne(50);
					}
					else
						sqThreadSignal.WaitOne(500);
				}
			}
			catch (Exception e)
			{
				if (LogComms)
					Log.Record(Name + ": Exception in ProcessCommands(): " + e.ToString());
				else
					Notice.Send(e.ToString());
			}
		}

		void ProcessResponse(string report)
		{
			if (report.Length == OnOffDevice.ReportLength)
			{
				try
				{
					// channel # is in first 2 bytes of the report
					int ch = int.Parse(report.Substring(0, 2));
					if (ch >= 0 && ch <= Channels)
					{
						OnOffDevice s = Switches[ch];
						if (s != null)
							s.Report = report.Substring(0, report.Length - 2);  // strip /r/n
					}
					else Log.Record("Invalid SwitchBank Channel " + ch.ToString());
				}
				catch { Log.Record("Bad SwitchBank Controller response: [" + report + "]"); }
			}
			else
				Log.Record("Unrecognized SwitchBank response: \r\n" + report + ";\r\n Length = " + report.Length.ToString());

			responseSignal.Set();
			stateSignal.Set();
		}


		public override string ToString()
		{
			if (Switches == null) return "";
			StringBuilder sb = new StringBuilder(Name);
			sb.Append(": ");
			foreach (OnOffDevice s in Switches)
			{
				if (s != null)	// TODO: or should we show all, with state?
				{
					sb.Append("\r\n   ");
					sb.Append(s.ToString().Replace($"{Name}:", ""));
				}
			}
			return sb.ToString();
		}
	}
}
