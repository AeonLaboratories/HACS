using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Threading;
using Utilities;

namespace HACS.Components
{
	public class SerialController : StateManager, ISerialController
	{
		public static Command DefaultCommand = new Command();

		#region HacsComponent

		[HacsStart]
		protected virtual void PostStart()
		{
			if (LogEverything) Log.Record($"SerialController {Name}: Starting...");
			responseThread = new Thread(ProcessResponses) { Name = $"{Name} ProcessResponses", IsBackground = true };
			responseThread.Start();
			if (LogEverything) Log.Record($"...SerialController {Name}: Started.");
		}

		[HacsPostStop]
		protected virtual void PostStop()
		{
			if (LogEverything) Log.Record($"SerialController {Name}: Stopping...");
			responseSignal.Set();
			SerialDevice?.Close();
			if (LogEverything) Log.Record($"...SerialController {Name}: Stopped.");
			log?.Close();
			StateSignal.Set();
		}

		#endregion HacsComponent

		#region Class interface properties and methods

		#region Settings

		/// <summary>
		/// The SerialDevice that transmits and receives 
		/// messages for this controller.
		/// </summary>
		[JsonProperty]
		public SerialDevice SerialDevice
		{
			get => serialDevice;
			set
			{
				if (serialDevice == value) return;
				if (serialDevice != null)
				{
					serialDevice.ResponseReceivedHandler -= Receive;
					serialDevice.Log = null;
				}
				serialDevice = value;
				if (serialDevice != null)
				{
					UpdateSerialDeviceLog();
					serialDevice.ResponseReceivedHandler -= Receive;
					serialDevice.ResponseReceivedHandler += Receive;
					serialDevice.Connect();
				}
				NotifyPropertyChanged();
			}
		}
		SerialDevice serialDevice;


		void UpdateSerialDeviceLog()
		{
			if (SerialDevice == null) return;
			SerialDevice.Log = LogEverything ? Log : null;
		}

		/// <summary>
		/// 
		/// </summary>
		[JsonProperty, DefaultValue(false)]
		public bool LogCommands
		{
			get => logCommands;
			set => Ensure(ref logCommands, value);
		}
		bool logCommands;

		/// <summary>
		/// 
		/// </summary>
		[JsonProperty, DefaultValue(false)]
		public bool LogResponses
		{
			get => logResponses;
			set => Ensure(ref logResponses, value);
		}
		bool logResponses;

		public override bool LogEverything
		{
			get => base.LogEverything;
			set
			{
				base.LogEverything = value;
				UpdateSerialDeviceLog();
			}
		}

		public override LogFile Log
		{
			get => base.Log;
			set
			{
				base.Log = value;
				UpdateSerialDeviceLog();
			}
		}

		/// <summary>
		/// Split the ServiceCommand into space-separated 
		/// tokens and transmit them in sequence.
		/// </summary>
		[JsonProperty, DefaultValue(false)]
		public bool TokenizeCommands
		{
			get => tokenizeCommands;
			set => Ensure(ref tokenizeCommands, value);
		}
		bool tokenizeCommands = false;

		/// <summary>
		/// Ignore incoming messages (&quot;Responses&quot;) from the 
		/// hardware unless they are expected.
		/// </summary>
		[JsonProperty]
		public bool IgnoreUnexpectedResponses
		{
			get => ignoreUnexpectedResponses;
			set => Ensure(ref ignoreUnexpectedResponses, value);
		}
		bool ignoreUnexpectedResponses;

		/// <summary>
		/// 
		/// </summary>
		[JsonProperty, DefaultValue(200)]
		public int ResponseTimeout
		{
			get => responseTimeout;
			set => Ensure(ref responseTimeout, value);
		}
		int responseTimeout = 200;

		#endregion Settings

		/// <summary>
		/// This structure is used to convey commands to the Controller.
		/// When it is ready for a new command, the controller checks
		/// invokes the SelectService method to get one.
		/// </summary>
		public struct Command
		{
			public string Message;
			public int ResponsesExpected;
			public bool Hurry;
			public Command(string message = "", int responsesExpected = 0, bool hurry = false)
			{
				Message = message;
				ResponsesExpected = responsesExpected;
				Hurry = hurry;
			}
		}

		/// <summary>
		/// This method provides a command to the controller.
		/// </summary>
		/// <returns></returns>
		public Func<Command> SelectServiceHandler { get; set; }

		/// <summary>
		/// The assigned method receives a response message for
		/// processing. If the method returns true (for example, 
		/// if the response is valid), the SerialController will
		/// attempt to retrieve a new Command to process.
		/// If false is returned, the controller will retry the 
		/// current Command.
		/// </summary>
		public Func<string, int, bool> ResponseProcessor { get; set; }

		/// <summary>
		/// This event is raised if the controller's 
		/// SerialDevice disconnects.
		/// </summary>
		public event EventHandler LostConnection;


