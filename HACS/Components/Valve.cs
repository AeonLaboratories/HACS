using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utilities;
using System.Xml.Serialization;
using HACS.Core;

namespace HACS.Components
{
	[XmlInclude(typeof(MeteringValve))]
	public class Valve : Actuator
	{
		public static new List<Valve> List;
		public static new Valve Find(string name)
		{ return List?.Find(x => x.Name == name); }

		[XmlType(AnonymousType = true)]
		public enum States { Unknown, Closed, Opened, Closing, Opening };

		// these string constants are names for some valve actions
		[XmlIgnore] public static string OpenValve = "Open";
		[XmlIgnore] public static string CloseValve = "Close";
		[XmlIgnore] public static string OpenPulse = "Open pulse";
		[XmlIgnore] public static string OpenABit = "Open a bit";
		[XmlIgnore] public static string OpenABitSlower = "Open a bit slower";
		[XmlIgnore] public static string CloseABit = "Close a bit";
		[XmlIgnore] public static string OpenOneSecond = "Open one second";

		public States ValveState { get; set; }

		int cpw0 = 1500;
		public override void Initialize()
		{
			try
			{
				cpw0 = (FindAction(OpenValve).CPW + FindAction(CloseValve).CPW) / 2;
			}
			catch { }

			ValveStateChanged();

			base.Initialize();
		}

		States cpwDirection(int cpw, int refcpw)
		{
			if (cpw == refcpw) // they match; return the abolute direction
				refcpw = cpw0;

			if (cpw < refcpw)
				return States.Opening;
			else if (cpw > refcpw)
				return States.Closing;
			else        // direction can't be determined
				return States.Unknown;
		}

		public virtual States ActionDirection(ActuatorAction action)
		{
			if (action == null)
			{
				if (Action == null)
					return States.Unknown;
				else
					return cpwDirection(Action.CPW, cpw0);
			}

			if (Action == null)
				return cpwDirection(action.CPW, cpw0);

			return cpwDirection(action.CPW, Action.CPW);
		}

		public States LastMotion { get { return ActionDirection(null); } }

		public override ActuatorAction ValidateAction(ActuatorAction action)
		{
			var dir = ActionDirection(action);
			if ((dir == States.Closing && isClosed) || (dir == States.Opening && isOpened))
				return null;
			else
				return action;
		}

		public virtual void Close() { DoAction(CloseValve); }
		public virtual void Open() { DoAction(OpenValve); }

		[XmlIgnore]
		public bool isOpened
		{ get { return ValveState == States.Opened; } }

		[XmlIgnore]
		public bool isClosed
		{ get { return ValveState == States.Closed; } }

		public Valve() : base()
		{
			State.Changed = ValveStateChanged;
		}

		// called whenever the valve is "Active" and a report is received,
		// and also once when the valve becomes inactive
		public virtual void ValveStateChanged()
		{
			if (Action != null)
			{
				if (LastMotion == States.Opening)
				{
					ValveState =
						(PositionDetectable ? PositionDetected : ActionSucceeded) ? States.Opened :
							Active ? States.Opening : States.Unknown;
				}
				else    // assume a closing motion
				{
					ValveState =
						(PositionDetectable ? PositionDetected : ActionSucceeded) ? States.Closed :
							Active ? States.Closing : States.Unknown;
				}
			}

			StateChanged?.Invoke();
		}

		public override string ToString()
		{
			string s = string.IsNullOrEmpty(State.Report) ? "" : ReportHeader + State.Report + "\r\n";
			s += State.ToString() + "\r\n";
			s += String.Format("Ch:{0} PendingActions:{1} LastMotion:{2} Succeeded:{3}",
					Channel, PendingActions, LastMotion, ActionSucceeded);

			return Name + " (" + ValveState.ToString() + "):\r\n" +
				Utility.IndentLines(s);
		}

	}

