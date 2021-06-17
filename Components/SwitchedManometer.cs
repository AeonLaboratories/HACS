using HACS.Core;
using Newtonsoft.Json;
using System.ComponentModel;

namespace HACS.Components
{
    public class SwitchedManometer : Manometer, ISwitchedManometer, SwitchedManometer.IDevice, SwitchedManometer.IConfig
    {
        #region Device interfaces

        public new interface IDevice : Manometer.IDevice, Switch.IDevice { }
        public new interface IConfig : Manometer.IConfig, Switch.IConfig { }
        public new IDevice Device => this;
        public new IConfig Config => this;
        Switch.IDevice ISwitch.Device => this;
        Switch.IConfig ISwitch.Config => this;
        OnOff.IDevice IOnOff.Device => this;
        OnOff.IConfig IOnOff.Config => this;

        #endregion Device interfaces

        #region Switch

        public SwitchState State
        {
            get => Switch?.State ?? SwitchState.Off;
            set
            {
                if (Switch != null)
                    Switch.State = value;
            }
        }
        public virtual OnOffState OnOffState => Switch?.OnOffState ?? OnOffState.Unknown;
        OnOffState OnOff.IDevice.OnOffState
        {
            get => Switch?.Device?.OnOffState ?? OnOffState.Unknown;
            set
            {
                if (Switch?.Device != null)
                    Switch.Device.OnOffState = value;
            }
        }
        public virtual bool IsOn => Switch?.IsOn ?? false;
        public virtual bool IsOff => Switch?.IsOff ?? false;
        
        public virtual StopAction StopAction
        {
            get => Switch?.StopAction ?? StopAction.None;
            set
            {
                if (Switch != null)
                    Switch.StopAction = value;
            }
        }

        public virtual long MillisecondsOn => Switch?.MillisecondsOn ?? 0;
        public virtual long MillisecondsOff => Switch?.MillisecondsOff ?? 0;
        public virtual long MillisecondsInState => Switch?.MillisecondsInState ?? 0;

        public virtual bool TurnOn()
        {
            if (IsOn || MillisecondsInState <= MinimumMillisecondsOff)
                return false;
            return Switch?.TurnOn() ?? false;
        }
        public virtual bool TurnOff() => Switch?.TurnOff() ?? false;
        public virtual bool TurnOnOff(bool on) => Switch?.TurnOnOff(on) ?? false;

        #endregion Switch

        [JsonProperty, DefaultValue(5000)]
        public virtual int MillisecondsToValid
        {
            get => millisecondsToValid;
            set => Ensure(ref millisecondsToValid, value);
        }
        int millisecondsToValid = 5000;

        [JsonProperty, DefaultValue(2000)]
        public virtual int MinimumMillisecondsOff
        {
            get => minimumMillisecondsOff;
            set => Ensure(ref minimumMillisecondsOff, value);
        }
        int minimumMillisecondsOff = 2000;

        public virtual bool Valid => MillisecondsOn >= MillisecondsToValid;

		public override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == Switch)
                NotifyPropertyChanged(e?.PropertyName);
            else
                base.OnPropertyChanged(sender, e);
        }

        public override void OnConfigChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == Switch)
                NotifyConfigChanged(e?.PropertyName);
            else
                base.OnConfigChanged(sender, e);
        }

        protected ISwitch Switch
        {
            get => _switch;
            set
            {
                if (_switch != null)
                {
                    _switch.PropertyChanged -= OnPropertyChanged;
                    _switch.ConfigChanged -= OnConfigChanged;
                }
                _switch = value;
                _switch.PropertyChanged += OnPropertyChanged;
                _switch.ConfigChanged += OnConfigChanged;
            }
        }
        ISwitch _switch;

        public SwitchedManometer(IHacsDevice d = null) : base(d) { }

        public string ManometerString() => base.ToString();
        public override string ToString() => IsOn ? ManometerString() : $"{Name}: (Off)";
    }
}
