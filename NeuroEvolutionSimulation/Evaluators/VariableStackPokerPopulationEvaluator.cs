using System;
using System.Collections.Generic;
using SharpNeatLib.Evolution;
using SharpNeatLib.NeuralNetwork;
using FastPokerEngine;
using System.IO;

namespace NeuroEvolutionSimulation.Evaluators
{
    public class VariableStackPokerPopulationEvaluator : IPopulationEvaluator
    {
        private const double MAX_HUGE_STACK = 700;
        private const double MAX_LARGE_STACK = 200;
        private const double MAX_MEDIUM_STACK = 105;
        private const double MAX_SMALL_STACK = 65;
        private const double MEAN_FOR_MEDIUM_STACK = 100;
        private const double MEAN_FOR_SMALL_STACK = 35;
        private const double MIN_STACK = 1;
        private const double PROBABILITY_OF_LARGE_STACK = 0.3708;
        private const double PROBABILITY_OF_MEDIUM_STACK = 0.2617;
        private const double PROBABILITY_OF_SMALL_STACK = 0.3002;

        public int PlayersPerGame { get; set; }
        public ulong GamesPlayed { get; set; }
        public int GamesPerEvaluation { get; set; }
        public List<CachedHand> CachedHands { get; set; }
        public IActivationFunction ActivationFunction { get; set; }
        public Random Rand { get; set; }
        public BettingStructure BettingType { get; set; }

        #region IPopulationEvaluator Members

        public bool BestIsIntermediateChampion
        {
            get { return false; }
        }

        public void EvaluatePopulation(Population pop, EvolutionAlgorithm ea)
        {
            var count = pop.GenomeList.Count;

            #region Reset the genomes

            for (var i = 0; i < count; i++)
            {
                pop.GenomeList[i].TotalFitness = EvolutionAlgorithm.MIN_GENOME_FITNESS;
                pop.GenomeList[i].EvaluationCount = 0;
                pop.GenomeList[i].Fitness = EvolutionAlgorithm.MIN_GENOME_FITNESS;
            }

            #endregion

            //TODO: Parallelize/Distribute this loop
            //Ideally we should have a distributed method which returns an array of
            //doubles to add to the genome fitnesses of each individual.
            for (var i = 0; i < count; i++)
            {
                Console.WriteLine("Individual #{0}", i + 1);
                var g = pop.GenomeList[i];

                var network = g.Decode(ActivationFunction);
                if (network == null)
                {
                    // Future genomes may not decode - handle the possibility.
                    g.Fitness = EvolutionAlgorithm.MIN_GENOME_FITNESS;
                    g.TotalFitness = g.Fitness;
                    g.EvaluationCount = 1;
                    continue;
                }

                HandEngine engine = new HandEngine();
                //Run multiple hands per individual
                for (var curGame = 0; curGame < GamesPerEvaluation; curGame++)
                {
                    #region Setup the players for this game

                    var field = new List<Seat>();
                    var stacks = GetStacks(PlayersPerGame);
                    var networks = new int[PlayersPerGame];
                    networks[0] = i;
                    IPlayer hero = null;//new NeuralNetworkPlayer(InputGenerator, OutputInterpreter,
                                   //                        network, Rand);
                    field.Add(new Seat(1, "Net_" + i, stacks[0], hero));

                    for (var curPlayer = 1; curPlayer < PlayersPerGame; curPlayer++)
                    {
                        INetwork nextNetwork = null;
                        var next = 0;
                        while (nextNetwork == null)
                        {
                            next = Rand.Next(0, count);
                            nextNetwork = pop.GenomeList[next].Decode(ActivationFunction);
                        }
                        networks[curPlayer] = next;
                        //"NeuralNet" + next, stacks[curPlayer],
                        IPlayer villain = null;// new NeuralNetworkPlayer(InputGenerator,
                                                 //          OutputInterpreter, nextNetwork, Rand);
                        field.Add(new Seat(curPlayer + 1, "Net" + next + "_Seat+ " + (curPlayer+1), stacks[curPlayer], villain));
                    }

                    #endregion

                    //Have the players play a single hand.
                    HandHistory history = new HandHistory(field.ToArray(), (ulong)curGame+1, (uint)(curGame % PlayersPerGame + 1), 
                                                            new double[] { 1, 2 }, 0, BettingType);
                    CachedHand hand = CachedHands[Rand.Next(CachedHands.Count)];
                    engine.PlayHand(history);

                    #region Add the results to the players' fitness scores
                    
                    //We'll use the profit as the fitness function.
                    //Alternatively, we could in the future experiment with using profit
                    //as a percentage of the original stacks. Or we could use the square
                    //of the profit (multiplying by -1 if the player lost money).
                    for (var curResult = 0; curResult < PlayersPerGame; curResult++)
                    {
                        var curGenome = pop.GenomeList[networks[curResult]];
                        curGenome.TotalFitness += field[curResult].Chips - stacks[curResult];
                        curGenome.EvaluationCount++;
                    }

                    #endregion

                    if (GamesPlayed % 10000 == 0)
                        using (TextWriter writer = new StreamWriter("game_" + GamesPlayed + ".txt"))
                            writer.WriteLine(history.ToString());

                    //increment the game counter
                    GamesPlayed++;
                }

                
            }

            //Normalize the fitness scores to use the win-rate
            for (var i = 0; i < count; i++)
            {
                pop.GenomeList[i].Fitness = Math.Max(pop.GenomeList[i].Fitness, 
                                                     EvolutionAlgorithm.MIN_GENOME_FITNESS);
                pop.GenomeList[i].TotalFitness = Math.Max(pop.GenomeList[i].Fitness,
                                                     EvolutionAlgorithm.MIN_GENOME_FITNESS);
            }
        }


