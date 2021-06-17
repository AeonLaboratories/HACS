using HACS.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Utilities;

namespace HACS.Components
{
	public class InletPort : LinePort, IInletPort
	{
		#region HacsComponent

		protected override void Connect()
		{
			base.Connect();
			QuartzFurnace = Find<IHeater>(quartzFurnaceName);
			SampleFurnace = Find<IHeater>(sampleFurnaceName);
			Fan = Find<ISwitch>(fanName);
			PathToFirstTrap = FindAll<IValve>(pathToVTTValveNames);
		}

		#endregion HacsComponent

		public enum Type { Combustion, Needle, Manual, GasSupply }

		[JsonProperty]
		public virtual List<Type> SupportedPortTypes
		{
			get => supportedPortTypes;
			set => Ensure(ref supportedPortTypes, value);
		}
		List<Type> supportedPortTypes;

		[JsonProperty]
		public virtual Type PortType
		{
			get => portType;
			set => Ensure(ref portType, value);
		}
		Type portType;

        public override string Contents => Sample?.LabId ?? "<none>";

		[JsonProperty]
		public bool NotifySampleFurnaceNeeded
		{
			get => notifySampleFurnaceNeeded;
			set => Ensure(ref notifySampleFurnaceNeeded, value);
		}
		bool notifySampleFurnaceNeeded;

		[JsonProperty, DefaultValue(40)]
		public int WarmTemperature
		{
			get => warmTemperature;
			set => Ensure(ref warmTemperature, value);
		}
		int warmTemperature = 40;

		[JsonProperty("QuartzFurnace")]
		string QuartzFurnaceName { get => QuartzFurnace?.Name; set => quartzFurnaceName = value; }
		string quartzFurnaceName;
		public IHeater QuartzFurnace
		{
			get => quartzFurnace;
			set => Ensure(ref quartzFurnace, value);
		}
		IHeater quartzFurnace;

		[JsonProperty("SampleFurnace")]
		string SampleFurnaceName { get => SampleFurnace?.Name; set => sampleFurnaceName = value; }
		string sampleFurnaceName;
		public IHeater SampleFurnace
		{
			get => sampleFurnace;
			set => Ensure(ref sampleFurnace, value);
		}
		IHeater sampleFurnace;

		[JsonProperty("Fan")]
		string FanName { get => Fan?.Name; set => fanName = value; }
		string fanName;
		public ISwitch Fan
		{
			get => fan;
			set => Ensure(ref fan, value);
		}
		ISwitch fan;

		[JsonProperty("PathToFirstTrap")]
		List<string> PathToFirstTrapValveNames { get => PathToFirstTrap?.Names(); set => pathToVTTValveNames = value; }
		List<string> pathToVTTValveNames;
		public List<IValve> PathToFirstTrap
		{
			get => pathToFirstTrap;
			set => Ensure(ref pathToFirstTrap, value);
		}
		List<IValve> pathToFirstTrap;

		public virtual void TurnOffFurnaces()
		{
			QuartzFurnace?.TurnOff();
			SampleFurnace?.TurnOff();
		}

		public virtual void Update()
		{
			if (Fan is null)
				return;
			if ((QuartzFurnace?.IsOn ?? false) || SampleFurnace?.Temperature >= WarmTemperature)
				Fan.TurnOn();
			else
				Fan.TurnOff();
		}

		public override string ToString()
		{
			var sb = new StringBuilder($"{Name}: {State}");
			if (Sample == null)
				sb.Append(" (no sample)");
			else
				sb.Append($", {Sample.LabId}, {Sample.Grams:0.000000} g");
			var sb2 = new StringBuilder();
			if (SampleFurnace != null) sb2.Append($"\r\n{SampleFurnace}");
			if (QuartzFurnace != null) sb2.Append($"\r\n{QuartzFurnace}");
			if (Fan != null) sb2.Append($"\r\n{Fan}");
			return sb.Append(Utility.IndentLines(sb2.ToString())).ToString();
		}
	}
}
