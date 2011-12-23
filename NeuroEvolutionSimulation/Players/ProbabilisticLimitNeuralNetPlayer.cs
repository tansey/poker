using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastPokerEngine;
using SharpNeatLib.NeuralNetwork;

namespace NeuroEvolutionSimulation.Players
{
    public abstract class ProbabilisticLimitNeuralNetPlayer : IPlayer
    {
        public int NetworkIndex { get; set; }
        public INetwork Network { get; set; }
        public Random Rand { get; set; }
        public bool ThreadSafe { get; set; }
        public abstract IActivationFunction ActivationFunction { get; set; }

        public ProbabilisticLimitNeuralNetPlayer()
        {
            Rand = new MersenneTwister();
        }
        public ProbabilisticLimitNeuralNetPlayer(INetwork network, int index)
        {
            Network = network;
            NetworkIndex = index;
            Rand = new MersenneTwister();
        }

        public ProbabilisticLimitNeuralNetPlayer(INetwork network, int index, Random random)
        {
            Network = network;
            NetworkIndex = index;
            Rand = random;
        }
        public abstract double[] GetInputs(HandHistory history);

        public void GetAction(HandHistory history, out FastPokerEngine.Action.ActionTypes action, out double amount)
        {
            double[] inputs = GetInputs(history);
            if (ThreadSafe)
            {
                lock (Network)
                    RunNetwork(inputs, out action, out amount);
            }
            else
                RunNetwork(inputs, out action, out amount);
        }

        public void RunNetwork(double[] inputs, out FastPokerEngine.Action.ActionTypes action, out double amount)
        {
            Network.ClearSignals();
            Network.SetInputSignals(inputs);
            Network.RelaxNetwork(10, 0.001);

            action = getAction(Network.GetOutputSignal(0),
                               Network.GetOutputSignal(1),
                               Network.GetOutputSignal(2));
            amount = 0;
        }


        private FastPokerEngine.Action.ActionTypes getAction(double foldProb, double callProb, double raiseProb)
        {
            double sum = foldProb + callProb + raiseProb;
            foldProb /= sum;
            callProb /= sum;
            raiseProb /= sum;
            double val = Rand.NextDouble();
            if (val < foldProb)
                return FastPokerEngine.Action.ActionTypes.Fold;
            if (val < foldProb + callProb)
                return FastPokerEngine.Action.ActionTypes.Call;
            return FastPokerEngine.Action.ActionTypes.Raise;
        }
    }
}
