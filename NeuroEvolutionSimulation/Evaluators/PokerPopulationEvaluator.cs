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
    public class PokerPopulationEvaluator<PlayerType,EvaluatorType> : IPopulationEvaluator
        where PlayerType : ProbabilisticLimitNeuralNetPlayer, new()
        where EvaluatorType : IPlayerEvaluator, new()
    {
        #region Helper Classes
        private class ThreadInfo
        {
            public EvaluatorType Evaluator { get; set; }
            public List<IPlayer> Table { get; set; }
            public Thread EvalThread { get; set; }
        }
        private class Score
        {
            public double Winnings { get; set; }
            public int GamesPlayed { get; set; }
        }
        #endregion

        private Settings settings;
        private Random random;
        private Score[] scores;
        private List<ThreadInfo> threads;
        
        public PokerPopulationEvaluator(Settings settings)
        {
            this.settings = settings;
            this.random = new MersenneTwister();
        }

        public PokerPopulationEvaluator(Settings settings, Random random)
        {
            this.settings = settings;
            this.random = random;
        }

        public bool BestIsIntermediateChampion { get { return false; } }
        public string EvaluatorStateMessage { get { return "Current Game: " + GamesPlayed; } }
        public bool SearchCompleted { get { return false; } }
        public ulong EvaluationCount { get; private set; }
        public ulong GamesPlayed { get; set; }

        private int threadsToUse;
        public void EvaluatePopulation(Population pop, EvolutionAlgorithm ea)
        {
            List<IPlayer> allPlayers = CreatePlayers(pop);
            threads = new List<ThreadInfo>();

            //for (var i = 0; i < allPlayers.Count; i++)
            //{
            //    Console.WriteLine("Individual #{0}", i + 1);
            //    for (var games = 0; games < settings.GamesPerIndividual; games++)
            //    {
            //        if (threads.Count >= settings.Threads)
            //            WaitOne();
                    //List<IPlayer> table = allPlayers.RandomSubset(settings.PlayersPerGame - 1, random);
                    //table.Add(allPlayers[i]);
            threadsToUse = Settings.IsPeakHours() ? settings.PeakHoursThreads : settings.Threads;

            Console.WriteLine("Using {0} evaluation threads", threadsToUse);
            for (int i = 0; i < threadsToUse; i++)
            {
                EvaluatorType eval = new EvaluatorType();
                Thread t = new Thread(delegate() { eval.Evaluate(allPlayers, settings, false, settings.GamesPerIndividual / threadsToUse); });
                threads.Add(new ThreadInfo() { EvalThread = t, Evaluator = eval, Table = allPlayers });
                t.Start();
            }
            

            WaitAll();

            AssignFitness(pop);
            PlayChampions(allPlayers);

            EvaluationCount++;
        }

        

        private void WaitAll()
        {
            while (threads.Count > 0)
            {
                for (var i = 0; i < threads.Count; i++)
                    if (!threads[i].EvalThread.IsAlive)
                    {
                        AddScores(threads[i].Table, threads[i].Evaluator.Scores);
                        threads.RemoveAt(i);
                        return;
                    }

                Thread.Sleep(10);
            }
        }

        private void WaitOne()
        {
            while (threads.Count >= threadsToUse)
            {
                for (var i = 0; i < threads.Count; i++)
                    if (!threads[i].EvalThread.IsAlive)
                    {
                        AddScores(threads[i].Table, threads[i].Evaluator.Scores);
                        threads.RemoveAt(i);
                        return;
                    }

                Thread.Sleep(10);
            }
        }

        

        private List<IPlayer> CreatePlayers(Population pop)
        {
            resetGenomes(pop);

            var temp = new PlayerType();
            IActivationFunction activationFunction = temp.ActivationFunction;

            List<IPlayer> players = new List<IPlayer>();
            for (var i = 0; i < pop.GenomeList.Count; i++)
            {
                var g = pop.GenomeList[i];

                var network = g.Decode(activationFunction);
                g.Fitness = EvolutionAlgorithm.MIN_GENOME_FITNESS;
                if (network == null)
                {
                    // Future genomes may not decode - handle the possibility.
                    g.TotalFitness = g.Fitness;
                    g.EvaluationCount = 1;
                }
                else
                    players.Add(new PlayerType()
                    {
                        Network = network,
                        NetworkIndex = i,
                        ThreadSafe = true
                    });
            }

            return players;
        }

        //Resets the genomes to be evaluated this generation
        private void resetGenomes(Population pop)
        {
            var count = pop.GenomeList.Count;
            scores = new Score[count];

            for (var i = 0; i < count; i++)
            {
                pop.GenomeList[i].Fitness = EvolutionAlgorithm.MIN_GENOME_FITNESS;
                scores[i] = new Score();
            }
        }

        private void AddScores(List<IPlayer> table, List<double> roi)
        {
            for (int i = 0; i < table.Count; i++)
            {
                ProbabilisticLimitNeuralNetPlayer player = (ProbabilisticLimitNeuralNetPlayer)table[i];
                scores[player.NetworkIndex].Winnings += roi[i];
                scores[player.NetworkIndex].GamesPlayed++;
            }
        }

        private void AssignFitness(Population pop)
        {
            double min = 0;
            double max = 0;
            double[] winRates = new double[scores.Length];
            for (var i = 0; i < scores.Length; i++)
            {
                if (scores[i].GamesPlayed == 0)
                    continue;

                winRates[i] = scores[i].Winnings / scores[i].GamesPlayed;
                if (winRates[i] < min)
                    min = winRates[i];
                if (winRates[i] > max)
                    max = winRates[i];
            }
            Console.WriteLine("Max win rate: {0}", max);
            min = Math.Abs(min);
            for (var i = 0; i < scores.Length; i++)
            {
                if (winRates[i] == double.NaN)
                    throw new Exception("Win rate calculated wrong");

                pop.GenomeList[i].Fitness = Math.Max(winRates[i] + min, EvolutionAlgorithm.MIN_GENOME_FITNESS);
                pop.GenomeList[i].EvaluationCount++;
                pop.GenomeList[i].TotalFitness += pop.GenomeList[i].Fitness;
            }
        }

        private void PlayChampions(List<IPlayer> allPlayers)
        {
            List<IPlayer> champions = new List<IPlayer>();
            allPlayers.Sort(SortPlayersByWinrate);
            for (int i = 0; i < settings.PlayersPerGame; i++)
                champions.Add(allPlayers[i]);

            EvaluatorType eval = new EvaluatorType() { Generation = (int)EvaluationCount };
            eval.Evaluate(champions, settings, true, settings.GamesPerChampion);
        }

        private int SortPlayersByWinrate(IPlayer a, IPlayer b)
        {
            var p1 = a as ProbabilisticLimitNeuralNetPlayer;
            var p2 = b as ProbabilisticLimitNeuralNetPlayer;

            double winRate1 = scores[p1.NetworkIndex].Winnings / scores[p1.NetworkIndex].GamesPlayed;
            double winRate2 = scores[p2.NetworkIndex].Winnings / scores[p2.NetworkIndex].GamesPlayed;

            return winRate2.CompareTo(winRate1);
        }
    }
}
