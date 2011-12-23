using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HoldemHand;

namespace FastPokerEngine
{
    public class HandProbabilitiesLookup
    {
        #region Suit Masks

        public static readonly ulong SPADES_MASK =
            Hand.CardMasksTable[0 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[1 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[2 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[3 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[4 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[5 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[6 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[7 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[8 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[9 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[10 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[11 + Hand.SPADE_OFFSET]
            | Hand.CardMasksTable[12 + Hand.SPADE_OFFSET];

        public static readonly ulong HEARTS_MASK =
            Hand.CardMasksTable[0 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[1 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[2 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[3 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[4 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[5 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[6 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[7 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[8 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[9 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[10 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[11 + Hand.HEART_OFFSET]
            | Hand.CardMasksTable[12 + Hand.HEART_OFFSET];

        public static readonly ulong DIAMONDS_MASK =
            Hand.CardMasksTable[0 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[1 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[2 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[3 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[4 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[5 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[6 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[7 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[8 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[9 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[10 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[11 + Hand.DIAMOND_OFFSET]
            | Hand.CardMasksTable[12 + Hand.DIAMOND_OFFSET];

        public static readonly ulong CLUBS_MASK =
            Hand.CardMasksTable[0 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[1 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[2 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[3 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[4 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[5 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[6 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[7 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[8 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[9 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[10 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[11 + Hand.CLUB_OFFSET]
            | Hand.CardMasksTable[12 + Hand.CLUB_OFFSET];

        public static readonly ulong[] SuitMasks = { CLUBS_MASK, DIAMONDS_MASK, HEARTS_MASK, SPADES_MASK };

        #endregion

        private class Probabilities
        {
            public float PositivePotential { get; set; }
            public float NegativePotential { get; set; }
            public float HandStrength { get; set; }
            public float WinProbability { get; set; }
        }

        private Dictionary<ulong, Dictionary<ulong, Probabilities>> probabilities;

        public HandProbabilitiesLookup()
        {
            load(AppDomain.CurrentDomain.BaseDirectory);
        }

        public HandProbabilitiesLookup(string directory)
        {
            load(directory);
        }

        public void GetProbabilities(ulong pockets, ulong board,
                                    out float ppot, out float npot,
                                    out float hs, out float wp)
        {
            ulong isoPockets, isoBoard;
            Transform(pockets, board, out isoPockets, out isoBoard);
            Probabilities prob = probabilities[isoPockets][isoBoard];
            ppot = prob.PositivePotential;
            npot = prob.NegativePotential;
            hs = prob.HandStrength;
            wp = prob.WinProbability;
        }

        private void load(string directory)
        {
            probabilities = new Dictionary<ulong, Dictionary<ulong, Probabilities>>();
            foreach (string pocketsFile in Directory.GetFiles(directory))
            {
                int startIndex = pocketsFile.LastIndexOf('\\') + 1;
                int endIndex = pocketsFile.LastIndexOf('.');
                string pocketQuery = pocketsFile.Substring(startIndex, endIndex - startIndex);
                Console.WriteLine(pocketQuery);
                ulong pockets = Hand.ParseHand(pocketQuery);
                addProbabilities(pockets, pocketsFile);
            }
        }

        private void addProbabilities(ulong pockets, string file)
        {
            if (!probabilities.ContainsKey(pockets))
                probabilities.Add(pockets, new Dictionary<ulong, Probabilities>());

            using (BinaryReader reader = new BinaryReader(new FileStream(file, FileMode.Open)))
            {
                while (reader.PeekChar() != -1)
                {
                    ulong board = reader.ReadUInt64();
                    Probabilities prob = new Probabilities();
                        prob.PositivePotential = reader.ReadSingle();
                        prob.NegativePotential = reader.ReadSingle();
                        prob.HandStrength = reader.ReadSingle();
                        prob.WinProbability = reader.ReadSingle();
                    
                    probabilities[pockets].Add(board, prob);
                }
            }
        }

        /// <summary>
        /// Transforms a standard 4-suit set of holecards and board cards
        /// into an isomorphic-suit version.
        /// </summary>
        private static void Transform(ulong pockets, ulong board, out ulong isoPockets, out ulong isoBoard)
        {
            ulong cards = pockets | board;

            List<int> suits = new List<int>() { 0, 1, 2, 3 };
            suits.Sort(delegate(int a, int b)
            {
                //sort based on who has the most on the board plus in the player's pockets
                var result = CountSuit(cards, a).CompareTo(CountSuit(cards, b));
                if (result != 0)
                    return result;

                //break ties based on who has the highest hole card
                int maxRank1 = -1, maxRank2 = -1;
                foreach (int card in Cards(pockets))
                {
                    var suit = Hand.CardSuit(card);
                    var rank = Hand.CardRank(card);
                    if (suit == a)
                    {
                        if (maxRank1 < rank)
                            maxRank1 = rank;
                    }
                    else if (suit == b)
                    {
                        if (maxRank2 < rank)
                            maxRank2 = rank;
                    }
                }
                result = maxRank1.CompareTo(maxRank2);
                if (result != 0)
                    return result;

                //break ties by which suit is higher (e.g., spades > hearts)
                return a.CompareTo(b);
            });

            ulong transformedPockets = 0UL;
            ulong transformedBoard = 0UL;
            for (int i = 3; i >= 0; i--)
            {
                int suit = suits[i];
                foreach (int card in Cards(pockets))
                    if (Hand.CardSuit(card) == suit)
                    {
                        transformedPockets |= Card(Hand.CardRank(card), i);
                    }

                foreach (int card in Cards(board))
                    if (Hand.CardSuit(card) == suit)
                        transformedBoard |= Card(Hand.CardRank(card), i);
            }
            isoPockets = transformedPockets;
            isoBoard = transformedBoard;
        }

        private static int CountSuit(ulong cards, int suit)
        {
            ulong mask = cards & SuitMasks[suit];
            return Hand.BitCount(mask);
        }

        private static IEnumerable<int> Cards(ulong mask)
        {
            for (int i = 51; i >= 0; i--)
            {
                if (((1UL << i) & mask) != 0)
                {
                    yield return i;
                }
            }
        }

        private static ulong Card(int rank, int suit)
        {
            return Hand.CardMasksTable[rank + suit * 13];
        }
    }
}
