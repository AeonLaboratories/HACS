using HACS.Core;
using System;

namespace HACS.Components
{
    public class NamedValue : NamedObject, INamedValue
    {
        public double Value => GetValue();
        Func<double> GetValue { get; set; }
        public NamedValue(string name, Func<double> getValue)
        {
            Name = name;
            GetValue = getValue;
        }
    }
}
