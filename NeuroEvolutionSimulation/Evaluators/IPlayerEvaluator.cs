using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastPokerEngine;

namespace NeuroEvolutionSimulation.Evaluators
{
    public interface IPlayerEvaluator
    {
        List<double> Scores { get; set; }
        int Generation { get; set; }
        void Evaluate(List<IPlayer> players, Settings settings, bool champions, int games);
    }
}
