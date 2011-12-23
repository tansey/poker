using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpNeatLib.Evolution;
using NeuroEvolutionSimulation.Players;
using FastPokerEngine;

namespace NeuroEvolutionSimulation.Evaluators
{
    public class SupervisedLimitEvaluator<T>
    {
        private Random random;

        public SupervisedLimitEvaluator()
        {
            random = new MersenneTwister();
        }

        public bool BestIsIntermediateChampion
        {
            get { return false; }
        }
    }
}
