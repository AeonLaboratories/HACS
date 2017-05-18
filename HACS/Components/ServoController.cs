using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Utilities;

namespace HACS.Components
{
	public partial class ServoController : Controller
	{
		public static new List<ServoController> List;
		public static new ServoController Find(string name)
		{ return List?.Find(x => x.Name == name); }

		[XmlType(AnonymousType = true)]
		public enum States { Free, Configuring, Confirming, Going, AwaitingMotion, AwaitingFinish, Aborting }
		static long instanceCount = 0;

		public bool LogCommands = false;

		#region variables

		const int maxChannels = 64; // hardware limitation

		Queue<ObjectPair> ServiceQ = new Queue<ObjectPair>();
		Thread sqThread;
		ManualResetEvent sqThreadSignal = new ManualResetEvent(false);
		ManualResetEvent sqOpSignal = new ManualResetEvent(false);

		bool Aborting = false;

		[XmlIgnore] public Actuator[] Actuator = new Actuator[maxChannels];

		[XmlIgnore] public double Voltage { get; set; }

		States _State = States.Free;

		#endregion variables

		#region Properties

		public States State { get { return _State; } }

		#endregion Properties

		new public void Initialize()
		{
			base.Initialize();
			instanceCount++;
			ResponseProcessor = ProcessResponse;

			sqThread = new Thread(serviceActuatorQ);
			sqThread.Name = "SC sqThread " + instanceCount.ToString();
			sqThread.IsBackground = true;
			sqThread.Start();
		}

		public bool Busy()
		{
			bool busy;
			lock (ServiceQ) busy = ServiceQ.Count > 0 || CurrentActuator != null;
			return busy;
		}

		public ServoController() : base() { }

		public ServoController(string name, SerialPortSettings portSettings)
			: base(name, portSettings) { }

		public void Connect(Actuator a)
		{
			Connect(a, a.Channel);
		}

		public void Connect(Actuator a, int ch)
		{
			if (Actuator[ch] != a)
			{
				if (Actuator[ch] == null)
				{
					Actuator[ch] = a;
					a.Connect(this);
				}
				else
				{
					MessageBox.Show("Actuator " + a.Name + " conflicts with " + Actuator[ch].Name + " on channel " + ch.ToString());
				}

			}
		}

		public void EnQforService(Actuator a, ActuatorAction action)
		{
			ObjectPair op = new ObjectPair(a, action);
			lock (ServiceQ) ServiceQ.Enqueue(op);
			sqThreadSignal.Set();
		}

		[XmlIgnore] public Actuator CurrentActuator = null;
		[XmlIgnore] public ActuatorAction CurrentAction = null;
		// runs in sqThread
		void serviceActuatorQ()
		{
			try
			{
				while (true)
				{
					if (CurrentActuator != null)
					{
						do
						{
							bool responseExpected = operate(CurrentActuator);
							if (responseExpected)
							{
								sqOpSignal.Reset();         // block on WaitOne()
								sqOpSignal.WaitOne(200);    // wait for response
							}
						} while (_State != States.Free);
						CurrentActuator = null;
						CurrentAction = null;
					}

					int count;
					lock (ServiceQ) count = ServiceQ.Count;
					if (count > 0)
					{
						ObjectPair op;
						lock (ServiceQ) op = ServiceQ.Dequeue();
						CurrentActuator = op.x as Actuator;
						CurrentAction = op.y as ActuatorAction;
					}
					else
					{
						sqThreadSignal.Reset();         // block on WaitOne()
						sqThreadSignal.WaitOne();       // wait for an actuator
						Aborting = false;
					}
				}
			}
			catch (Exception e)
			{ MessageBox.Show(e.ToString()); }
		}

		bool operate(Actuator a)
		{
			bool responseExpected = false;
			bool done = false;
			if (Aborting) _State = States.Aborting;

			switch (_State)
			{
				case States.Free:
					a.Action = CurrentAction;
					a.Active = true;
					if (a.Action == null)
						done = true;        // this prevents using null action to simply select the actuator
					else
					{
						if (LogCommands) log.Record(CurrentActuator.Name + ": " + CurrentActuator.Command);
						_State = States.Configuring;
					}
					break;
				case States.Configuring:
					foreach (string cmd in a.Command.Split(' '))
						Command(cmd);
					responseExpected = true;
					_State = States.Confirming;
					break;
				case States.Confirming:
					if (!a.Configured)
					{ _State = States.Configuring; }
					else if (a.Action == null)          // this doesn't work, presently; see case States.Free above
					{ done = true; }
					else
					{ _State = States.Going; }
					break;
				case States.Going:
					Command("g r");
					responseExpected = true;
					_State = States.AwaitingMotion;
					break;
				case States.AwaitingMotion:
					if (a.InMotion || a.MotionInhibited)
						_State = States.AwaitingFinish;
					else
						_State = States.Going;
					break;
				case States.AwaitingFinish:
					if (a.Stopped)
						done = true;
					else
					{
						Command("r");
						responseExpected = true;
					}
					break;
				case States.Aborting:
					if (a.Stopped)
						done = true;
					else
					{
						Command("s r");
						responseExpected = true;
					}
					break;
				default:
					break;
			}
			if (done)
			{
				if (CurrentAction != null) Command("n63");      // "park" at last actuator
				a.Active = false;
				_State = States.Free;
				Aborting = false;
			}
			return responseExpected;
		}

		public void Abort()
		{
			if (Aborting) return;
			Aborting = true;
			lock (ServiceQ)
			{
				while (ServiceQ.Count > 0)
				{
					ObjectPair op = ServiceQ.Dequeue();
					(op.x as Actuator).Active = false;
				}
			}
		}

		void ProcessResponse(string s)
		{
			if (s.Length == HACS.Components.Actuator.ReportLength)
			{
				try
				{
					// channel # is in first 3 bytes of the report
					int ch = int.Parse(s.Substring(0, 3));
					if (ch >= 0 && ch <= maxChannels)
					{
						Actuator a = Actuator[ch];
						if (a != null)
						{
							a.State.Report = s.Substring(0, s.Length - 2);  // strip /r/n
							if (a.State.ReportValid)
								Voltage = a.State.ControllerVoltage;
							//if (LogEverything) log.Record(a.ToString());
						}
					}
					else log.Record("Invalid Servo Channel " + ch.ToString());
				}
				catch { log.Record("Bad ServoController response: [" + s + "]"); }
			}
			else
				log.Record("Unrecognized ServoController response: \r\n" + s + ";\r\n Length = " + s.Length.ToString());

			sqOpSignal.Set();
		}
	}
}
