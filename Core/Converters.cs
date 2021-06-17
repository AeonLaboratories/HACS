using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HACS.Core
{
	public class NamedObjectConverter : JsonConverter<INamedObject>
	{
		public override INamedObject ReadJson(JsonReader reader, Type objectType, INamedObject existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			JObject item = JObject.Load(reader);
			Type type = Type.GetType(item["$type"].Value<string>());
			var no = (INamedObject)Activator.CreateInstance(type);
			no.Name = item["Name"].Value<string>();
			return no;
		}

		public override void WriteJson(JsonWriter writer, INamedObject value, JsonSerializer serializer)
		{
			var type = value.GetType();
			writer.WriteStartObject();
			writer.WritePropertyName("$type");
			writer.WriteValue($"{type}, {type.Assembly.GetName().Name}");
			writer.WritePropertyName("Name");
			writer.WriteValue(value.Name);
			writer.WriteEndObject();
		}
	}
}
