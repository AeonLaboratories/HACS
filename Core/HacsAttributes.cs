using System;

namespace HACS.Core
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	public abstract class HacsAttribute : Attribute { public abstract Action Action { get; set; } }

	public class HacsPreConnectAttribute : HacsAttribute { public override Action Action { get => Hacs.OnPreConnect; set => Hacs.OnPreConnect = value; } }
	public class HacsConnectAttribute : HacsAttribute { public override Action Action { get => Hacs.OnConnect; set => Hacs.OnConnect = value; } }
	public class HacsPostConnectAttribute : HacsAttribute { public override Action Action { get => Hacs.OnPostConnect; set => Hacs.OnPostConnect = value; } }
	public class HacsPreInitializeAttribute : HacsAttribute { public override Action Action { get => Hacs.OnPreInitialize; set => Hacs.OnPreInitialize = value; } }
	public class HacsInitializeAttribute : HacsAttribute { public override Action Action { get => Hacs.OnInitialize; set => Hacs.OnInitialize = value; } }
	public class HacsPostInitializeAttribute : HacsAttribute { public override Action Action { get => Hacs.OnPostInitialize; set => Hacs.OnPostInitialize = value; } }
	public class HacsPreStartAttribute : HacsAttribute { public override Action Action { get => Hacs.OnPreStart; set => Hacs.OnPreStart = value; } }
	public class HacsStartAttribute : HacsAttribute { public override Action Action { get => Hacs.OnStart; set => Hacs.OnStart = value; } }
	public class HacsPostStartAttribute : HacsAttribute { public override Action Action { get => Hacs.OnPostStart; set => Hacs.OnPostStart = value; } }
	public class HacsPreUpdateAttribute : HacsAttribute { public override Action Action { get => Hacs.OnPreUpdate; set => Hacs.OnPreUpdate = value; } }
	public class HacsUpdateAttribute : HacsAttribute { public override Action Action { get => Hacs.OnUpdate; set => Hacs.OnUpdate = value; } }
	public class HacsPostUpdateAttribute : HacsAttribute { public override Action Action { get => Hacs.OnPostUpdate; set => Hacs.OnPostUpdate = value; } }
	public class HacsPreStopAttribute : HacsAttribute { public override Action Action { get => Hacs.OnPreStop; set => Hacs.OnPreStop = value; } }
	public class HacsStopAttribute : HacsAttribute { public override Action Action { get => Hacs.OnStop; set => Hacs.OnStop = value; } }
	public class HacsPostStopAttribute : HacsAttribute { public override Action Action { get => Hacs.OnPostStop; set => Hacs.OnPostStop = value; } }
}
