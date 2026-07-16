using System;
using System.Globalization;

namespace App2
{
    public static class MetricDisplayHelper
    {
        private const int ManThreshold = 10_000;

        public static string Format(int count)
        {
            if (count < 0)
                count = 0;

            if (count < ManThreshold)
                return count.ToString("#,0", CultureInfo.InvariantCulture);

            var man = count / (double)ManThreshold;
            var rounded = Math.Round(man, 1, MidpointRounding.AwayFromZero);

            if (Math.Abs(rounded - Math.Truncate(rounded)) < 0.05)
                return ((long)rounded).ToString(CultureInfo.InvariantCulture) + "万";

            return rounded.ToString("0.0", CultureInfo.InvariantCulture) + "万";
        }
    }
}
