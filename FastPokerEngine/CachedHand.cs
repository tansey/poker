using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastPokerEngine
{
    /// <summary>
    /// Stores all of the computation-intensive information about a single hand.
    /// </summary>
    [Serializable]
    public class CachedHand
    {
        public ulong[] HoleCards { get; set; }
        public ulong Flop { get; set; }
        public ulong Turn { get; set; }
        public ulong River { get; set; }

        public List<List<List<HandAnalysis>>> Analysis { get; set; }

        public HandAnalysis GetAnalysis(int seatIdx, int opponents, Round round)
        {
            
            switch (round)
            {
                case Round.Preflop: return Analysis[0][seatIdx][opponents - 1];
                case Round.Flop: return Analysis[1][seatIdx][opponents - 1];
                case Round.Turn: return Analysis[2][seatIdx][opponents - 1];
                case Round.River: return Analysis[3][seatIdx][opponents - 1];
                default: return null;
            }
        }

        public CachedHand()
        {
        }

        public CachedHand(int numPlayers, Random random)
        {
            cacheCards(numPlayers, random);
            cacheAnalysis(numPlayers);
        }

        private void cacheAnalysis(int numPlayers)
        {
            Analysis = new List<List<List<HandAnalysis>>>();
            for (int round = 0; round < 4; round++)
            {
                Analysis.Add(new List<List<HandAnalysis>>());
                for (int curPlayer = 0; curPlayer < numPlayers; curPlayer++)
                {
                    Analysis[round].Add(new List<HandAnalysis>());
                    for (int curOppCount = 1; curOppCount < numPlayers; curOppCount++)
                    {
                        ulong board = 0UL;
                        switch (round)
                        {
                            case 0: 
                                break;
                            case 1: board = Flop;
                                break;
                            case 2: board = Flop | Turn;
                                break;
                            case 3: board = Flop | Turn | River;
                                break;
                            default:
                                break;
                        }
                        Analysis[round][curPlayer].Add(new HandAnalysis(HoleCards[curPlayer], board, curOppCount));
                    }
                }
            }
        }

        private void cacheCards(int numPlayers, Random random)
        {
            ulong dead = 0UL;
            HoleCards = new ulong[numPlayers];
            for (int i = 0; i < numPlayers; i++)
            {
                HoleCards[i] = HoldemHand.Hand.RandomHand(random, dead, 2);
                dead = dead | HoleCards[i];
            }

            Flop = HoldemHand.Hand.RandomHand(random, dead, 3);
            dead = dead | Flop;

            Turn = HoldemHand.Hand.RandomHand(random, dead, 1);
            dead = dead | Turn;

            River = HoldemHand.Hand.RandomHand(random, dead, 1);
        }
    }

    [Serializable]
    public class CachedHands
    {
        public List<CachedHand> Hands { get; set; }
    }
}