		/// <summary>
		/// This counter is incremented whenever a response fails to arrive
		/// before the ResponseTimeout period. It is reset to zero on 
		/// receipt of any response, so it functions as a check of
		/// "too many consecutive response timeouts."
		/// </summary>
		public bool Responsive => Ready && ResponseTimeouts < TooManyResponseTimeouts;
		public int TooManyResponseTimeouts { get; set; } = 3;       // TODO: magic number

		/// <summary>
		/// The device is not Ready or there is nothing to do
		/// (i.e., !Busy).
		/// </summary>
		public virtual bool Idle => !Busy;

		/// <summary>
		/// The device is Ready but doing nothing.
		/// </summary>
		public virtual bool Free => Ready && SerialDevice.Free;

		/// <summary>
		/// 
		/// </summary>
		public uint CommandCount { get; private set; } = 0;

		/// <summary>
		/// 
		/// </summary>
		public uint ResponseCount { get; private set; } = 0;


		/// <summary>
		/// Wait until the Controller is Idle.
		/// </summary>
		public virtual bool WaitForIdle(int timeout = -1) =>
			Utility.WaitForCondition(() => Idle, timeout);


		public override string ToString()
		{
			var sb = new StringBuilder($"{Name}");
			if (!SerialDevice.Ready)
				sb.Append($" (Disconnected)");
			else if (Busy)
				sb.Append($" (Busy)");
			else if (Free)
				sb.Append($" (Free)");
			else            // simply not Busy
				sb.Append($" (Idle)");
			return sb.ToString();
		}

		#endregion Class interface properties and methods

		public string Escape(string s) => SerialDevice?.Escape(s).TrimEnd();

		#region State management

		public override bool Ready =>
			base.Ready &&
			(SerialDevice?.Ready ?? false);

		public override bool HasWork =>
			base.HasWork ||
			Hurry == true ||
			!ServiceCommand.IsBlank() ||
			AwaitingResponses > 0 ||
			(SerialDevice?.Busy ?? false);

		/// <summary>
		/// The device is Responsive and doing work.
		/// </summary>
		public override bool Busy => base.Busy || Responsive && HasWork;

		protected virtual void ManageState() =>
			SendCommandMessages();

		/// <summary>
		/// This value starts with the controller's ResponseTimeout, if any
		/// responses are expected. Otherwise, it starts at 0 if Hurry is true, 
		/// or IdleTimeout if not. Then, if multiple commands are contained in the 
		/// ServiceCommand, inter-message pacing delays are added. 
		/// Override this  property to customize the timeout, e.g., based 
		/// on specific commands.
		/// </summary>
		protected override int StateLoopTimeout
		{
			get
			{
				int timeout;
				if (SerialDevice == null || AwaitingResponses < 1)
				{
					timeout = !Hurry ? IdleTimeout : Math.Max(1, SerialDevice?.MillisecondsBetweenMessages ?? 1);
				}
				else if (SerialDevice.ResponseCount < 1)    // give some extra time at start-up 
				{
					// TODO: magic number "FirstTimeout"? "StartupTimeout"? "Wake..."?
					timeout = 2000;
				}
				else
				{
					timeout = ResponseTimeout;
					int n = (CommandMessages?.Length ?? 0) - 1;
					if (n > 0 && SerialDevice.MillisecondsBetweenMessages > 0)
						timeout += n * SerialDevice.MillisecondsBetweenMessages;
				}
				return timeout;
			}
		}

		/// <summary>
		/// The number of expected responses remaining since the last issued command
		/// </summary>
		protected int AwaitingResponses { get; private set; } = 0;

		/// <summary>
		/// Hurry tells the controller to invoke the SelectService
		/// method for new command as soon as the expected 
		/// responses for the current one have been received and 
		/// validated. If Hurry is false, the controller will
		/// invoke SelectService after the IdleTimeout period
		/// elapses.
		/// </summary>
		public bool Hurry
		{
			get { return hurry; }
			set
			{
				if (hurry == value) return;
				hurry = value;
				if (value && AwaitingResponses == 0)
					StateSignal.Set();      // release the StateLoop Wait() if no response is presently expected
			}
		}
		bool hurry = false;

		#region Commands

		/// <summary>
		/// The Command message from the selected service.
		/// </summary>
		public string ServiceCommand { get; private set; }  = "";

		/// <summary>
		/// The number of device messages expected in response
		/// to the ServiceCommand.
		/// </summary>
		protected int ResponsesExpected = 0;

		/// <summary>
		/// // The ServiceCommand, broken into space-delimited fragments.
		/// </summary>
		protected string[] CommandMessages;

		/// <summary>
		/// The current ServiceCommand fragment.
		/// </summary>
		public string CommandMessage { get; private set; } = "";
		public int ResponseTimeouts { get; private set; } = 0;
		object responseTimeoutsLocker = new object();