	/// <summary>
	/// interfaces an Aeon 360-degree incremental servo (DSMCi)
	/// </summary>
	public class MeteringValve : Valve
	{
		public double Turns { get; set; }       // device configuration: #turns from CpwMin to CpwMax
		public double Resolution { get; set; }  // detectable positions per turn
		public int CpwMin { get; set; }         // in microseconds
		public int CpwMax { get; set; }         // in microseconds
		public int Cpw0 { get; set; }           // in microseconds, tuned
		public int OpenedPosition { get; set; }

		double cpwPerPosition { get; set; }     // microseconds per detectable position
		int openingPositions { get; set; }      // maximum opening positions that can be commanded in one go
		int closingPositions { get; set; }

		public int Position { get; set; }
		int headroom { get { return OpenedPosition - Position; } }

		double degreesPerPosition { get; set; }

		public override void Initialize()
		{
			cpwPerPosition = (CpwMax - CpwMin) / (Turns * Resolution);
			degreesPerPosition = 360.0 / Resolution;

			closingPositions = Convert.ToInt32(Math.Floor((CpwMax - Cpw0) / cpwPerPosition));
			openingPositions = Convert.ToInt32(-Math.Floor((Cpw0 - CpwMin) / cpwPerPosition));

			ValveStateChanged();
		}

		public override void Close() { MoveToPosition(0); }
		public override void Open() { MoveToPosition(OpenedPosition); }

		/// <summary>
		/// moves the valve the specified number of positions
		/// </summary>
		/// <param name="dpos">The desired positional movement, negative for open, positive for close</param>
		public void Move(int dpos)
		{
			// constrain the movement to what is possible
			if (-dpos > headroom)       // opening
				dpos = -headroom;
			else if (dpos > Position)
				dpos = Position;
			ActuatorAction action = FindAction(dpos < 0 ? OpenValve : CloseValve);

			while (dpos != 0)
			{
				int thisDelta;
				if (dpos < openingPositions)
					thisDelta = openingPositions;
				else if (dpos > closingPositions)
					thisDelta = closingPositions;
				else
					thisDelta = dpos;

				action = action.Clone;
				action.CPW = cpw(thisDelta);
				DoAction(action);
				dpos -= thisDelta;
			}
		}

		// pos values ouside of (0..OpenPosition) are clamped into that range
		public void MoveToPosition(int pos) { Move(Position - pos); }

		int cpw(int dpos)
		{ return Cpw0 + Convert.ToInt32(Math.Round(dpos * cpwPerPosition)); }

		int dPosition(int cpw)
		{ return Convert.ToInt32((Cpw0 - cpw) / cpwPerPosition); }

		bool wasActive = false;
		public override void ValveStateChanged()
		{
			bool openingMotion = (Action != null && Action.CPW < Cpw0);

			if (Active)
			{
				ValveState = openingMotion ? States.Opening : States.Closing;
				wasActive = true;
			}
			else // action just completed
			{
				if (Action != null && wasActive && ActionSucceeded)
				{
					Position = Position + dPosition(Action.CPW);
					if (CurrentLimitDetected || Position < 0)
						Position = 0;
					wasActive = false;
				}

				if (Position == 0)
					ValveState = States.Closed;
				else if (Position < OpenedPosition)
					ValveState = States.Unknown;    // hmm, it is known, though: Position
				else
					ValveState = States.Opened;
			}
			
			StateChanged?.Invoke();
		}

		public override string ToString()
		{
			string s = string.IsNullOrEmpty(State.Report) ? "" : ReportHeader + State.Report + "\r\n";
			s += State.ToString() + "\r\n";
			s += String.Format("Ch:{0} Position:{1} PendingActions:{2} LastMotion:{3} Succeeded:{4}",
					Channel, Position, PendingActions, LastMotion, ActionSucceeded);

			return Name + " (" + ValveState.ToString() + "):\r\n" +
				Utility.IndentLines(s);
		}
	}
}
