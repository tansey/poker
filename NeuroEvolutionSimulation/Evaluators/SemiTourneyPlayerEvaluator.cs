using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastPokerEngine;
using NeuroEvolutionSimulation.Players;
using System.IO;

namespace NeuroEvolutionSimulation.Evaluators
{
    public class SemiTourneyPlayerEvaluator : IPlayerEvaluator
    {
        const int MIN_STACK = 24;
        private static ulong GamesPlayed = 0;
        private static object LogLock = new object();
        public List<double> Scores { get; set; }
        public int Generation { get; set; }

        public void Evaluate(List<IPlayer> players, Settings settings, bool champions, int games)
        {
            ResetScores(players);
            
            var seats = CreateSeats(players, settings.BigBlind * settings.StackSize);
            
            HandEngine engine = new HandEngine() { AnalyzeHands = true };
            
            for (int i = 0, handsThisTourney = 0;
                seats.Count > 3 && handsThisTourney < settings.MaxHandsPerTourney;
                i = (i + 1) % seats.Count, handsThisTourney++)
            {
                ulong handNumber;
                handNumber = GamesPlayed++;

                HandHistory history = new HandHistory(seats.ToArray(), GamesPlayed, (uint)seats[i].SeatNumber,
                                                        new double[] { settings.SmallBlind, settings.BigBlind },
                                                        0, BettingStructure.Limit);
                engine.PlayHand(history);

                if (champions)
                    LogHand(settings, history, handNumber > 1);

                RemoveBrokePlayers(settings, seats);
            }

            ScorePlayers(seats);
        }

        private void ScorePlayers(List<Seat> seats)
        {
            for (var i = 0; i < seats.Count; i++)
            {
                var rank = 0;
                for (var j = 0; j < seats.Count; j++)
                    if (j != i && seats[i].Chips < seats[j].Chips)
                        rank++;

                switch (rank)
                {
                    case 0: Scores[i] = 3; break;
                    case 1: Scores[i] = 2; break;
                    case 2: Scores[i] = 1; break;
                }
            }
        }

        private void RemoveBrokePlayers(Settings settings, List<Seat> seats)
        {
            for (var j = seats.Count - 1; j >= 0; j--)
                if (seats[j].Chips < MIN_STACK * settings.BigBlind)
                {
                    seats.RemoveAt(j);
                    Scores[j] = -1;
                }
        }

        private void LogHand(Settings settings, HandHistory history, bool append)
        {
            lock(LogLock)
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

        private List<Seat> CreateSeats(List<IPlayer> players, double stackSize)
        {
            var seats = new List<Seat>();
            for (int i = 0; i < players.Count; i++)
                seats.Add(new Seat(i + 1, "Seat" + i + "_Network" + ((ProbabilisticLimitNeuralNetPlayer)players[i]).NetworkIndex,
                    stackSize, players[i]));
            return seats;
        }
    }
}
