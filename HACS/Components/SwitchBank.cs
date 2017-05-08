using HACS.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Utilities;

namespace HACS.Components
{
	public class SwitchBank : Component
    {
		public static new List<SwitchBank> List;
		public static new SwitchBank Find(string name)
		{ return List?.Find(x => x.Name == name); }

		public const bool On = true, Off = false;
		public const bool High = true, Low = false;
		public static OnOffDevice NoDevice = new OnOffDevice();

		static long instanceCount = 0;

		[XmlElement("LabJack")]
		public string LabJackName { get; set; }
		LabJackDaq LabJack;
		public LabJackDaq.DIO MasterResetDio { get; set; }
		public LabJackDaq.DIO LatchEnableDio { get; set; }
		public LabJackDaq.DIO DataDio { get; set; }
		public LabJackDaq.DIO[] AddressDio { get; set; }

		[XmlIgnore] public int Switches = 64;       // maximum addressible number of switches
		[XmlIgnore] public int AddressBits;

		[XmlIgnore] public OnOffDevice[] Device;

		public int LatestAddress;
		public bool IsOn(int ch) { return Device[ch].IsOn; }

		public SwitchBank() { }

		public SwitchBank(string name, string labJackName, LabJackDaq.DIO masterResetDio,
			LabJackDaq.DIO latchEnableDio, LabJackDaq.DIO dataDio, LabJackDaq.DIO[] addressDio)
		{
			Name = name;
			LabJackName = labJackName;
			MasterResetDio = masterResetDio;
			LatchEnableDio = latchEnableDio;
			DataDio = dataDio;
			AddressDio = addressDio;
		}

		// must be called before any Connect(device)
		public override void Connect()
		{
			LabJack = LabJackDaq.Find(LabJackName);
			AddressBits = AddressDio.Length;
			Switches = (int) Math.Pow(2, AddressBits);
			Device = new OnOffDevice[Switches];
			for (int i = 0; i < Switches; i++)
				Device[i] = NoDevice;
		}

		public void Connect(OnOffDevice device)
		{
			Connect(device, device.Channel);
		}

        public void Connect(OnOffDevice device, int channel)
        {
			if (Device[channel] != device)
			{
				Device[channel] = device;
				device.Connect(this);
			}
        }

		public override void Initialize()
		{
			instanceCount++;
			EnsureMemoryMode();
			for (int i = 0; i < Switches; i++)
				if (Device[i] == NoDevice)
					TurnOff(i);
		}

		public void EnsureMemoryMode()
		{
			SetLatchEnable(Low);
			SetMasterReset(High);
		}

		public void Reset()
		{
			SetLatchEnable(Low); // Default State
			SetMasterReset(Low);
			SetMasterReset(High);
		}

		public void SetState(int ch)
		{
			SetAddress(ch);
			SetData(Device[ch].IsOn);
			SetLatchEnable(High);
			SetLatchEnable(Low);
		}

		public void SetAddress(int addr)
        {
            // set each address pin to the appropriate state
            for (int bit = 0, mask = 1; bit < AddressBits; bit++, mask <<= 1)
                SetDio(AddressDio[bit], (addr & mask) != 0);
			LatestAddress = addr;
		}

		public void SetDio(LabJackDaq.DIO dio, bool highLow)
		{
			LabJack.SetDO(dio, highLow);
		}

		public void SetData(bool highLow)
		{
			SetDio(DataDio, highLow);
		}

		public void SetLatchEnable(bool highLow)
		{
			// NOTE: LatchEnable is inverted on the hardware so setting this
			// value high here sends a low to the chip's -LE input
			SetDio(LatchEnableDio, highLow);
		}

		public void SetMasterReset(bool highLow)
		{
			SetDio(MasterResetDio, highLow);
		}

		public void TurnOn(int ch) { Device[ch].IsOn = true; SetState(ch); }
		public void TurnOff(int ch) { Device[ch].IsOn = false; SetState(ch); }

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(Name);
			sb.Append(": ");
			sb.Append(LatestAddress.ToString());
			sb.Append(" (");
			bool moreThanOne = false;
			for (int i = 0; i < Device.Length; i++)
			{
				if (Device[i].IsOn)
				{
					if (moreThanOne)
						sb.Append(" ");
					else
						moreThanOne = true;
					sb.Append(i.ToString());
				}
			}
			sb.Append(")");
			return sb.ToString();
		}
	}
}
