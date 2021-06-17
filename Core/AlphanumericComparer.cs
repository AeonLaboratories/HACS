using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace HACS.Core
{
    public class AlphanumericComparer : IComparer<string>
    {
        const string numeral = "0123456789";

        // TODO extension method?
        protected void AlphanumericSplit(string input, out string alpha, out string numeric)
        {
            if (input is null)
            {
                alpha = null;
                numeric = null;
                return;
            }

            var splitPoint = input.LastIndexOf(input.LastOrDefault(c => !numeral.Contains(c))) + 1;
            if (splitPoint != 0)
            {
                if (splitPoint == input.Length)
                {
                    alpha = input;
                    numeric = "0";
                }
                else
                {
                    alpha = input.Substring(0, splitPoint);
                    numeric = input.Substring(splitPoint, input.Length - splitPoint);
                }
            }
            else
            {
                alpha = "";
                numeric = input;
            }
        }

        public int Compare([AllowNull] string x, [AllowNull] string y)
        {
            if (x == y)
                return string.Compare(x, y);

            AlphanumericSplit(x, out string alphaX, out string numericX);
            AlphanumericSplit(y, out string alphaY, out string numericY);

            if (string.Compare(alphaX, alphaY) == 0)
                return int.Parse(numericX) - int.Parse(numericY);
            return string.Compare(x, y);
        }
    }
}
