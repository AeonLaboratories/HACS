using System.Collections.Generic;
using System.Xml.Serialization;

namespace HACS.Components
{
    [XmlInclude(typeof(EurothermFurnace))]
    [XmlInclude(typeof(Eurotherm818Furnace))]
    [XmlInclude(typeof(MtiFurnace))]
    public class TubeFurnace : Controller
    {
		#region Component Implementation

		public static readonly new List<TubeFurnace> List = new List<TubeFurnace>();
		public static new TubeFurnace Find(string name) { return List.Find(x => x?.Name == name); }

		public TubeFurnace()
		{
			List.Add(this);
		}

		#endregion Component Implementation


		public virtual double Setpoint { get; }
        public virtual double Temperature { get; }

        /// <summary>
        /// Sets the desired furnace temperature.
        /// </summary>
        /// <param name="setpoint"></param>
        public virtual void SetSetpoint(double setpoint) { }

        /// <summary>
        /// Sets the Setpoint rate limit (deg/minute; 0 means no limit).
        /// This driver thereafter ramps the Setpoint
        /// to programmed levels at the given rate.
        /// </summary>
        /// <param name="degreesPerMinute"></param>
        public virtual void SetRampRate(double degreesPerMinute) { }

        /// <summary>
        /// Turns off the furnace.
        /// </summary>
        public virtual void TurnOff() { }

        /// <summary>
        /// Turns the furnace on.
        /// </summary>
        public virtual void TurnOn() { }

        /// <summary>
        /// Sets the furnace temperature and turns it on.
        /// </summary>
        /// <param name="setpoint">Desired furnace temperature (°C)</param>
        public virtual void TurnOn(double setpoint) { }

        /// <summary>
        /// Sets the furnace temperature and turns it on.
        /// If the furnace is on when the specified time elapses, it is turned off.
        /// </summary>
        /// <param name="setpoint">Desired furnace temperature (°C)</param>
        /// <param name="minutes">Maximum number of minutes to remain on</param>
        public virtual void TurnOn(double setpoint, double minutes) { }

        public virtual bool UseTimeLimit { get; set; }
        public virtual double TimeLimit { get; set; }

        /// <summary>
        /// True if the furnace is on, except during system startup, 
        /// when the returned value indicates whether the furnace 
        /// is supposed to be on, instead.
        /// </summary>
        public virtual bool IsOn { get; set; }

        [XmlIgnore] public virtual string Report { get; set; }

        [XmlIgnore] public virtual double MinutesOn { get; }
        [XmlIgnore] public virtual double MinutesOff { get; }
        [XmlIgnore] public virtual double MinutesInState { get; }
    }
}