        public ulong EvaluationCount
        {
            get { return GamesPlayed * (ulong) PlayersPerGame; }
        }

        public string EvaluatorStateMessage
        {
            get { return "Current Game: " + GamesPlayed; }
        }

        public bool SearchCompleted
        {
            get { return false; }
        }

        #endregion

        private double[] GetStacks(int numStacks)
        {
            var result = new double[numStacks];
            for (var i = 0; i < result.Length; i++)
            {
                //result[i] = NextStack();
                result[i] = 10000;//TODO: change back
            }
            return result;
        }

        private double NextStack()
        {
            var distribution = Rand.NextDouble();
            var stack = double.MaxValue;

            if (distribution <= PROBABILITY_OF_SMALL_STACK)
            {
                while (stack > MAX_SMALL_STACK || stack < MIN_STACK)
                {
                    stack = Rand.GaussianMutate(MEAN_FOR_SMALL_STACK, MAX_SMALL_STACK - MEAN_FOR_SMALL_STACK);
                }
            }
            else if (distribution <= PROBABILITY_OF_MEDIUM_STACK + PROBABILITY_OF_SMALL_STACK)
            {
                while (stack > MAX_MEDIUM_STACK || stack < MAX_SMALL_STACK)
                {
                    stack = Rand.GaussianMutate(MEAN_FOR_MEDIUM_STACK, MEAN_FOR_MEDIUM_STACK - MAX_SMALL_STACK);
                }
            }
            else if (distribution <=
                     PROBABILITY_OF_LARGE_STACK + PROBABILITY_OF_MEDIUM_STACK + PROBABILITY_OF_SMALL_STACK)
            {
                while (stack > MAX_LARGE_STACK || stack < MAX_MEDIUM_STACK)
                {
                    stack = MAX_MEDIUM_STACK * (Rand.NextDouble() * 2.Log()).Exp();
                }
            }
            else
            {
                while (stack > MAX_HUGE_STACK || stack < MAX_MEDIUM_STACK)
                {
                    stack = MAX_LARGE_STACK * (Rand.NextDouble() * 6.Log()).Exp();
                }
            }
            return stack;
        }
    }
}