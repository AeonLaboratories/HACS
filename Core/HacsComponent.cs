using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reflection;

namespace HACS.Core
{
	[JsonObject(MemberSerialization.OptIn)]
	public class HacsComponent : NamedObject, IHacsComponent
	{
        public virtual bool Connected => Hacs.Connected;
        public virtual bool Initialized => Hacs.Initialized;
        public virtual bool Started => Hacs.Started;
        public virtual bool Stopped => Hacs.Stopped;

		public HacsComponent()
		{
			#region Subscribe HacsActions
			GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance)
				?.Where(method => method.IsDefined(typeof(HacsAttribute), true))?.ToList()?.ForEach(method =>
					Array.ForEach((HacsAttribute[])Attribute.GetCustomAttributes(method, typeof(HacsAttribute)), a =>
						a.Action += (Action)Delegate.CreateDelegate(typeof(Action), this, method)));
			#endregion Subscribe HacsActions
		}

		public override string ToString() { return $"{Name}"; }
    }
}
