using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Utilities;
using Newtonsoft.Json;

namespace HACS.Components
{
    public class ActuatorController : Controller
    {
        #region Component Implementation

        public static readonly new List<ActuatorController> List = new List<ActuatorController>();
        public static new ActuatorController Find(string name) { return List.Find(x => x?.Name == name); }

        protected override void Initialize()
        {
            ResponseProcessor = ProcessControllerResponse;
            if (Actuator != null)
                Actuator.ResponseProcessor = ProcessActuatorResponse;

            sqThread = new Thread(serviceQ)
            {
                Name = $"{Name} serviceQ",
                IsBackground = true
            };
            sqThread.Start();

            base.Initialize();
        }

        // This method is not a Component override. It is intended to be called by
        // the Actuators during their Connect() phase.
        public void Connect(CpwActuator a)
        {
            if (a == null) return;
            if (Actuators == null) Actuators = new CpwActuator[Channels];

            int ch = a.Channel;
            if (Actuators[ch] != null)
                Log.Record($"Replacing {Actuators[ch].Name} on channel {ch} with {a.Name}");
            Actuators[ch] = a;
        }

        public ActuatorController()
        {
            List.Add(this);
        }

        #endregion Component Implementation


        [XmlType(AnonymousType = true)]
        public enum States { Free, Configuring, Confirming, Going, AwaitingMotion, AwaitingFinish, Aborting }

		[JsonProperty]
		public int Channels { get; set; }   // hardware limit

		/// <summary>
		// This SerialDevice Controller is for actuators with direct serial communications
		/// </summary>
		[JsonProperty]
		public Controller Actuator { get; set; }

        #region fields

        Queue<ObjectPair> ServiceQ = new Queue<ObjectPair>();
        Thread sqThread;
        AutoResetEvent sqThreadSignal = new AutoResetEvent(false);

        bool ControllerResponseExpected = false;
        AutoResetEvent CRRSignal = new AutoResetEvent(false);       // Controller ResponseRecieved

        bool ActuatorResponseExpected = false;
        AutoResetEvent ARRSignal = new AutoResetEvent(false);       // Actuator ResponseRecieved

        bool Aborting = false;

        [XmlIgnore] public CpwActuator[] Actuators;

        [XmlIgnore] public double Voltage;

        #endregion fields

        #region Properties

        [XmlIgnore] public States State { get; private set; }

        #endregion Properties

        public bool Busy()
        {
            bool busy = !base.Idle || CurrentActuator != null;
            if (!busy)
                lock (ServiceQ) busy = ServiceQ.Count > 0;
            return busy;
        }

        public override void WaitForIdle()
        {
            while (Busy()) Thread.Sleep(20);
        }

