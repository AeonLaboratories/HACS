using Newtonsoft.Json;
using System.ComponentModel;

namespace HACS.Components
{
    public class RxValve : Valve, IRxValve, RxValve.IDevice, RxValve.IConfig
    {
        #region Device interfaces

        public new interface IDevice : Valve.IDevice
        {
            int Position { get; set; }
            int Movement { get; set; }
            int CommandedMovement { get; set; }
            int ConsecutiveMatches { get; set; }
        }
        public new interface IConfig : Valve.IConfig
        { 
            int ConsecutiveMatches { get; }
        }
        public new IDevice Device => this;
        public new IConfig Config => this;

        #endregion Device interfaces

        public override int Position
        {
            get => position;
            protected set => Ensure(ref position, value);
        }
        [JsonProperty]
        int position;
        int IDevice.Position
        {
            get => Position;
            set => Position = value;
        }

        public virtual int CommandedMovement
        {
            get => commandedMovement;
            protected set => Ensure(ref commandedMovement, value);
        }
        int commandedMovement;
        int IDevice.CommandedMovement
        {
            get => CommandedMovement;
            set => CommandedMovement = value;
        }

        public virtual int Movement
        {
            get => movement;
            protected set => Ensure(ref movement, value);
        }
        int movement;
        int IDevice.Movement
        {
            get => Movement;
            set => Movement = value;
        }

        /// <summary>
        /// The number of consecutive occurrences of Movement == CommandedMovement
        /// required to ensure the Target position has been reached and is
        /// stable.
        /// </summary>
        public virtual int ConsecutiveMatches
        {
            get => consecutiveMatches;
            set => Ensure(ref TargetConsecutiveMatches, value, NotifyConfigChanged, nameof(TargetConsecutiveMatches));
        }
        [JsonProperty("ConsecutiveMatches"), DefaultValue(3)]
        int TargetConsecutiveMatches;
        int IConfig.ConsecutiveMatches => TargetConsecutiveMatches;
        int IDevice.ConsecutiveMatches
        {
            get => consecutiveMatches;
            set => Ensure(ref consecutiveMatches, value);
        }
        int consecutiveMatches;

        /// <summary>
        /// The number of ConsecutiveMatches detected has
        /// reached or exceeded the target value.
        /// </summary>
        public bool EnoughMatches => consecutiveMatches >= TargetConsecutiveMatches;

        [JsonProperty]
        public virtual int MinimumPosition
        {
            get => minimumPosition;
            set => Ensure(ref minimumPosition, value);
        }
        int minimumPosition;

        [JsonProperty]
        public virtual int MaximumPosition
        {
            get => maximumPosition;
            set => Ensure(ref maximumPosition, value);
        }
        int maximumPosition;


        [JsonProperty]
        public virtual int PositionsPerTurn
        {
            get => positionsPerTurn;
            set => Ensure(ref positionsPerTurn, value);
        }
        int positionsPerTurn;


        public RxValve(IHacsDevice d = null) : base(d) { }
    }
}
