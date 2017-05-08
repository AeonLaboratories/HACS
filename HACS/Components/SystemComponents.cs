using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Linq;
using System.Text;
using Utilities;

namespace HACS.Components
{
	public class SystemComponents
	{
		public List<LabJackDaq> DAQs { get { return LabJackDaq.List; } set { LabJackDaq.List = value; } }
		public List<Meter> Meters { get { return Meter.List; } set { Meter.List = value; } }
		public List<DigitalOutput> DigitalOutputs { get { return DigitalOutput.List; } set { DigitalOutput.List = value; } }

		public List<ServoController> ActuatorControllers { get { return ServoController.List; } set { ServoController.List = value; } }
		public List<Valve> Valves { get { return Valve.List; } set { Valve.List = value; } }

		public List<ThermalController> ThermalControllers { get { return ThermalController.List; } set { ThermalController.List = value; } }
		public List<Heater> Heaters { get { return Heater.List; } set { Heater.List = value; } }
		public List<TempSensor> TempSensors { get { return TempSensor.List; } set { TempSensor.List = value; } }

		public List<SwitchBank> SwitchBanks { get { return SwitchBank.List; } set { SwitchBank.List = value; } }
		public List<OnOffDevice> OnOffDevices { get { return OnOffDevice.List; } set { OnOffDevice.List = value; } }

		public List<Tank> Tanks { get { return Tank.List; } set { Tank.List = value; } }
		public List<FTColdfinger> FTCs { get { return FTColdfinger.List; } set { FTColdfinger.List = value; } }
		public List<VTT> VTTs { get { return VTT.List; } set { VTT.List = value; } }
		public List<GraphiteReactor> GRs { get { return GraphiteReactor.List; } set { GraphiteReactor.List = value; } }

		public List<DynamicQuantity> DynamicQuantities { get { return DynamicQuantity.List; } set { DynamicQuantity.List = value; } }

		public List<LinePort> LinePorts { get { return LinePort.List; } set { LinePort.List = value; } }

		public List<ProcessSequence> ProcessSequences { get { return ProcessSequence.List; } set { ProcessSequence.List = value; } }

		public List<Sample> Samples = new List<Sample>();

		public List<MassFlowController> MFCs { get { return MassFlowController.List; } set { MassFlowController.List = value; } }
		public List<EurothermFurnace> EurothermFurnaces { get { return EurothermFurnace.List; } set { EurothermFurnace.List = value; } }
	}
}
