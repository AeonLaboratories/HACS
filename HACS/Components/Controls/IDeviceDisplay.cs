using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HACS.Components.Controls
{
	public interface IDeviceDisplay
	{
		object Device { get; set; }
		void StateChanged();
	}
}
