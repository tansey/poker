using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastPokerEngine;
using SharpNeatLib.NeuralNetwork;
using KeithRuleHand = HoldemHand.Hand;
using PokerAction = FastPokerEngine.Action;

namespace NeuroEvolutionSimulation.Players
{
    public class SimpleLimitNeuralNetPlayer : ProbabilisticLimitNeuralNetPlayer
    {
        private static object DebugLock = new object();
        public const int INPUT_NODE_COUNT = 11;
        public override IActivationFunction ActivationFunction { get; set; }
        public SimpleLimitNeuralNetPlayer()
            : base()
        {
            ActivationFunction = new SteepenedSigmoid();
        }
        public SimpleLimitNeuralNetPlayer(INetwork network, int index)
            : base(network, index)
        {
            ActivationFunction = new SteepenedSigmoid();
        }

        public SimpleLimitNeuralNetPlayer(INetwork network, int index, Random random)
            : base(network, index)
        {
            ActivationFunction = new SteepenedSigmoid();
        }


        public override double[] GetInputs(HandHistory history)
        {
            double[] inputs = new double[INPUT_NODE_COUNT];

            int index = 0;
            index = Util.AddPlayersAsSingleNode(history, inputs, index);//1 node
            index = Util.AddRoundAsSingleNode(history, inputs, index);//1 node
            //index = Util.AddCardsAsSingleNodes(history, inputs, index);//14 nodes
            //index = Util.AddBetsAsSingleNodes(history, 6, inputs, index);//16 nodes
            index = Util.AddBetsDecisionInfoAndPositionAsSingleNodes(history, inputs, index);//5 nodes
            index = Util.AddProbabilitiesAsSingleNodes(history, inputs, index);//4 nodes
            //index = Util.AddAnalysisAsSingleNodes(history, inputs, index);//10 nodes

            //lock (DebugLock)
            //{
            //    debugPrint(history, inputs);
            //    Console.ReadKey(false);
            //}
            if (index != INPUT_NODE_COUNT)
                throw new Exception("Wrong: " + index);

            return inputs;
        }

        private void debugPrint(HandHistory history, double[] inputs)
        {
            Console.WriteLine();
            Console.WriteLine(history.ToString());
            Console.WriteLine();
            Console.WriteLine("HoleCards: {0}",
                                HoldemHand.Hand.MaskToString(history.HoleCards[history.Hero]));
            Console.WriteLine("INPUTS:");
            for (var i = 0; i < inputs.Length; i++)
                Console.WriteLine("{0}: {1}", i, inputs[i]);

            Console.WriteLine();
        }
    }
}