        public void RequestService(CpwActuator a, ActuatorOperation operation)
        {
            ObjectPair op = new ObjectPair(a, operation);
            lock (ServiceQ) ServiceQ.Enqueue(op);
            sqThreadSignal.Set();
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
                    (op.x as CpwActuator).Active = false;
                }
            }
        }

        [XmlIgnore] public CpwActuator CurrentActuator = null;
        [XmlIgnore] public ActuatorOperation CurrentOperation = null;

        // runs in sqThread
        void serviceQ()
        {
            try
            {
                while (true)
                {
                    if (CurrentActuator != null)
                    {
                        do
                        {
                            operate(CurrentActuator);

                            bool timedOut = false;
                            if (ControllerResponseExpected && ActuatorResponseExpected)
                                timedOut = !WaitHandle.WaitAll(new WaitHandle[] { CRRSignal, ARRSignal }, 200);
                            else if (ControllerResponseExpected)
                                timedOut = !CRRSignal.WaitOne(200);
                            else if (ActuatorResponseExpected)
                                timedOut = !ARRSignal.WaitOne(200);
                            if (LogEverything && timedOut)
                                Log.Record($"serviceQ: Actuator response timeout");

                        } while (State != States.Free);
                        CurrentActuator = null;
                        CurrentOperation = null;
                    }

                    int count;
                    lock (ServiceQ) count = ServiceQ.Count;
                    if (count > 0 && !SerialDevice.Disconnected)
                    {
                        ObjectPair op;
                        lock (ServiceQ) op = ServiceQ.Dequeue();
                        CurrentActuator = op.x as CpwActuator;
                        CurrentOperation = op.y as ActuatorOperation;
                    }
                    else
                    {
                        Aborting = false;
                        sqThreadSignal.WaitOne();   // wait for an actuator to request service
                    }
                }
            }
            catch (Exception e) { Notice.Send(e.ToString()); }
        }

        void operate(CpwActuator a)
		{
            var v = a as RS232Valve;

			bool done = false;
			if (Aborting) State = States.Aborting;

			ControllerResponseExpected = false;
			ActuatorResponseExpected = false;

			switch (State)
			{
				case States.Free:
                    initiateOperation(a);
                    State = States.Configuring;
					break;
				case States.Configuring:
                    configureController(a);
					ControllerResponseExpected = true;
					State = States.Confirming;
					break;
				case States.Confirming:
					if (!a.Configured)
					{ State = States.Configuring; }
					else if (a.Operation == null)
					{ done = true; }
					else
					{
						if (v == null || v.WaitingToGo)
                            State = States.Going;
						else
                            configureActuator(v);
					}
					break;
				case States.Going:
                    sendGoCommand(a);
					State = States.AwaitingMotion;
					break;
				case States.AwaitingMotion:
                    if (a.InMotion || a.MotionInhibited)
                        State = States.AwaitingFinish;
                    else
                        checkForMotion(a);
					break;
				case States.AwaitingFinish:
                    if (a.Stopped)
                        done = true;
                    else
                        checkForStopped(a);
					break;
				case States.Aborting:
                    if (a.Stopped)
                        done = true;
                    else
                        stopOperation(a);
					break;
				default:
					break;
			}
			if (done)
			{
				a.Active = false;
				Aborting = false;
                State = States.Free;
            }
        }

        void initiateOperation(CpwActuator a)
        {
            if (LogCommands)
                Log.Record($"{a.Name} ({a.Channel}): {a.Operation.Configuration}");
            // Temporary: change channel, to zero Actuator controller-reported conditions
            // Modify firmware: add command to do the zeroing.
            Command($"n{(a.Channel == 63 ? 62 : 63)} n{a.Channel}");
            SerialDevice.WaitForIdle();
            a.Operation = CurrentOperation;
            a.Active = true;
        }

        void configureController(CpwActuator a)
        {
            if (!(a is RS232Valve))
                Command($"p{a.Operation.Value}");
            foreach (string cmd in a.Operation.Configuration.Split(' '))
                Command(cmd);

            SerialDevice.WaitForIdle();
            Thread.Sleep(40);

            Command("r");
            SerialDevice.WaitForIdle();
        }

        void configureActuator(RS232Valve v)
        {
            // Issue a 'stop/zero' command to ensure the next position command can be detected
            Actuator?.Command("0");
            if (LogCommands) Log.Record($"{v.Name} Actuator: 0");

            Thread.Sleep(10);

            Actuator?.Command("r");
            ActuatorResponseExpected = true;
        }

        void sendGoCommand(CpwActuator a)
        {
            Command("g");
            if (LogCommands) Log.Record($"{a.Name}: g");

            if (a is RS232Valve v)
            {
                SendActuatorGoCommand(v);
                Actuator?.WaitForIdle();
            }

            SerialDevice.WaitForIdle(); // don't do this wait before SendActuatorGoCommand

            //Thread.Sleep(10);       // after this, reports should indicate the commands were received, but the action should still be incomplete
        }

        int PriorMovement = 0;
        int CommandedMovement = 0;
        void SendActuatorGoCommand(RS232Valve v)
        {
            int tgtpos = v.Operation.Value + (v.Operation.Incremental ? v.Position : 0);
            if (tgtpos > v.PositionMax) tgtpos = v.PositionMax;
            if (tgtpos < v.PositionMin) tgtpos = v.PositionMin;

            PriorMovement = 0;
            CommandedMovement = tgtpos - v.Position;
            if (CommandedMovement != 0)
                Actuator?.Command($"g{CommandedMovement}");
            if (LogCommands) Log.Record($"{v.Name} Actuator: g{CommandedMovement}");
        }

        void checkForMotion(CpwActuator a)
        {
            Command("g r");
            if (LogCommands) Log.Record($"{a.Name}: g r");
            SerialDevice.WaitForIdle();
            ControllerResponseExpected = true;

            if (a is RS232Valve)
            {
                Actuator?.Command("r");
                ActuatorResponseExpected = true;
            }
        }

        void checkForStopped(CpwActuator a)
        {
            var v = a as RS232Valve;
            var controllerStopped = v?.ControllerStopped ?? a.Stopped;

            if (a.Stopping || controllerStopped || (v != null && v.ActuatorStopped))
                stopOperation(a);
            else
            {
                Command("r");
                SerialDevice.WaitForIdle();
                ControllerResponseExpected = true;

                if (v != null)
                {
                    Actuator?.Command("r");
                    ActuatorResponseExpected = true;
                }
            }
        }

        void stopOperation(CpwActuator a)
        {
            var v = a as RS232Valve;
            var controllerStopped = v?.ControllerStopped ?? a.Stopped;

            if (!controllerStopped)
            {
                Command("s r");             // stop the Controller
                if (LogCommands) Log.Record($"{a.Name}: s r");
                SerialDevice.WaitForIdle();
                ControllerResponseExpected = true;
            }

            if (v != null && !v.ActuatorStopped)
            {
                Actuator?.Command("s r");      // stop the Actuator
                if (LogCommands) Log.Record($"{v.Name} Actuator: s r");
                ActuatorResponseExpected = true;
            }
        }


        public override string ToString()
        {
            return $"{Name}: {State}";
        }

        void ProcessControllerResponse(string s)
		{
			if (s.Length == CpwActuatorState.ReportLength)
			{
				try
				{
					// channel # is in first 3 bytes of the report
					int ch = int.Parse(s.Substring(0, 3));
					if (ch >= 0 && ch <= Channels)
					{
                        CpwActuator a = Actuators[ch];
						if (a != null)
						{
							a.CpwActuatorState.Report = s.Substring(0, s.Length - 2);  // strip /r/n
							if (LogResponses) Log.Record($"{a.Name}: {a.CpwActuatorState.Report}");
							if (a.CpwActuatorState.ReportValid)
								Voltage = a.CpwActuatorState.ControllerVoltage;
						}
					}
					else Log.Record("Invalid ActuatorController Channel " + ch.ToString());
				}
				catch { Log.Record("Bad ActuatorController response: [" + s + "]"); }
			}
			else
				Log.Record("Unrecognized ActuatorController response: \r\n" + s + ";\r\n Length = " + s.Length.ToString());

			CRRSignal.Set();
		}

		void ProcessActuatorResponse(string s)
		{
			if (s.Length == RS232ActuatorState.ReportLength)
			{
				if (CurrentActuator is RS232Valve v)
				{
					v.RS232ActuatorState.Report = s.Substring(0, s.Length - 2);  // strip /r/n
					if (LogResponses) Log.Record($"{v.Name}: {v.RS232ActuatorState.Report}");
					if (v.RS232ActuatorState.ReportValid && v.RS232ActuatorState.CommandedMovement == CommandedMovement)
						v.Position += (v.RS232ActuatorState.Movement - PriorMovement);
					PriorMovement = v.RS232ActuatorState.Movement;
				}
			}
			else
				Log.Record("Unrecognized Actuator response: \r\n" + s + ";\r\n Length = " + s.Length.ToString());

			ARRSignal.Set();
		}
	}
}
