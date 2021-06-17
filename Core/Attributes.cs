using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HACS.Core
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public abstract class HacsAttribute : Attribute { }

    public class PreConnectAttribute : HacsAttribute { }

    public class ConnectAttribute : HacsAttribute { }

    public class PostConnectAttribute : HacsAttribute { }

    public class PreInitializeAttribute : HacsAttribute { }

    public class InitializeAttribute : HacsAttribute { }

    public class PostInitializeAttribute : HacsAttribute { }

    public class PreStartAttribute : HacsAttribute { }

    public class StartAttribute : HacsAttribute { }

    public class PostStartAttribute : HacsAttribute { }

    public class PreStopAttribute : HacsAttribute { }

    public class StopAttribute : HacsAttribute { }

    public class PostStopAttribute : HacsAttribute { }
}
