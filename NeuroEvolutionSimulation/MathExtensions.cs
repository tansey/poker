using System;
using SysMath = System.Math;

namespace NeuroEvolutionSimulation
{
    public static class MathExtensions
    {
        public static double Sqrt(this double d)
        {
            return SysMath.Sqrt(d);
        }

        public static double Cos(this double d)
        {
            return SysMath.Cos(d);
        }

        public static double Log(this double d)
        {
            return SysMath.Log(d);
        }

        public static double Log(this int i)
        {
            return SysMath.Log(i);
        }

        public static double Exp(this double d)
        {
            return SysMath.Exp(d);
        }

        public static double Pow(this double d, double power)
        {
            return SysMath.Pow(d, power);
        }

        public static double GaussianMutate(this Random rand, double mean, double stddev)
        {
            var x1 = rand.NextDouble();
            var x2 = rand.NextDouble();
            var y1 = (-2.0 * x1.Log()).Sqrt() * (2.0 * SysMath.PI * x2).Cos();
            
            return y1 * stddev + mean;
        }
    }
}