		private int priorAwaitingResponses = -1;

		/// <summary>
		/// 
		/// </summary>
		protected virtual void SendCommandMessages()
		{
			if (priorAwaitingResponses > 0 && AwaitingResponses >= priorAwaitingResponses && !StateSignalReceived)
				lock (responseTimeoutsLocker) ResponseTimeouts++;

			// command doesn't change until a valid response to 
			// the prior one is received
			if (AwaitingResponses == 0)
			{
				var command = SelectServiceHandler == null ? DefaultCommand : SelectServiceHandler();
				ServiceCommand = command.Message;
				ResponsesExpected = command.ResponsesExpected;
				Hurry = command.Hurry;
				if (LogEverything) 
					Log.Record($"SerialController {Name}: ServiceCommand = \"{Escape(ServiceCommand)}\", ResponsesExpected = {ResponsesExpected}, Hurry = {Hurry}");
			}

			if (ServiceCommand.IsBlank())
			{
				AwaitingResponses = 0;
			}
			else
			{
				// either 
				//  this is the first command, or
				//  the timeout occurred, or 
				//  an invalid response was received (so, awaitingReponses > 0), or
				//  a valid response was received (so, awaitingResponses == 0) and 
				//      the new command is nonempty
				AwaitingResponses = ResponsesExpected;
				if (AwaitingResponses > 0)
					StateSignal.Reset();

				if (TokenizeCommands)
				{
					CommandMessages = ServiceCommand.Split(null as char[], StringSplitOptions.RemoveEmptyEntries);

					foreach (var c in CommandMessages)
						Send(CommandMessage = c);
				}
				else
					Send(CommandMessage = ServiceCommand);

				priorAwaitingResponses = AwaitingResponses;
			}
		}

		#endregion Commands

		#region Responses

		/// <summary>
		/// 
		/// </summary>
		public string Response
		{
			get { return response; }
			private set
			{
				if (IgnoreUnexpectedResponses && AwaitingResponses == 0)
				{
					if (LogEverything)
						Log.Record($"SerialController {Name}: Ignored unexpected response: \"{Escape(value)}\"");
					return;
				}

				bool invalidResponse;
				lock (Response)
				{
					response = value;
					invalidResponse = ResponseProcessor != null && !ResponseProcessor(response, ResponsesExpected - AwaitingResponses);
					if (invalidResponse)
					{
						if (LogEverything) Log.Record($"SerialController {Name}: Clearing responseQ");
						responseQ = new ConcurrentQueue<string>();  // clear the queue
					}
					else if (AwaitingResponses > 0)
					{
						if (LogEverything) Log.Record($"SerialController {Name}: Decrementing AwaitingResponses");
						AwaitingResponses--;   // decremented only when response is valid
					}

					if (invalidResponse || (AwaitingResponses == 0 && Hurry))
						StateSignal.Set();
				}

				if (invalidResponse && LogEverything)
					Log.Record($"SerialController {Name}: Couldn't interpret response.");
				NotifyPropertyChanged();
			}
		}
		string response = "";


		Thread responseThread;
		AutoResetEvent responseSignal = new AutoResetEvent(false);
		ConcurrentQueue<string> responseQ = new ConcurrentQueue<string>();
		protected void ProcessResponses()
		{
            while (!Stopping || Busy)
            {
                while (responseQ.TryDequeue(out string entry))
                    Response = entry; // this assignment does stuff
                responseSignal.WaitOne(ResponseTimeout);
            }
        }

		#endregion Responses

		#endregion State management

		#region SerialDevice interraction

		void OnConnected(object sender, EventArgs e) { }
		void OnDisconnecting(object sender, EventArgs e)
		{
			LostConnection?.Invoke(this, null);
		}

		/// <summary>
		/// Sends a message to the SerialDevice for transmission.
		/// </summary>
		/// <param name="message">the message to transmit</param>
		/// <returns>true if the SerialDevice is Ready</returns>
		protected virtual bool Send(string message)
		{

			if (LogCommands || LogEverything)
			{
				Log.Record($"SerialController {Name}: Sending \"{Escape(message)}\"");
			}

			bool status = SerialDevice.Command(message);
			CommandCount++;
			return status;
		}


		// This is SerialDevice's ResponseReceived delegate.
		// Forwards the Response to the ResponseProcessor delegates,
		// which has the responsiblity of marshalling if needed.
		// Runs in SerialDevice's prxThread.
		void Receive(string s)
		{
			if (LogResponses || LogEverything)
                Log.Record($"SerialController {Name}: Received \"{Escape(s)}\"");

            lock (responseTimeoutsLocker) ResponseTimeouts = 0;
			
			ResponseCount++;
			responseQ.Enqueue(s ?? "");
			responseSignal.Set();
		}

		#endregion SerialDevice interraction


		public SerialController()
		{
			(this as IStateManager).ManageState = ManageState;
		}
	}
}
