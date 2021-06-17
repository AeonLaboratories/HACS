using HACS.Core;
using Newtonsoft.Json;

namespace HACS.Components
{
    public class Sensor : BindableObject, ISensor
    {
        [JsonProperty]
        public virtual double Value
        {
            get => value;
            protected set => Set(ref this.value, value);
        }
        double value;
    }
}
