using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace HACS.Core
{
	[JsonObject(MemberSerialization.OptIn)]
	public class NamedObject : BindableObject, INamedObject
	{
		static readonly Dictionary<string, INamedObject> dict = new Dictionary<string, INamedObject>();

		static readonly Dictionary<Type, IList> cachedLists = new Dictionary<Type, IList>();

		static object dictionaryLocker = new object();

		/// <summary>
		/// Returns default if name is invalid or the named object is not the specified type.
		/// Throws KeyNotFoundException if name is valid but not present in dictionary.
		/// </summary>
		public static T Find<T>(string name) where T : class
		{
			if (string.IsNullOrWhiteSpace(name))
				return default;

			INamedObject entry = null;

			lock(dictionaryLocker)
				if (!dict.TryGetValue(name, out entry))
					return default;

			if (entry is NamedObjectList list)
				return (T)list.FirstOrDefault(item => item is T);
			if (entry is T obj)
				return obj;

			return default;
		}

		public static List<INamedObject> FindAll(string name)
		{
			var result = new List<INamedObject>();
			lock (dictionaryLocker)
			{
				if (dict.TryGetValue(name, out INamedObject item))
				{
					if (item is NamedObjectList list)
						result.AddRange(list);
					else
						result.Add(item);
				}
			}
			return result;
		}

		public static List<T> FindAll<T>() where T : INamedObject
		{
			var list = new List<T>();
			lock (dictionaryLocker)
			{
				foreach (var obj in dict.Values)
				{
					if (obj is NamedObjectList l)
						l.ForEach(item => { if (item is T li) list.Add(li); });
					else if (obj is T i)
						list.Add(i);
				}
			}
			return list;
		}

		public static List<T> CachedList<T>() where T : INamedObject
		{
			var type = typeof(T);
			List<T> result;

			if (!cachedLists.ContainsKey(type))
				result = (List<T>)(cachedLists[type] = FindAll<T>());
			else
				result = (List<T>)cachedLists[type];

			return result;
		}

		private static void addCached(INamedObject o)
		{
			if (o?.GetType() is Type t)
			{
				if (!cachedLists.ContainsKey(t))
					return;
				cachedLists[t].Add(o);
			}
		}

		private static void removeCached(INamedObject o)
		{
			if (o?.GetType() is Type t)
			{
				if (!cachedLists.ContainsKey(t))
					return;
				cachedLists[t].Remove(o);
			}
		}

		/// <summary>
		/// Returns null if names is null.
		/// Throws an exception if any name is valid but not present in dictionary.
		/// </summary>
		public static List<T> FindAll<T>(List<string> names) where T : class
		{
			if (names is null)
				return default;
			var list = new List<T>();
			names.ForEach(name => list.Add(Find<T>(name)));
			return list;
		}

		public static List<T> FindAll<T>(Predicate<T> match) where T : INamedObject => FindAll<T>().FindAll(match);

		public static T FirstOrDefault<T>() where T : INamedObject => FindAll<T>().FirstOrDefault();

		public static T FirstOrDefault<T>(Func<T, bool> match) where T : INamedObject => FindAll<T>().FirstOrDefault(match);

		private static void remove(INamedObject o)
		{
			var name = o?.Name;
			if (string.IsNullOrWhiteSpace(name))
				return;
			lock (dictionaryLocker)
			{
				if (dict.ContainsKey(name))
				{
					if (dict[name] == o)
						dict.Remove(name);
					else if (dict[name] is NamedObjectList list)
					{
						list.Remove(o);
						if (list.Count == 1)
							dict[name] = list[0];
					}
				}
			}
		}

		private static void add(INamedObject o)
		{
			var name = o?.Name;
			if (string.IsNullOrWhiteSpace(name))
			{
				removeCached(o);
				return;
			}

			lock (dictionaryLocker)
			{
				if (!dict.ContainsKey(name))
				{
					dict[name] = o;
					addCached(o);
				}
				else
				{
					var o0 = dict[name];
					if (o0 != o)
					{
						if (o0.GetType() == o.GetType())
							return;

						if (o0 is NamedObjectList list)
						{
							if (list.FirstOrDefault(x => x.GetType() == o.GetType()) != null)
								return;
						}
						else
							list = new NamedObjectList(o0);

						list.Add(o);
						dict[name] = list;
						addCached(o);
					}
				}
			}
		}

		[JsonProperty(Order = -99)]
		public virtual string Name
		{
			get => name;
			set
			{
				remove(this);
				Set(ref name, value);
				add(this);
			}
		}
		string name;

		~NamedObject() => remove(this);

		public override string ToString() => Name;

		internal class NamedObjectList : List<INamedObject>, INamedObject
		{

			public string Name
			{ 
				get => name;
				set { name = value; NotifyPropertyChanged(); }
			}
			string name;

			public NamedObjectList(INamedObject o)
			{
				Name = o.Name;
				Add(o);
			}

			public event PropertyChangedEventHandler PropertyChanged;
			protected virtual void NotifyPropertyChanged([CallerMemberName] string propertyName = "") =>
				NotifyPropertyChanged(this, PropertyChangedEventArgs(propertyName));
			protected virtual void NotifyPropertyChanged(object sender, PropertyChangedEventArgs e) =>
				PropertyChanged?.Invoke(sender, e);

		}
	}
}
