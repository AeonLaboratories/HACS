using HACS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace HACS.Components
{
	public class XGS_600 : Controller
	{
		#region ComponentImplementation

		public static new List<XGS_600> List = new List<XGS_600>();
		public static new XGS_600 Find(string name) => List?.Find(x => x.Name == name);

		protected override void Initialize()
		{
			ResponseProcessor = GetResponse;

			responseLoop = Task.Run(ResponseLoop, correspondence.Token);
			commandLoop = Task.Run(CommandLoop, correspondence.Token);

			base.Initialize();
		}

		protected override void Stop()
		{
			correspondence.Cancel();
			responseSignal.Set();
			commandSignal.Set();

			base.Stop();
		}

		public XGS_600()
		{
			TargetUnits = Units;
			List.Add(this);
		}

		#endregion ComponentImplementation

		public enum PressureUnits { Torr = 0x00, mBar = 0x01, Pascal = 0x02 }

		public enum Commands { ReadPressureDump = 0x0F, SetPressureUnits = 0x10, ReadPressureUnits = 0x13 }

		const string InvalidCommand = "?FF";
		const string Address = "00"; // RS232 Communication
		const char TerminationChar = '\r';
		
		CancellationTokenSource correspondence = new CancellationTokenSource();

		Task responseLoop;
		AutoResetEvent responseSignal = new AutoResetEvent(false);
		Queue<string> responseQ = new Queue<string>();

		Task commandLoop;
		AutoResetEvent commandSignal = new AutoResetEvent(false);

		public int ResponseTimeout { get; set; } = 500;
		public int CommandTimeout { get; set; } = 200;

		Commands LastCommand;

		[XmlArray("Gauges")]
        [XmlArrayItem("DynamicQuantityRef")]
        public List<HacsComponent<DynamicQuantity>> GaugeRefs { get; set; }
        [XmlIgnore] public List<DynamicQuantity> Gauges => GaugeRefs?.Select(gr => gr.Component).ToList();

		public PressureUnits Units { get; protected set; }
		PressureUnits TargetUnits;

		public void SetPressureUnits(PressureUnits pressureUnits) => TargetUnits = pressureUnits;

		protected virtual void ProcessResponse(string response)
		{
			if (response == InvalidCommand)
			{
				Log.Record($"Invalid Command: {LastCommand}");
				return;
			}
			switch (LastCommand)
			{
				case Commands.ReadPressureDump:
					string[] pressures = response.Split(',');
					int i = 0;
					Gauges?.ForEach(gauge =>
					{
						if (gauge != null)
						{
							gauge.Value = double.Parse(pressures[i]);
							i++;
						}
					});
					break;
				case Commands.ReadPressureUnits:
					Units = (PressureUnits)int.Parse(response);
					break;
				case Commands.SetPressureUnits:
				default:
					// No response
					break;
			}
		}

		protected virtual void ResponseLoop()
		{
			string response;
			try
			{
				while (!correspondence.IsCancellationRequested)
				{
					try
					{
						// Done inside of a try catch to supress the error of an empty queue
						// Empty queue results in continuation of loop and skips the
						// ProcessResponse method.
						lock (responseQ) response = responseQ.Dequeue();

						ProcessResponse(response);
					}
					catch { }
					responseSignal.WaitOne(ResponseTimeout);
				}
			}
			catch (Exception e)
			{
				throw (e);
			}
		}

		protected virtual void GetResponse(string response)
		{
			lock (responseQ) responseQ.Enqueue(response);
			responseSignal.Set();
		}

		protected virtual void SendCommand(Commands cmd)
		{
			string commandString = $"#{Address}";
			switch (cmd)
			{
				case Commands.ReadPressureDump:
					commandString += $"{(int)cmd:X2}";
					break;
				case Commands.SetPressureUnits:
					commandString += $"{(int)cmd + (int)TargetUnits:X2}";
					break;
				case Commands.ReadPressureUnits:
					commandString += $"{(int)cmd:X2}";
					break;
				default:
					break;
			}
			commandString += TerminationChar;
			Command(commandString);
			LastCommand = cmd;
		}

		protected virtual void SendCommand()
		{
			if (CommandCount == 0)
				SendCommand(Commands.SetPressureUnits);
			else if (LastCommand == Commands.SetPressureUnits)
				SendCommand(Commands.ReadPressureUnits);
			else if (TargetUnits != Units)
				SendCommand(Commands.SetPressureUnits);
			else
				SendCommand(Commands.ReadPressureDump);
		}

		protected virtual void CommandLoop()
		{
			try
			{
				while (!correspondence.IsCancellationRequested)
				{
					SendCommand();
					commandSignal.WaitOne(CommandTimeout);
				}
			}
			catch (Exception e)
			{
				throw (e);
			}
		}
	}
}
