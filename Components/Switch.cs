using HACS.Core;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Text;

namespace HACS.Components
{
    /// <summary>
    /// An on-off device that can be controlled.
    /// </summary>
    public class Switch : OnOff, ISwitch, Switch.IDevice, Switch.IConfig
    {
        #region static
        public static void DoStopAction(ISwitch d)
        {
            if (d.StopAction == StopAction.TurnOff)
                d.TurnOff();
            else if (d.StopAction == StopAction.TurnOn)
                d.TurnOn();
        }

        #endregion static

        #region HacsComponent
        [HacsPostStart]
        protected virtual void PostStart()
        {
            NotifyConfigChanged(nameof(State));
        }

        [HacsPreStop]
        protected virtual void PreStop() =>
            DoStopAction(this);
        #endregion HacsComponent

        #region Class interface properties and methods

        #region Device interfaces

        public new interface IDevice : OnOff.IDevice { }

        public new interface IConfig : OnOff.IConfig
        {
            /// <summary>
            /// Whether the device should be on.
            /// </summary>
            SwitchState State { get; }

            /// <summary>
            /// What to do with the hardware when its managing instance is Stopped.
            /// </summary>
            StopAction StopAction { get; }
        }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        #region Settings

        protected virtual bool UpdateSwitchState(bool value) =>
            UpdateSwitchState(value.ToSwitchState());

        protected virtual bool UpdateSwitchState(SwitchState value) =>
            Ensure(ref state, value, NotifyConfigChanged, nameof(State));

        /// <summary>
        /// The configured/desired state of the switch.
        /// </summary>
        public virtual SwitchState State
        {
            get => state;
            set => UpdateSwitchState(value);
        }
        [JsonProperty("OnOffState")]
        SwitchState state;
        SwitchState IConfig.State => state;

        /// <summary>
        /// What to do with the hardware device when this instance is Stopped.
        /// </summary>
        [JsonProperty("StopAction"), DefaultValue(StopAction.None)]
        public virtual StopAction StopAction
        {
            get => stopAction;
            set => Ensure(ref stopAction, value);
        }
        StopAction stopAction = StopAction.None;

        #endregion Settings



        /// <summary>
        /// Configure the device to be off.
        /// </summary>
        /// <returns>Whether the device configuration was changed.</returns>
        public virtual bool TurnOff() =>
            UpdateSwitchState(SwitchState.Off);

        /// <summary>
        /// Configure the device to be on.
        /// </summary>
        /// <returns>Whether the device configuration was changed.</returns>
        public virtual bool TurnOn() =>
            UpdateSwitchState(SwitchState.On);

        /// <summary>
        /// Configure the device on or off according to the parameter.
        /// </summary>
        /// <param name="on">true => on, false => off</param>
        /// <returns>Whether the device configuration was changed.</returns>
        public virtual bool TurnOnOff(bool on)
        {
            if (on) return TurnOn();
            return TurnOff();
        }

        public Switch(IHacsDevice d = null) : base(d) { }

        public override string ToString()
        {
            return $"{Name}: {OnOffState}";
        }


        #endregion Class interface properties and methods
    }
}
