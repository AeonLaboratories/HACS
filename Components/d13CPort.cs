using static HACS.Components.CegsPreferences;

namespace HACS.Components
{
    public class d13CPort : LinePort, Id13CPort
    {
        string mass
        {
            get
            {
                var ugC = Aliquot.Sample.Micrograms_d13C;
                var umolC = ugC / GramsCarbonPerMole;
                return $" {ugC:0.0} µgC = {umolC:0.00} µmol";
            }
        }
        public override string Contents
        {
            get
            {
                if (Aliquot?.Sample?.LabId is string contents)
                    return contents + mass;
                return "";
            }
        }
    }
}
