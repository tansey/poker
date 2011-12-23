using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HoldemHand;

namespace FastPokerEngine
{
    [Serializable]
    public class HandProbabilities
    {
        const double MAX_CALCULATION_TIME = 0.001;
        const int MIN_TRIALS = 1000;

        public readonly ulong Pockets;
        public readonly ulong Board;
        public readonly int OpponentCount;
        public readonly double PositivePotential;
        public readonly double NegativePotential;
        public readonly double HandStrength;
        public readonly double WinProbability;
        public HandProbabilities(ulong hand, ulong board, int numOpponents)
        {
            Hand.HandPotential(hand, board, out PositivePotential, out NegativePotential,
                               numOpponents, MAX_CALCULATION_TIME, MIN_TRIALS);

            HandStrength = Hand.HandStrength(hand, board, numOpponents, MAX_CALCULATION_TIME, MIN_TRIALS);
            WinProbability = Hand.WinOdds(hand, board, 0UL, numOpponents, MAX_CALCULATION_TIME, MIN_TRIALS);
            Pockets = hand;
            Board = board;
            OpponentCount = numOpponents;
        }
    }
}
