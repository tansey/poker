using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpNeatLib.NeuralNetwork;
using FastPokerEngine;

namespace NeuroEvolutionSimulation.Players
{
    class SimpleLimitNeuralNetPlayer2: ProbabilisticLimitNeuralNetPlayer
    {
        private static object DebugLock = new object();
        public const int INPUT_NODE_COUNT = 17;
        public override IActivationFunction ActivationFunction { get; set; }
        public SimpleLimitNeuralNetPlayer2()
            : base()
        {
            ActivationFunction = new BipolarSigmoid();
        }
        public SimpleLimitNeuralNetPlayer2(INetwork network, int index)
            : base(network, index)
        {
            ActivationFunction = new BipolarSigmoid();
        }

        public SimpleLimitNeuralNetPlayer2(INetwork network, int index, Random random)
            : base(network, index)
        {
            ActivationFunction = new BipolarSigmoid();
        }


        public override double[] GetInputs(HandHistory history)
        {
            double[] inputs = new double[INPUT_NODE_COUNT];

            int index = 0;
            index = Util.AddPlayersAsSeparateNodes(history, inputs, index);//6 nodes
            index = Util.AddRoundAsSingleNode(history, inputs, index);//1 node
            index = Util.AddBetsDecisionInfoAsSingleNodes(history, inputs, index);//3 nodes
            index = Util.AddPreviousRoundBetInfo(history, inputs, index);//2 nodes
            index = Util.AddCurrentAndPreviousProbabilities(history, inputs, index);//5 nodes

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
