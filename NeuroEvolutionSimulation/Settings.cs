using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NeuroEvolutionSimulation
{
    public class Settings
    {
        public int StackSize { get; set; }
        public double SmallBlind { get; set; }
        public double BigBlind { get; set; }
        public int PlayersPerGame { get; set; }
        public int GamesPerIndividual { get; set; }
        public int GamesPerChampion { get; set; }
        public int Threads { get; set; }
        public int PeakHoursThreads { get; set; }
        public string LogFile { get; set; }
        public int MaxHandsPerTourney { get; set; }

        public static bool IsPeakHours()
        {
            return DateTime.Now.TimeOfDay.Hours > 15 ||
               DateTime.Now.TimeOfDay.Hours == 6 ||
               DateTime.Now.TimeOfDay.Hours == 7;
        }
    }
}
