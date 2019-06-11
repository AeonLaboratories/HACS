using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HACS.Core
{
	public interface INamedObject
	{
		string Name { get; set; }
	}

    public interface IOperatable : IHacsComponent
    {
        List<string> Operations { get; }
        void DoOperation(string operation);
    }

    public interface IOnOff : IHacsComponent
    {
        bool IsOn { get; }
        void TurnOn();
        void TurnOff();
    }

    public interface IValue : IHacsComponent
    {
        double Value { get; }
    }

    public interface ISetpoint : IHacsComponent
    {
        double Setpoint { get; set; }
    }

    public interface IThermometer : IHacsComponent
    {
        double Temperature { get; }
    }

    public interface IHeater : IHacsComponent, IOnOff
    {
        double PowerLevel { get; set; }
    }

	public interface IHacsUI
	{
		void Close();
	}
}
