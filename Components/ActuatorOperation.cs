using HACS.Core;
using Newtonsoft.Json;

namespace HACS.Components
{
    public class ActuatorOperation : BindableObject, IActuatorOperation
	{
		[JsonProperty(Order = -99)]
		public virtual string Name
		{
			get => name;
			set => Ensure(ref name, value);
		}
		string name;

		/// <summary>
		/// A numeric characterization of the desired motion, 
		/// typically a position, movement amount, or speed.
		/// </summary>
		[JsonProperty]
        public int Value
		{
			get => _value;
			set => Ensure(ref _value, value);
		}
		int _value;

		/// <summary>
		///  True if the Value figure is incremental, that is, it is 
		///  intended to adjust the present condition rather than
		///  replace it.
		/// </summary>
		[JsonProperty]
        public bool Incremental
		{
			get => incremental;
			set => Ensure(ref incremental, value);
		}
		bool incremental;

		/// <summary>
		/// A string that encapsulates the operation parameters, such as
		/// a space-delimited list of controller commands.
		/// </summary>
		[JsonProperty]
        public string Configuration
		{
			get => configuration;
			set => Ensure(ref configuration, value);
		}
		string configuration;

		public override string ToString()
		{
			return $"{Name}: {Value} {(Incremental ? "Inc" : "Abs")} \"{Configuration}\"";
		}
	}
}
