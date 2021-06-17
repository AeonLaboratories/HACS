using HACS.Core;
using Newtonsoft.Json;
using Utilities;

namespace HACS.Components
{
    /// <summary>
    /// A device that can be on or off.
    /// </summary>
    public class OnOff : HacsDevice, IOnOff, OnOff.IDevice, OnOff.IConfig
    {

        #region Class interface properties and methods

        #region Device interfaces

        public new interface IDevice : HacsDevice.IDevice
        { 
            OnOffState OnOffState { get; set; } 
        }

        public new interface IConfig : HacsDevice.IConfig { }

        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        #region Settings
        /// <summary>
        /// The device is known to be On. (False if OnOffState is Unknown or Off.)
        /// </summary>
        public virtual bool IsOn => onOffState.IsOn();
        /// <summary>
        /// The device is known to be Off. (False if OnOffState is Unknown or On.)
        /// </summary>
        public virtual bool IsOff => onOffState.IsOff();

        [JsonProperty("OnOffState")]
        public virtual OnOffState OnOffState
        { 
            get => onOffState; 
            protected set
            {
                if (Ensure(ref onOffState, value))
                {
                    StateStopwatch.Restart();
                    NotifyPropertyChanged(nameof(IsOn));
                    NotifyPropertyChanged(nameof(IsOff));
                }
            }
        }
        OnOffState onOffState = OnOffState.Unknown;
        OnOffState IDevice.OnOffState
        {
            get => OnOffState;
            set => OnOffState = value;
        }

        #endregion Settings

        public virtual long MillisecondsOn => IsOn ? MillisecondsInState : 0;
        public virtual long MillisecondsOff => IsOff ? MillisecondsInState : 0;
        public virtual long MillisecondsInState => StateStopwatch.ElapsedMilliseconds;

        public OnOff()
        {
            StateStopwatch.Restart();
        }


        #endregion Class interface properties and methods

        Stopwatch StateStopwatch = new Stopwatch();

        public OnOff(IHacsDevice d = null) : base(d)
        {
            StateStopwatch.Restart();
        }

        public override string ToString()
        {
            return $"{Name}: {Device.OnOffState}";
        }
    }
}
