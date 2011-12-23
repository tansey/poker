using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeuroEvolutionSimulation.Evaluators
{
    public static class ListExtensions
    {
        public static List<T> RandomSubset<T>(this List<T> list, int count, Random random)
        {
            List<T> sublist = new List<T>();
            for (int i = 0; i < count; i++)
                sublist.Add(list[random.Next(list.Count)]);
            return sublist;
        }

        public static void Randomize<T>(this List<T> list, Random random)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int swap = random.Next(list.Count);
                T temp = list[i];
                list[i] = list[swap];
                list[swap] = temp;
            }
        }
    }
}
