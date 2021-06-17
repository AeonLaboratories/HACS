using HACS.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace System.Collections.Generic
{

    public static class ListExtensions
	{
		public static List<string> Names<T>(this List<T> source) where T : INamedObject =>
			source?.Select(x => x?.Name)?.ToList();

		/// <summary>
		/// Treats null values as empty sets, to avoid ArgumentNullException.
		/// </summary>
		public static List<T> SafeUnion<T>(this IEnumerable<T> a, IEnumerable<T> b)
		{
			IEnumerable<T> s;
			if (a == null)
				s = b;
			else if (b == null)
				s = a;
			else
				s = a.Union(b);
			return s?.ToList();
		}

		/// <summary>
		/// Treats null values as empty sets, to avoid ArgumentNullException.
		/// </summary>
		public static List<T> SafeIntersect<T>(this IEnumerable<T> a, IEnumerable<T> b)
		{
			if (a == null || b == null) return null;
			return a.Intersect(b).ToList();
		}

		/// <summary>
		/// Treats null values as empty sets, to avoid ArgumentNullException.
		/// </summary>
		public static List<T> SafeExcept<T>(this IEnumerable<T> fromThese, IEnumerable<T> subtractThese)
		{
			if (fromThese == null) return null;

			IEnumerable<T> s;
			if (subtractThese == null)
				s = fromThese;
			else
				s = fromThese.Except(subtractThese);
			return s.ToList();
		}
	}

	public static class DictionaryExtensions
    {
        public static Dictionary<string, string> KeysNames<T>(this Dictionary<string, T> source) where T : INamedObject =>
            source?.ToDictionary(x => x.Key, x => x.Value.Name);
    }
}

namespace System.Reflection
{
    public static class TypeExtensions
	{
		public static MemberInfo Default(this Type t) =>
			t?.GetMember("Default", MemberTypes.Field | MemberTypes.Property, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.FirstOrDefault();

		public static MemberInfo GetInstanceMember(this Type t, string propertyName) =>
			t?.GetMember(propertyName, MemberTypes.Field | MemberTypes.Property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.FirstOrDefault();
	}

	public static class MemberInfoExtensions
	{
		public static void SetValue(this MemberInfo member, object property, object value)
		{
			if (member.MemberType == MemberTypes.Property)
				((PropertyInfo)member).SetValue(property, value);
			else if (member.MemberType == MemberTypes.Field)
				((FieldInfo)member).SetValue(property, value);
			else
				throw new Exception("Property must be of type FieldInfo or PropertyInfo");
		}

		public static object GetValue(this MemberInfo member, object property)
		{
			if (member.MemberType == MemberTypes.Property)
				return ((PropertyInfo)member).GetValue(property);
			else if (member.MemberType == MemberTypes.Field)
				return ((FieldInfo)member).GetValue(property);
			else
				throw new Exception("Property must be of type FieldInfo or PropertyInfo");
		}
	}
}

namespace HACS.Core
{
    public static class OnOffStateExtensions
	{
		public static bool IsOn(this OnOffState s) => s == OnOffState.On;
		public static bool IsOff(this OnOffState s) => s == OnOffState.Off;
		public static bool IsUnknown(this OnOffState s) => s == OnOffState.Unknown;
	}

	public static class SwitchStateExtensions
	{
		public static bool IsOn(this SwitchState s) => s == SwitchState.On;
		public static bool IsOff(this SwitchState s) => s == SwitchState.Off;
	}

}

namespace System
{
    public static class StringExtensions
	{
		/// <summary>
		/// Shorthand for string.IsNullOrWhiteSpace()
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static bool IsBlank(this string s) =>
			string.IsNullOrWhiteSpace(s);

		public static bool Includes(this string s, string token) =>
			s?.Contains(token) ?? false;

		public static char[] LineDelimiters = { '\r', '\n' };

		public static string[] GetLines(this string s) =>
			s.Split(LineDelimiters, StringSplitOptions.RemoveEmptyEntries);

		public static string[] GetValues(this string s) =>
			s.Split(null as char[], StringSplitOptions.RemoveEmptyEntries);
	}

	public static class BoolExtensions
	{
		public static string ToString(this bool value, string trueString, string falseString)
			=> value ? trueString : falseString;
		public static string YesNo(this bool value) =>
			value.ToString("Yes", "No");
		public static string OneZero(this bool value) =>
			value.ToString("1", "0");
		public static string OnOff(this bool value) =>
			value.ToString("On", "Off");
		public static OnOffState ToOnOffState(this bool value) =>
			value ? OnOffState.On : OnOffState.Off;
		public static SwitchState ToSwitchState(this bool value) =>
			value ? SwitchState.On : SwitchState.Off;
	}

	public static class IntExtensions
	{
		/// <summary>
		/// The least significant byte of an integer.
		/// </summary>
		/// <param name="i"></param>
		/// <returns></returns>
		public static byte Byte0(this int i) =>
			(byte)(i & 0xFF);

		/// <summary>
		/// The second-least significant byte of an integer. This is
		/// the most significant byte of a 16-bit value.
		/// </summary>
		/// <param name="i"></param>
		/// <returns></returns>
		public static byte Byte1(this int i) =>
			(byte)((i >> 8) & 0xFF);
	}

	public static class ByteExtensions
	{
		/// <summary>
		/// An eight character string representing the byte,
		/// e.g., "11001001" for the value 0xC9.
		/// </summary>
		/// <param name="b"></param>
		/// <returns></returns>
		public static string ToBinaryString(this byte b) =>
			Convert.ToString(b, 2).PadLeft(8, '0');
	}

	public static class DoubleExtensions
	{
		public static int ToInt(this double n) =>
			Convert.ToInt32(n);
	}

	public static class ActionExtensions
	{
		public static void AsyncInvoke(this Action action)
		{
			List<Task> all = new List<Task>();
			action.GetInvocationList().Cast<Action>().ToList().ForEach(
				a => all.Add(Task.Factory.StartNew(a, TaskCreationOptions.LongRunning)));
			Task.WhenAll(all.ToArray());
		}
	}
}