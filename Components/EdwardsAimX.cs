using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Text;
using Utilities;

namespace HACS.Components
{
    public class EdwardsAimX : SwitchedManometer, IEdwardsAimX, EdwardsAimX.IDevice, EdwardsAimX.IConfig
    {
        #region HacsComponent

        [HacsConnect]
        protected virtual void Connect()
        {
            Switch = Find<IDigitalOutput>(digitalOutputName);
        }

        #endregion HacsComponent

        #region Device interfaces

        public new interface IDevice : SwitchedManometer.IDevice, AnalogInput.IDevice { }
        public new interface IConfig : SwitchedManometer.IConfig, AnalogInput.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;
        AnalogInput.IDevice IAnalogInput.Device => this;
        AnalogInput.IConfig IAnalogInput.Config => this;
        ManagedDevice.IDevice IManagedDevice.Device => this;
        ManagedDevice.IConfig IManagedDevice.Config => this;

        #endregion Device interfaces

        public IDeviceManager Manager { get => AnalogInput.Device.Manager; set => AnalogInput.Device.Manager = value; }
        
        #region AnalogInput
        [JsonProperty]
        public AnalogInputMode AnalogInputMode { get => AnalogInput.AnalogInputMode; set => AnalogInput.AnalogInputMode = value; }
        [JsonProperty]
        public double MaximumVoltage { get => AnalogInput.MaximumVoltage; set => AnalogInput.MaximumVoltage = value; }
        public double MinimumVoltage { get => AnalogInput.MinimumVoltage; set => AnalogInput.MinimumVoltage = value; }
        
        [JsonProperty, DefaultValue(2.0)]
        double errorSignalVoltage = 2.0;

        public new bool OverRange => base.OverRange || AnalogInput.OverRange;
        public new bool UnderRange => base.UnderRange || AnalogInput.UnderRange;

        public double Voltage => AnalogInput.Voltage;
        double AnalogInput.IDevice.Voltage
        { 
            get => AnalogInput.Voltage;
            set
            {
                AnalogInput.Device.Voltage = value;
                Update(Voltage);
                if (Valid)
                    Error = Voltage < errorSignalVoltage ? 1 : 0;
            }
        }


        #endregion AnalogInput

        public virtual int Error
        {
            get => error;
            set => Ensure(ref error, value);
        }
        int error = 0;


        public override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == AnalogInput)
                NotifyPropertyChanged(e?.PropertyName);
            else
                base.OnPropertyChanged(sender, e);
        }

        public override void OnConfigChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == AnalogInput)
                NotifyConfigChanged(e?.PropertyName);
            else
                base.OnConfigChanged(sender, e);
        }

        AnalogInput AnalogInput { get; set; }

        [JsonProperty("DigitalOutput")]
        string DigitalOutputName { get => Switch?.Name; set => digitalOutputName = value; }
        string digitalOutputName;

        public EdwardsAimX(IHacsDevice d = null) : base(d)
        {
            AnalogInput = new AnalogInput(d ?? this);
        }

        public override string ToString()
		{
			var sb = new StringBuilder(base.ToString().Replace(UnitSymbol, $"{UnitSymbol}, {IsOn.OnOff()}"));
            if (IsOn) 
                sb.Append(Utility.IndentLines($"\r\n({Voltage:0.0000} V)"));

            if (Error != 0)
				sb.Append("\r\nError Detected: Service Required?");
            sb.Append(ManagedDevice.ManagerString(this));
            return sb.ToString();
		}
	}
}
