using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NeuroEvolutionSimulation.Players;
using FastPokerEngine;
using System.IO;

namespace NeuroEvolutionSimulation.Evaluators
{
    public class RingGamePlayerEvaluator : IPlayerEvaluator
    {
        const int MIN_STACK = 24;
        private static ulong GamesPlayed = 0;
        private static object LogLock = new object();

        private Random random;
        public List<double> Scores { get; set; }
        public int Generation { get; set; }

        public RingGamePlayerEvaluator()
        {
            random = new Random();
        }

        public void Evaluate(List<IPlayer> players, Settings settings, bool champions, int games)
        {
            ResetScores(players);
            int[] handsPlayed = new int[players.Count];
            List<int> availablePlayers = new List<int>();
            for (int i = 0; i < players.Count; i++)
                availablePlayers.Add(i);

            double startingChips = settings.BigBlind * settings.StackSize;

            HandEngine engine = new HandEngine() { AnalyzeHands = false };

            int handsThisGeneration = 0;
            while(availablePlayers.Count >= settings.PlayersPerGame)
            {
                ulong handNumber = GamesPlayed++;

                List<int> playerIndices = champions ? 
                                            CreateShuffledChampionsList(settings) :
                                            availablePlayers.RandomSubset(settings.PlayersPerGame, random);
                var seats = CreateSeats(players, startingChips, playerIndices);

                HandHistory history = new HandHistory(seats, GamesPlayed, (uint)settings.PlayersPerGame,
                                                        new double[] { settings.SmallBlind, settings.BigBlind },
                                                        0, BettingStructure.Limit);
                engine.PlayHand(history);
                handsThisGeneration++;

                if (champions)
                    LogHand(settings, history, handNumber > 1);

                if (handNumber % 100000 == 0)
                    lock(LogLock)
                        Console.WriteLine("Hand: {0}", handNumber);

                AddScores(startingChips, playerIndices, seats);
                IncrementHandsPlayedAndRemoveDone(games, handsPlayed, availablePlayers, playerIndices, champions);
            }

            //normalize win rates
            for (int i = 0; i < Scores.Count; i++)
            {
                Scores[i] /= (double)handsPlayed[i];
                Scores[i] /= settings.BigBlind;
                if (Scores[i] > 2)
                    Scores[i] = 2;
                else if (Scores[i] < -5)
                    Scores[i] = -5;
            }
        }

        private List<int> CreateShuffledChampionsList(Settings settings)
        {
            var results = new List<int>();
            for (int i = 0; i < settings.PlayersPerGame; i++)
                results.Add(i);
            results.Randomize(random);
            return results;
        }

        private static void IncrementHandsPlayedAndRemoveDone(int games, int[] handsPlayed, List<int> availablePlayers, List<int> playerIndices, bool champions)
        {
            foreach (int idx in playerIndices)
            {
                handsPlayed[idx]++;
                if (handsPlayed[idx] >= games)
                    availablePlayers.Remove(idx);
            }
        }

        private void AddScores(double startingChips, List<int> playerIndices, Seat[] seats)
        {
            for (int i = 0; i < playerIndices.Count; i++)
            {
                int playerIdx = playerIndices[i];
                double profit = seats[i].Chips - startingChips;
                Scores[playerIdx] += profit;

                if (Scores[playerIdx] < -5 * startingChips)
                    Scores[playerIdx] = -5000 * startingChips;
            }
        }

        private static Seat[] CreateSeats(List<IPlayer> players, double startingChips, List<int> playerIndices)
        {
            var seats = new Seat[playerIndices.Count];
            for (int i = 0; i < playerIndices.Count; i++)
                seats[i] = new Seat(i + 1, "Seat" + i + "_Network" + playerIndices[i],
                    startingChips, players[playerIndices[i]]);
            return seats;
        }


        private void LogHand(Settings settings, HandHistory history, bool append)
        {
            lock (LogLock)
                using (TextWriter writer = new StreamWriter(settings.LogFile + Generation + ".txt", append))
                {
                    writer.WriteLine(history.ToString());
                    writer.WriteLine();
                }
        }

        private void ResetScores(List<IPlayer> players)
        {
            Scores = new List<double>();
            for (int i = 0; i < players.Count; i++)
                Scores.Add(0);
        }

        
    }
}
