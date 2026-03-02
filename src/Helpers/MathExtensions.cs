using System;

namespace WinTakMeshtasticPlugin.Helpers
{
    /// <summary>
    /// Math helper extensions for .NET Framework 4.8 compatibility.
    /// Math.Clamp is not available in .NET Framework.
    /// </summary>
    public static class MathExtensions
    {
        /// <summary>
        /// Clamps a value between a minimum and maximum value.
        /// </summary>
        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Clamps a value between a minimum and maximum value.
        /// </summary>
        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
