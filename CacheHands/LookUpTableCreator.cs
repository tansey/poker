using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastPokerEngine;
using HoldemHand;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace FastPokerEngine
{
    public class LookUpTableCreator
    {
        #region Suit Masks
        
        public static readonly ulong SPADES_MASK =
            Hand.CardMasksTable[0+Hand.SPADE_OFFSET]
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

        public static void CreatePocketHandsLookUp(int minOpponents, int maxOpponents)
        {
            for (int opp = minOpponents; opp <= maxOpponents; opp++)
                CreatePocketHandsLookUp(opp);
        }


        public static void CreatePocketHandsLookUp(int opponents)
        {
            double[] ppot = new double[169];
            double[] npot = new double[169];
            double[] winp = new double[169];
            double[] hs = new double[169];

            using (TextWriter writer = new StreamWriter("preflops" + opponents + ".txt"))
            {
                foreach (ulong hc in PocketHands.Hands169())
                {
                    Hand.PocketHand169Enum handType = Hand.PocketHand169Type(hc);
                    
                    //Console.WriteLine(handType.ToString());
                    int index = (int)handType;
                    Hand.HandPotential(hc, 0UL, out ppot[index], out npot[index], opponents, 0.1);
                    winp[index] = Hand.WinOdds(hc, 0UL, 0UL, opponents, 0.1);
                    hs[index] = Hand.HandStrength(hc, 0UL, opponents, 0.1);

                    Console.WriteLine(index);
                    writer.WriteLine("new [] { " + ppot[index] + ", "
                                                 + npot[index] + ", " 
                                                 + winp[index] + ", " 
                                                 + hs[index] + " },");
                }
            }
        }

        public static void CreateFlopLookUpTable()
        {
            int situations = 0;
            int skipped = 0;
            
            CreateHandProbabilityDelegate d = new CreateHandProbabilityDelegate(CreateHandProbability);
            Dictionary<ulong, HashSet<ulong>> lookupTable = new Dictionary<ulong, HashSet<ulong>>();
            foreach (ulong pockets in PocketHands.Hands169())
            {
                Hand.PocketHand169Enum handType = Hand.PocketHand169Type(pockets);
                ulong[] flops = EnumerateFlop(pockets);
                situations += flops.Length;
                Console.WriteLine("Hand: {0} Flops: {1}", Hand.MaskToString(pockets), flops.Length);
                List<ulong> pocketList = new List<ulong>();
                List<ulong> boardList = new List<ulong>();
                foreach (ulong flop in flops)
                {
                    ulong isoPockets, isoFlop;
                    Transform(pockets, flop, out isoPockets, out isoFlop, false);
                    if (!lookupTable.ContainsKey(isoPockets))
                        lookupTable.Add(isoPockets, new HashSet<ulong>());
                    if (lookupTable[isoPockets].Contains(isoFlop))
                    {
                        skipped++;
                        continue;
                    }
                    lookupTable[isoPockets].Add(isoFlop);
                    pocketList.Add(isoPockets);
                    boardList.Add(isoFlop);
                }
                IAsyncResult[] results = new IAsyncResult[pocketList.Count * 5];

                for (int i = 0, r = 0; i < pocketList.Count; i++)
                {
                    results[r++] = d.BeginInvoke(pocketList[i], boardList[i], 1, null, null);
                    results[r++] = d.BeginInvoke(pocketList[i], boardList[i], 2, null, null);
                    results[r++] = d.BeginInvoke(pocketList[i], boardList[i], 3, null, null);
                    results[r++] = d.BeginInvoke(pocketList[i], boardList[i], 4, null, null);
                    results[r++] = d.BeginInvoke(pocketList[i], boardList[i], 5, null, null);
                }

                Dictionary<ulong, List<HandProbabilities>>[] probabilities = new Dictionary<ulong, List<HandProbabilities>>[5];
                for (int i = 0; i < 5; i++)
                    probabilities[i] = new Dictionary<ulong, List<HandProbabilities>>();
                for (int i = 0; i < results.Length; i++)
                {
                    HandProbabilities prob = d.EndInvoke(results[i]);
                    if (!probabilities[prob.OpponentCount - 1].ContainsKey(prob.Pockets))
                        probabilities[prob.OpponentCount - 1].Add(prob.Pockets, new List<HandProbabilities>());

                    probabilities[prob.OpponentCount - 1][prob.Pockets].Add(prob);
                }

                for (int opponents = 1; opponents <= 5; opponents++)
                    foreach (var pair in probabilities[opponents - 1])
                        SaveProbabilities(pair.Value, pair.Key, opponents, Round.Flop);
            }
            Console.WriteLine("Situations: {0} Skipped: {1} Estimated time: {2} hours", situations, skipped, Math.Round(situations * 0.04 / 3600.0,2));
            
        }

        private static void SaveProbabilities(List<HandProbabilities> probabilities, ulong pockets, int opponents, Round round)
        {
            if (!Directory.Exists(round.ToString()))
                Directory.CreateDirectory(round.ToString());
            if (!Directory.Exists(round + @"\" + opponents))
                Directory.CreateDirectory(round + @"\" + opponents);
            string filename = round + @"\" + opponents + @"\" + Hand.MaskToString(pockets) + ".dat";

            using (BinaryWriter stream = new BinaryWriter(new FileStream(filename, FileMode.Create)))
            {
                switch (round)
                {
                    case Round.Flop:
                    case Round.Turn:
                        foreach (HandProbabilities probs in probabilities)
                        {
                            stream.Write(probs.Board);
                            stream.Write((float)probs.PositivePotential);
                            stream.Write((float)probs.NegativePotential);
                            stream.Write((float)probs.HandStrength);
                            stream.Write((float)probs.WinProbability);
                        }
                        break;
                }
            }
        }

        delegate HandProbabilities CreateHandProbabilityDelegate(ulong pockets, ulong board, int opponents);
        private static HandProbabilities CreateHandProbability(ulong pockets, ulong board, int opponents)
        {
            HandProbabilities prob = new HandProbabilities(pockets, board, opponents);
            return prob;
        }

        public static void CreateTurnLookUpTable(int numOpponents)
        {
            int situations = 0;
            int skipped = 0;

            CreateHandProbabilityDelegate d = new CreateHandProbabilityDelegate(CreateHandProbability);
            Dictionary<ulong, HashSet<ulong>> lookupTable = new Dictionary<ulong, HashSet<ulong>>();
            foreach (ulong pockets in PocketHands.Hands169())
            {
                Hand.PocketHand169Enum handType = Hand.PocketHand169Type(pockets);
                ulong[] flops = EnumerateFlop(pockets);
                situations += flops.Length;
                Console.WriteLine("Hand: {0}", Hand.MaskToString(pockets));
                List<ulong> pocketList = new List<ulong>();
                List<ulong> boardList = new List<ulong>();
                foreach (ulong flop in flops)
                {
                    for (int t = 51; t >= 0; t--)
                    {
                        ulong turn = Hand.Mask(t);
                        if (Hand.BitCount(pockets | flop | turn) != 6)
                            continue;
                        ulong isoPockets, isoTurn;
                        Transform(pockets, flop | turn, out isoPockets, out isoTurn, false);
                        if (!lookupTable.ContainsKey(isoPockets))
                            lookupTable.Add(isoPockets, new HashSet<ulong>());
                        if (lookupTable[isoPockets].Contains(isoTurn))
                        {
                            skipped++;
                            continue;
                        }
                        lookupTable[isoPockets].Add(isoTurn);
                        pocketList.Add(isoPockets);
                        boardList.Add(isoTurn);
                    }
                }
                IAsyncResult[] results = new IAsyncResult[pocketList.Count];

                for (int i = 0, r = 0; i < pocketList.Count; i++)
                {
                    results[r++] = d.BeginInvoke(pocketList[i], boardList[i], numOpponents, null, null);
                }

                Dictionary<ulong, List<HandProbabilities>> probabilities = new Dictionary<ulong, List<HandProbabilities>>();
                for (int i = 0; i < results.Length; i++)
                {
                    HandProbabilities prob = d.EndInvoke(results[i]);
                    if (!probabilities.ContainsKey(prob.Pockets))
                        probabilities.Add(prob.Pockets, new List<HandProbabilities>());

                    probabilities[prob.Pockets].Add(prob);
                }

                foreach (var pair in probabilities)
                    SaveProbabilities(pair.Value, pair.Key, numOpponents, Round.Turn);
            }
           
        }
        
        

        /// <summary>
        /// Transforms a standard 4-suit set of holecards and board cards
        /// into an isomorphic-suit version.
        /// </summary>
        public static void Transform(ulong pockets, ulong board, out ulong isoPockets, out ulong isoBoard, bool print)
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

            if (print)
                Console.WriteLine("Order: {0} {1} {2} {3}", suits[3], suits[2], suits[1], suits[0]);

            ulong transformedPockets = 0UL;
            ulong transformedBoard = 0UL;
            for (int i = 3; i >= 0; i--)
            {
                int suit = suits[i];
                foreach (int card in Cards(pockets))
                    if (Hand.CardSuit(card) == suit)
                    {
                        transformedPockets |= Card(Hand.CardRank(card), i);
                        if (print)
                            Console.WriteLine("Transforming {0} -> {1} current pockets: {2}", 
                                Hand.MaskToString(Hand.Mask(card)),
                                Hand.MaskToString(Card(Hand.CardRank(card), i)), 
                                Hand.MaskToString(transformedPockets));
                    }

                foreach (int card in Cards(board))
                    if (Hand.CardSuit(card) == suit)
                        transformedBoard |= Card(Hand.CardRank(card), i);
            }
            if(print)
                Console.WriteLine("Final Pockets: {0}", Hand.MaskToString(transformedPockets));
            isoPockets = transformedPockets;
            isoBoard = transformedBoard;
        }

        public static IEnumerable<int> Cards(ulong mask)
        {
            for (int i = 51; i >= 0; i--)
            {
                if (((1UL << i) & mask) != 0)
                {
                    yield return i;
                }
            }
        }

        public static int CountSuit(ulong cards, int suit)
        {
            ulong mask = cards & SuitMasks[suit];
            return Hand.BitCount(mask);
        }

        public static ulong[] EnumerateFlop(ulong pockets)
        {
            //if (PocketHands.IsSuited(pockets))
            //    return EnumerateFlopForSuitedPockets(pockets);
            return EnumerateFlopForUnsuitedPockets(pockets);
        }

        

        public static ulong[] EnumerateFlopForSuitedPockets(ulong pockets)
        {
            //IEnumerable<string> cards = Hand.Cards(pockets);
            //int hc1 = Hand.ParseCard(cards.ElementAt(0));
            //int hc2 = Hand.ParseCard(cards.ElementAt(1));
            //int hcSuit = Hand.CardSuit(hc1);
            //int hcRank1 = Hand.CardRank(hc1);
            //int hcRank2 = Hand.CardRank(hc2);
            //int test = hcRank1 + hcSuit * 13;
            //if (hcSuit != Hand.Spades)
            //    throw new Exception("Suited holecards should be spades!");
            int hcSuit = Hand.Spades;
            int boardSuit1 = Hand.Hearts;
            int boardSuit2 = Hand.Diamonds;
            

            List<ulong> boards = new List<ulong>();
            for(int rank1 = Hand.RankAce; rank1 >= Hand.Rank2; rank1--)
                for(int rank2 = rank1; rank2 >= Hand.Rank2; rank2--)
                    for (int rank3 = rank2; rank3 >= Hand.Rank2; rank3--)
                    {
                        //create flop for all spades
                        ulong board = Card(rank1, hcSuit) | Card(rank2, hcSuit) | Card(rank3, hcSuit);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for all hearts
                        board = Card(rank1, boardSuit1) | Card(rank2, boardSuit1) | Card(rank3, boardSuit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 2 spades, 1 heart
                        //note there are three different places we could put the one heart
                        board = Card(rank1, boardSuit1) | Card(rank2, hcSuit) | Card(rank3, hcSuit);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, hcSuit) | Card(rank2, boardSuit1) | Card(rank3, hcSuit);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, hcSuit) | Card(rank2, hcSuit) | Card(rank3, boardSuit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 2 hearts, 1 spade
                        //note there are three different places we could put the one spade
                        board = Card(rank1, hcSuit) | Card(rank2, boardSuit1) | Card(rank3, boardSuit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, boardSuit1) | Card(rank2, hcSuit) | Card(rank3, boardSuit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, boardSuit1) | Card(rank2, boardSuit1) | Card(rank3, hcSuit);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 2 hearts, 1 diamond
                        //note there are three different places we could put the one diamond
                        board = Card(rank1, boardSuit2) | Card(rank2, boardSuit1) | Card(rank3, boardSuit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, boardSuit1) | Card(rank2, boardSuit2) | Card(rank3, boardSuit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, boardSuit1) | Card(rank2, boardSuit1) | Card(rank3, boardSuit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                       
                    }
            return boards.ToArray();
        }
        
        public static ulong[] EnumerateFlopForUnsuitedPockets(ulong pockets)
        {
            int suit1 = Hand.Spades;
            int suit2 = Hand.Hearts;
            int suit3 = Hand.Diamonds;
            int suit4 = Hand.Clubs;

            //hole cards are XsXh
            List<ulong> boards = new List<ulong>();
            for (int rank1 = Hand.RankAce; rank1 >= Hand.Rank2; rank1--)
                for (int rank2 = rank1; rank2 >= Hand.Rank2; rank2--)
                    for (int rank3 = rank2; rank3 >= Hand.Rank2; rank3--)
                    {
                        //create flop for 3 spades
                        ulong board = Card(rank1, suit1) | Card(rank2, suit1) | Card(rank3, suit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 3 hearts
                        board = Card(rank1, suit2) | Card(rank2, suit2) | Card(rank3, suit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 3 diamonds
                        board = Card(rank1, suit3) | Card(rank2, suit3) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 2 spades, 1 heart
                        board = Card(rank1, suit2) | Card(rank2, suit1) | Card(rank3, suit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit1) | Card(rank2, suit2) | Card(rank3, suit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit1) | Card(rank2, suit1) | Card(rank3, suit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 1 spade, 2 hearts
                        board = Card(rank1, suit1) | Card(rank2, suit2) | Card(rank3, suit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit2) | Card(rank2, suit1) | Card(rank3, suit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit2) | Card(rank2, suit2) | Card(rank3, suit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 2 spades, 1 diamond
                        board = Card(rank1, suit3) | Card(rank2, suit1) | Card(rank3, suit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit1) | Card(rank2, suit3) | Card(rank3, suit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit1) | Card(rank2, suit1) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 2 hearts, 1 diamond
                        board = Card(rank1, suit3) | Card(rank2, suit2) | Card(rank3, suit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit2) | Card(rank2, suit3) | Card(rank3, suit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit2) | Card(rank2, suit2) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 1 spade, 2 diamonds
                        board = Card(rank1, suit1) | Card(rank2, suit3) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit1) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit3) | Card(rank3, suit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 1 heart, 2 diamonds
                        board = Card(rank1, suit2) | Card(rank2, suit3) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit2) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit3) | Card(rank3, suit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 2 diamonds, 1 club
                        board = Card(rank1, suit4) | Card(rank2, suit3) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit4) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit3) | Card(rank3, suit4);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 1 spade, 1 heart, 1 diamond
                        board = Card(rank1, suit1) | Card(rank2, suit2) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit2) | Card(rank2, suit1) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit2) | Card(rank3, suit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit1) | Card(rank2, suit3) | Card(rank3, suit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit1) | Card(rank3, suit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit2) | Card(rank2, suit3) | Card(rank3, suit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create fop for 1 spade, 1 diamond, 1 club
                        board = Card(rank1, suit4) | Card(rank2, suit1) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit1) | Card(rank2, suit4) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit1) | Card(rank3, suit4);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit4) | Card(rank2, suit3) | Card(rank3, suit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit4) | Card(rank3, suit1);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit1) | Card(rank2, suit3) | Card(rank3, suit4);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);

                        //create flop for 1 heart, 1 diamond, 1 club
                        board = Card(rank1, suit4) | Card(rank2, suit2) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit2) | Card(rank2, suit4) | Card(rank3, suit3);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit2) | Card(rank3, suit4);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit4) | Card(rank2, suit3) | Card(rank3, suit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit3) | Card(rank2, suit4) | Card(rank3, suit2);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                        board = Card(rank1, suit2) | Card(rank2, suit3) | Card(rank3, suit4);
                        if ((board & pockets) == 0 && Hand.BitCount(board) == 3)
                            boards.Add(board);
                    }

            return boards.ToArray();
        }


        public static ulong Card(int rank, int suit)
        {
            return Hand.CardMasksTable[rank + suit * 13];
        }

        
    }
}
