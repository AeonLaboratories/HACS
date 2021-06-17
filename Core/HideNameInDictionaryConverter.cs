using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;

namespace HACS.Core
{
	public class HideNameInDictionaryConverter : JsonConverter
	{
		public static HideNameInDictionaryConverter Default = new HideNameInDictionaryConverter();

		public override bool CanConvert(Type typeToConvert) =>
			typeof(IDictionary).IsAssignableFrom(typeToConvert) && typeof(INamedObject).IsAssignableFrom(typeToConvert.GetGenericArguments()[1]);

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var dictionary = JToken.ReadFrom(reader);
			var result = (IDictionary)Activator.CreateInstance(objectType);
			var genericType = objectType.GetGenericArguments()[1];

			dictionary.Children<JProperty>().ToList().ForEach(child =>
			{
				var objType = child.Value["$type"]?.ToObject<Type>() ?? genericType;
				child.Value[nameof(INamedObject.Name)] = child.Name;
				var value = (INamedObject)child.Value.ToObject(objType, serializer);
				result.Add(value.Name, value);
			});

			return result;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			serializer.Converters.Remove(Default);

			var dictionary = JObject.FromObject(value, serializer);

			dictionary.Children<JProperty>().ToList().ForEach(child =>
			{
				string name = (string)child.Value[nameof(INamedObject.Name)];
				child.Value[nameof(INamedObject.Name)].Parent.Remove();
				child.Replace(new JProperty(name, child.Value));
			});

			dictionary.WriteTo(writer);

			serializer.Converters.Add(Default);
		}
	}
}
