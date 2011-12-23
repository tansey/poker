using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpNeatLib.Evolution;
using SharpNeatLib.NeuralNetwork;
using FastPokerEngine;
using NeuroEvolutionSimulation.Players;
using System.IO;
using System.Threading;

namespace NeuroEvolutionSimulation.Evaluators
{
    public class SimpleLimitPokerPopulationEvaluator<T> : IPopulationEvaluator 
           where T : ProbabilisticLimitNeuralNetPlayer, new()
    {
        private class ScoreInfo
        {
            public double Winnings { get; set; }
            public int TourneysPlayed { get; set; }
        }

        private ScoreInfo[] scores;
        private Random random;
        private List<CachedHand> cachedHands;

        /// <summary>
        /// The number of big blinds to start each hand with.
        /// </summary>
        public int StackSize { get; set; }
        public double SmallBlind { get; set; }
        public double BigBlind { get; set; }
        public int MaxHandsPerTourney { get; set; }
        public int MaxPlayersPerGame { get; set; }
        public int MinPlayersPerGame { get; set; }
        public int TourneysPerNetwork { get; set; }
        public ulong GamesPlayed { get; set; }
        public ulong EvaluationCount { get; set; }
        public IActivationFunction ActivationFunction { get; set; }
        public string LogFilename { get; set; }
        public bool AnalyzeHands { get; set; }
        public int CachedHandsPerGeneration { get; set; }
        public int Threads { get; set; }

        public SimpleLimitPokerPopulationEvaluator()
        {
            random = new MersenneTwister();
            initalize();
        }
        public SimpleLimitPokerPopulationEvaluator(Random rand)
        {
            random = rand;
            initalize();
        }

        private void initalize()
        {
            StackSize = 120;
            SmallBlind = 1;
            BigBlind = 2;
            MaxPlayersPerGame = 6;
            MinPlayersPerGame = 6;
            TourneysPerNetwork = 100;
            MaxHandsPerTourney = 200;
            ActivationFunction = new SteepenedSigmoid();
            LogFilename = "handlog.txt";
            AnalyzeHands = false;
            CachedHandsPerGeneration = 100000;
        }

        #region IPopulationEvaluator Members

        public bool BestIsIntermediateChampion
        {
            get { return false; }
        }

        public void EvaluatePopulation(Population pop, EvolutionAlgorithm ea)
        {
            resetGenomes(pop);

            if (AnalyzeHands)
            {
                Console.WriteLine("Caching {0} for this generation", CachedHandsPerGeneration);
                cachedHands = new List<CachedHand>();
                for (var i = 0; i < CachedHandsPerGeneration; i++)
                {
                    if (i % 10 == 0)
                        Console.WriteLine("{0} hands cached", i);
                    cachedHands.Add(new CachedHand(MaxPlayersPerGame, random));
                }
            }

            var count = pop.GenomeList.Count;
            
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

                for (int round = 0; round < TourneysPerNetwork; round++)
                {
                    //int playersThisHand = random.Next(MinPlayersPerGame, MaxPlayersPerGame + 1);
                    var field = getPlayers(pop, i, MaxPlayersPerGame);
                    var stacks = getStacks(MaxPlayersPerGame);

                    //List<double> roi = playRound(field, stacks);
                    var roi = playSemiTourney(field, stacks);
                    addScores(field, roi);
                }
            }

            //normalize the win rates to [0...max+min]
            assignFitness(pop);
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

        //Resets the genomes to be evaluated this generation
        private void resetGenomes(Population pop)
        {
            var count = pop.GenomeList.Count;
            scores = new ScoreInfo[count];

            for (var i = 0; i < count; i++)
            {
                pop.GenomeList[i].Fitness = EvolutionAlgorithm.MIN_GENOME_FITNESS;
                scores[i] = new ScoreInfo();
            }
        }

        //Everyone starts with the same stacks size
        private double[] getStacks(int players)
        {
            double[] result = new double[players];
            for (int i = 0; i < result.Length; i++)
                result[i] = BigBlind * StackSize;
            return result;
        }

        private List<IPlayer> getPlayers(Population pop, int heroIndex, int fieldSize)
        {
            var field = new List<IPlayer>();
            var count = pop.GenomeList.Count;

            for (var curPlayer = 0; curPlayer < fieldSize; curPlayer++)
            {
                INetwork nextNetwork = null;
                var next = 0;
                while (nextNetwork == null)
                {
                    next = curPlayer == 0 ? heroIndex : random.Next(0, count);
                    nextNetwork = pop.GenomeList[next].Decode(ActivationFunction);
                }
                ProbabilisticLimitNeuralNetPlayer villain = new T() {
                    Network = nextNetwork, 
                    NetworkIndex = next,
                    Rand = random };
                field.Add(villain);
            }
            return field;
        }

        private List<Seat> getSeats(List<IPlayer> players, double[] stacks)
        {
            var seats = new List<Seat>();
            for (int i = 0; i < players.Count; i++)
                seats.Add(new Seat(i + 1, "Seat" +i + "_Network" + ((ProbabilisticLimitNeuralNetPlayer)players[i]).NetworkIndex,
                    stacks[i], players[i]));
            return seats;
        }

        private List<double> playSemiTourney(List<IPlayer> players, double[] stacks)
        {
            List<double> roi = new List<double>();
            for (int i = 0; i < players.Count; i++)
                roi.Add(0);

            HandEngine engine = new HandEngine() { AnalyzeHands = false };
            var seats = getSeats(players, stacks);
            for (int i = 0, handsThisTourney = 0; 
                seats.Count > 3 && handsThisTourney < MaxHandsPerTourney;
                i = (i + 1) % seats.Count, GamesPlayed++, handsThisTourney++)
            {
                HandHistory history = new HandHistory(seats.ToArray(), GamesPlayed, (uint)seats[i].SeatNumber,
                                                        new double[] { SmallBlind, BigBlind },
                                                        0, BettingStructure.Limit);
                try
                {
                    if (AnalyzeHands)
                        engine.PlayHand(history, cachedHands[random.Next(cachedHands.Count)]);
                    else
                        engine.PlayHand(history);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                    using (TextWriter writer = new StreamWriter("error_log.txt", true))
                    {
                        writer.WriteLine(e.StackTrace);
                        writer.WriteLine();
                    }
                }

                if (GamesPlayed % 100000 == 0)
                {
                    using (TextWriter writer = new StreamWriter(LogFilename, GamesPlayed > 1))
                    {
                        writer.WriteLine(history.ToString());
                        writer.WriteLine();
                    }
                }

                //remove broke players
                for (var j = seats.Count - 1; j >= 0; j--)
                    if (seats[j].Chips < 24 * BigBlind)
                        seats.RemoveAt(j);
            }

            seats.Sort((a, b) => b.Chips.CompareTo(a.Chips));


            for (var i = 0; i < players.Count; i++)
            {
                for(var j = 0; j < seats.Count; j++)
                    if (seats[j].Brain == players[i])
                    {
                        if (j == 0)
                            roi[i] = 10;
                        else if (j == 1)
                            roi[i] = 7;
                        else if (j == 2)
                            roi[i] = 5;
                        else if (j == 3)
                            roi[i] = 3;
                        else if (j == 4)
                            roi[i] = 2;
                        break;

                        //if you made it this far, you don't lose more than 20 big blinds
                        //if you did really well though, you can win as much as you can take
                        //roi[i] = Math.Max(-20 * BigBlind, seats[j].Chips - stacks[i]);
                    }
            }

            return roi;
        }

        private List<double> playRound(List<IPlayer> players, double[] stacks)
        {
            List<double> roi = new List<double>();
            for (int i = 0; i < players.Count; i++)
                roi.Add(0);

            HandEngine engine = new HandEngine() { AnalyzeHands = false };

            //bool[] stillIn = new bool[stacks.Length];
            //for (int i = 0; i < stillIn.Length; i++)
            //    stillIn[i] = true;

            //Play one hand in every position
            for (int i = 0; i < players.Count; i++)
            {
                var seats = getSeats(players, stacks);
                //for (int j = seats.Count() - 1; j >= 0; j--)
                //    if (!stillIn[j])
                //        seats.RemoveAt(j);

                //Have the players play a single hand.
                HandHistory history = new HandHistory(seats.ToArray(), GamesPlayed, (uint)seats[i].SeatNumber,
                                                        new double[] { SmallBlind, BigBlind },
                                                        0, BettingStructure.Limit);
                engine.PlayHand(history);
                GamesPlayed++;

                if (GamesPlayed % 100000 == 0)
                {
                    using (TextWriter writer = new StreamWriter(LogFilename, GamesPlayed > 1))
                    {
                        writer.WriteLine(history.ToString());
                        writer.WriteLine();
                    }

                    for (int j = 0; j < stacks.Length; j++)
                        Console.WriteLine("Reward {0}: {1}", seats[j].Name, seats[j].Chips - stacks[j]);
                }

                for (int j = 0; j < stacks.Length; j++)
                    roi[j] += seats[j].Chips - stacks[j];
            }

            return roi;
        }

        private void addScores(List<IPlayer> players, List<double> roi)
        {
            for (int i = 0; i < players.Count; i++)
            {
                ProbabilisticLimitNeuralNetPlayer player = (ProbabilisticLimitNeuralNetPlayer)players[i];
                scores[player.NetworkIndex].Winnings += roi[i];
                scores[player.NetworkIndex].TourneysPlayed++;
            }
        }

        private void assignFitness(Population pop)
        {
            double min = 0;
            double[] winRates = new double[scores.Length];
            for (var i = 0; i < scores.Length; i++)
            {
                winRates[i] = scores[i].Winnings / scores[i].TourneysPlayed;
                if (winRates[i] < min)
                    min = winRates[i];
            }
            if (min < 0)
                min = Math.Abs(min);
            for (var i = 0; i < scores.Length; i++)
            {
                pop.GenomeList[i].Fitness = Math.Max(winRates[i] + min, EvolutionAlgorithm.MIN_GENOME_FITNESS);
                pop.GenomeList[i].EvaluationCount++;
                pop.GenomeList[i].TotalFitness += pop.GenomeList[i].Fitness;
            }
        }

    }
}
