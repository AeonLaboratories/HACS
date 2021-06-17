using HACS.Core;
using System;
using System.Text;

namespace HACS.Components
{
    public class IntEncodableDouble : BindableObject
    {
        public static bool operator ==(IntEncodableDouble n, IntEncodableDouble m)
            => n.Precision == m.Precision && n.IntValue == m.IntValue;
        public static bool operator !=(IntEncodableDouble n, IntEncodableDouble m)
            => n.Precision != m.Precision || n.IntValue != m.IntValue;

        public static bool Equals(IntEncodableDouble n, IntEncodableDouble m) =>
            n == m;

        public static bool Equals(IntEncodableDouble n, int i) =>
            n.IntValue == i;

        public static bool Equals(IntEncodableDouble n, double d) =>
            Equals(n, new IntEncodableDouble(d, n.Precision));

        public static implicit operator double(IntEncodableDouble x) => x.DoubleValue;
        public static implicit operator int(IntEncodableDouble x) => x.IntValue;

        public static int EncodeAsInt(double d, double multipler) => (d * multipler).ToInt();
        public static double DecodeAsDouble(int i, double multipler) => i / multipler;

        public double DoubleValue
        {
            get => doubleValue;
            set
            {
                if (value == doubleValue) return;
                doubleValue = value;
                intValue = EncodeAsInt(doubleValue, multiplier);
                NotifyPropertyChanged();
            }
        }
        double doubleValue;

        public int IntValue
        {
            get => intValue;
            set
            {
                if (value == intValue) return;
                intValue = value;
                doubleValue = DecodeAsDouble(intValue, multiplier);
                NotifyPropertyChanged();
            }
        }
        int intValue;


        /// <summary>
        /// The number [0..15] of fractional digits of the double 
        /// representation to be encoded as whole numbers in the integer 
        /// representation. For example, if Precision = 3, 6.01264 would be 
        /// encoded as the integer 6013.</param>
        /// </summary>
        public int Precision
        {
            get => precision;
            set
            {
                if (value < 0 || value > 15)
                    throw new ArgumentOutOfRangeException("Precision");
                precision = value;
                multiplier = Math.Pow(10, precision);
            }
        }
        int precision = 0;
        double multiplier = 1.0;

        public override bool Equals(object obj)
        {
            //Check for null and compare run-time types.
            if (obj is IntEncodableDouble n)
                return Equals(this, n);
            if (obj is double d)
                return Equals(this, d);
            if (obj is int i)
                return Equals(this, i);
            return false;
        }
        
        /// <summary>
        /// Creates a new instance of value 0 with the default precision of 0;
        /// </summary>
        public IntEncodableDouble() { }

        /// <summary>
        /// Creates a new instance of value 0 with the specified precision.
        /// </summary>
        /// <param name="precision">The number [0..15] of fractional digits of
        /// the double representation to be encoded as whole numbers in the integer 
        /// representation. For example, if precision = 3, 6.01264 would be 
        /// encoded as the integer 6013.</param>
        public IntEncodableDouble(int precision)
        { Precision = precision; }

        /// <summary>
        /// Creates a new instance with the specified value and the
        /// default precision of 0.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        public IntEncodableDouble(double value)
        { DoubleValue = value; }

        /// <summary>
        /// Creates a new instance with the specified value and precision.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="precision"></param>
        public IntEncodableDouble(double value, int precision)
        { Precision = precision; DoubleValue = value; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{DoubleValue}, Precision = {Precision}, Encoded = {IntValue}");
            return sb.ToString();
        }

        // TODO: replace with System.HashCode technique once this
        // project can target .NET Core
        public override int GetHashCode()
        {
            int hashCode = -1473892434;
            hashCode = hashCode * -1521134295 + IntValue.GetHashCode();
            hashCode = hashCode * -1521134295 + Precision.GetHashCode();
            return hashCode;
        }
    }
}
