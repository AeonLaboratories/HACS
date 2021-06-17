using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using Utilities;

namespace HACS.Core
{
	/// <summary>
	/// A "Configuration" property: a class property whose desired
	/// value "Config" is distinct from its actual state 
	/// "Value", which might for example, be obtained by querying 
	/// a physical device.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	[JsonConverter(typeof(TConfigPropertyConverter))]
	public class ConfigProperty<T> : BindableObject
	{
		public static implicit operator T(ConfigProperty<T> x)
			=> x == null ? default : x.Value;

		/// <summary>
		/// Sets the ConfigProperty to the specified value and Initializes it.
		/// </summary>
		/// <typeparam name="Tcp"></typeparam>
		/// <param name="configProperty">A reference to the ConfigProperty backing field or property</param>
		/// <param name="value">The ConfigProperty's new value</param>
		/// <param name="configChangedHandler1">A ConfigChanged handler to be invoked when the 
		/// ConfigProperty's Config property changes.</param>
		/// <param name="valueChangedHandler1">A ValueChanged handler to be invoked when the 
		/// ConfigProperty's Value property changes.</param>
		/// <param name="configChangedHandler2">A ConfigChanged handler to be invoked when the 
		/// ConfigProperty's Config property changes.</param>
		/// <param name="valueChangedHandler2">A ValueChanged handler to be invoked when the 
		/// ConfigProperty's Value property changes.</param>
		public static ConfigProperty<Tcp> SetConfigProperty<Tcp>(
			ref ConfigProperty<Tcp> configProperty,
			string senderName = default,
			ConfigProperty<Tcp> value = default,
			Action<string> configChangedHandler1 = default,
			Action<string> valueChangedHandler1 = default,
			Action<string> configChangedHandler2 = default,
			Action<string> valueChangedHandler2 = default
			)
		{
			bool newInstance = value == default;
			if (newInstance)
				value = new ConfigProperty<Tcp>();
			value.Name = senderName;
			value.ConfigChanged += configChangedHandler1;
			value.ConfigChanged += configChangedHandler2;
			value.ValueChanged += valueChangedHandler1;
			value.ValueChanged += valueChangedHandler2;
			configProperty = value;
			return configProperty;
		}

		public string Name;     // these have a Name, but are not NamedObjects

		/// <summary>
		/// The desired value for the represented property, e.g., a setting
		/// intended to be conveyed to a physical device.
		/// </summary>
		[JsonProperty]
		public T Config
		{
			get => _Config;
			set
			{
				if(ChangeConfig(ref _Config, value))
					ConfigChanged?.Invoke(Name);
				if (AlwaysSetConfig)
					ChangeConfig = Set;
				else
					ChangeConfig = Update;
			}
		}
		T _Config = default;

		/// <summary>
		/// The actual value of the represented property, e.g., a value retreived 
		/// from a physical device.
		/// </summary>
		public T Value
		{
			get => _Value;
			set
			{
				if (ChangeValue(ref _Value, value))
					ValueChanged?.Invoke(Name);
				if (AlwaysSetValue)
					ChangeValue = Set;
				else
					ChangeValue = Update;
			}
		}
		T _Value = default;


		/// <summary>
		/// The PropertySetter to use for changing Config.
		/// </summary>
		PropertySetter<T> ChangeConfig { get; set; }
		/// <summary>
		/// Whether to set Config and invoke the BindableObject's PropertyChanged event
		/// even when the new config value equals the present one. The default is false.
		/// </summary>
		public bool AlwaysSetConfig = false;
		/// <summary>
		/// Actions to be invoked whenever the ConfigProperty's Config changes.
		/// These are also invoked when the ConfigProperty instance is Initialized.
		/// </summary>
		public Action<string> ConfigChanged;


		/// <summary>
		/// The PropertySetter to use for changing Value.
		/// </summary>
		PropertySetter<T> ChangeValue { get; set; }
		/// <summary>
		/// Whether to set Value and invoke BindableObject's PropertyChanged event
		/// even when the new state value equals the present one. The default is false.
		/// </summary>
		public bool AlwaysSetValue = false;
		/// <summary>
		/// Actions to be invoked whenever the ConfigProperty's Value changes.
		/// Note that these are NOT invoked when the ConfigProperty instance is Initialized.
		/// </summary>
		public Action<string> ValueChanged;

		/// <summary>
		/// Whether the actual Value equals the desired Config.
		/// </summary>
		public virtual bool Configured => Value.Equals(Config);

		
		public ConfigProperty()
		{
			// Always use Set the first time Config or Value is received,
			// even if AlwaysSetConfig/AlwaysSetValue is false.
			ChangeConfig = Set;
			ChangeValue = Set;
		}

		public ConfigProperty(T config) : this() =>
			_Config = config;

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder($"{Name}");
			StringBuilder sb2 = new StringBuilder();
			sb2.Append($"\r\nConfig = {Config}");
			sb2.Append($"\r\nValue = {Value}");
			sb.Append(Utility.IndentLines(sb2.ToString()));
			return sb.ToString();
		}
	}

	public class TConfigPropertyConverter : JsonConverter
	{
		public static TConfigPropertyConverter Default = new TConfigPropertyConverter();

		public override bool CanConvert(Type objectType) =>
			objectType == typeof(ConfigProperty<>);

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var token = JToken.ReadFrom(reader);
			if (token.Type == JTokenType.Null)
				return Activator.CreateInstance(objectType);
			else
				return Activator.CreateInstance(objectType, new object[] { token?.ToObject(objectType.GetGenericArguments()[0], serializer) });
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if (((dynamic)value)?.Config is object config)
				JToken.FromObject(config, serializer).WriteTo(writer);
			else if (serializer.NullValueHandling == NullValueHandling.Include)
				writer.WriteNull();
		}
	}
}
