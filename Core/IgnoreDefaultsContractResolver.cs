using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.Reflection;

namespace HACS.Core
{
	public class IgnoreDefaultsContractResolver : DefaultContractResolver
	{
		protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
		{
			var property = base.CreateProperty(member, memberSerialization);
			if (property.DeclaringType.Default()?.GetValue(null) is object o)
			{
				var ov = property.DeclaringType.GetInstanceMember(property.UnderlyingName)?.GetValue(o);
				property.DefaultValue = ov;
			}
			return property;
		}
	}
}
