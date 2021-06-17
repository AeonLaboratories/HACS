using HACS.Core;
using System.ComponentModel;


namespace HACS.Components
{
    public class DetectorSwitch : BindableObject, IDetectorSwitch
    {
        public enum RuleCode { None, BelowSwitchpoint, AtSwitchpoint, AboveSwitchpoint }

        /// <summary>
        /// The sensor to monitor for Switchpoint
        /// </summary>
        public ISensor Sensor
        {
            get => sensor;
            set
            {
                if (Ensure(ref sensor, value))
                {
                    if (Detector != null)
                        Detector.Sensor = Sensor;
                }
            }
        }
        ISensor sensor;

        /// <summary>
        /// The Switch to operate in accordance with the Switchpoint.
        /// </summary>
        public ISwitch Switch
        {
            get => _switch;
            set => Ensure(ref _switch, value);
        }
        ISwitch _switch;

        /// <summary>
        /// Sensor.Value is compared to Switchpoint
        /// </summary>
        public double? Switchpoint
        {
            get => switchpoint;
            set => Ensure(ref switchpoint, value);
        }
        double? switchpoint;

        public RuleCode SwitchpointRule
        {
            get => switchpointRule;
            set => Ensure(ref switchpointRule, value);
        }
        RuleCode switchpointRule = RuleCode.None;

        RuleCode priorState = RuleCode.None;

        /// <summary>
        /// 
        /// </summary>
        public OnOffState DetectedState
        {
            get => detectedState;
            set => Ensure(ref detectedState, value);
        }
        OnOffState detectedState = OnOffState.On;

        Detector Detector
        {
            get => detector;
            set
            {
                detector.Condition = Condition;
                detector.Detected = OnPropertyChanged;
                detector.Sensor = Sensor;
            }
        }
        Detector detector = new Detector();

        bool Condition()
        {
            if (Sensor == null || Switchpoint == null || SwitchpointRule == RuleCode.None)
                return false;
            var value = Sensor.Value;
            RuleCode state;
            if (value < Switchpoint)
                state = RuleCode.BelowSwitchpoint;
            else if (value > Switchpoint)
                state = RuleCode.AboveSwitchpoint;
            else
                state = RuleCode.AtSwitchpoint;

            var result = state == SwitchpointRule ||
            (
                SwitchpointRule == RuleCode.AtSwitchpoint &&
                (
                    priorState == RuleCode.BelowSwitchpoint &&
                        state == RuleCode.AboveSwitchpoint ||
                    priorState == RuleCode.AboveSwitchpoint &&
                        state == RuleCode.BelowSwitchpoint
                )
            );
            priorState = state;
            return result;
        }


        public void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var propertyName = e.PropertyName;
            if (sender == Detector)
            {
                if (e == Detector.DetectedEventArgs)
                {
                    Switch.TurnOnOff(DetectedState.IsOn());
                    NotifyPropertyChanged(nameof(Switch));
                }
            }
        }
    }
}
