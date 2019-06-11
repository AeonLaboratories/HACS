using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HACS.Core
{
	public class StringINamedObjectConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType) => objectType is INamedObject;

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.Value == null) return null;

			var no = (INamedObject)Activator.CreateInstance(objectType);
			no.Name = reader.Value.ToString();
			return no;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
			JToken.FromObject((value as INamedObject).Name).WriteTo(writer);
	}

	public class DictionaryHacsComponentListConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType) => objectType is IList<INamedObject>;

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var o = reader.Value;
			return o;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
			JToken.FromObject((value as IList).Cast<INamedObject>().Select(obj => new { obj.Name, obj })
				.ToDictionary(wrapper => wrapper.Name, wrapper => wrapper.obj)).WriteTo(writer);
	}
}
