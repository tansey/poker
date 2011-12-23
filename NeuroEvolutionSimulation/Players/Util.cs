using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastPokerEngine;
using KeithRuleHand = HoldemHand.Hand;
using PokerAction = FastPokerEngine.Action;
using System.IO;
using HoldemHand;

namespace NeuroEvolutionSimulation.Players
{
    public static class Util
    {
        private class SeatInfo
        {
            public int Index { get; set; }
            public int BetLevel { get; set; }
        }

        public static int AddBetsAsSeparateNodes(HandHistory history, int maxPlayers, double[] inputs, int index)
        {
            Dictionary<string, SeatInfo> bets = new Dictionary<string, SeatInfo>();
            for (int i = ((int)history.Button + 1) % history.Players.Length, nodeIdx = 0;
                bets.Count < history.Players.Length;
                i = (i + 1) % history.Players.Length, nodeIdx++)
                bets.Add(history.Players[i].Name, new SeatInfo() { BetLevel = 0, Index = nodeIdx });

            //Each bet is one of 3 actions (fold/call/raise).
            //Each player can act up to 5 times per round.
            //There are 4 rounds of betting.
            //There are maxPlayers players
            int endIdx = 3 * 5 * 4 * maxPlayers + index;

            ulong hc = history.HoleCards[history.Hero];
            addBetsAsSeparateNodes(history.PreflopActions, inputs, bets,maxPlayers, ref index);

            if (history.CurrentRound >= Round.Flop)
                addBetsAsSeparateNodes(history.FlopActions, inputs, bets,maxPlayers, ref index);

            if (history.CurrentRound >= Round.Turn)
                addBetsAsSeparateNodes(history.TurnActions, inputs, bets,maxPlayers, ref index);

            if (history.CurrentRound >= Round.River)
                addBetsAsSeparateNodes(history.RiverActions, inputs, bets, maxPlayers, ref index);

            return endIdx;
        }

        private static void addBetsAsSeparateNodes(List<PokerAction> actions, double[] inputs,
                                        Dictionary<string, SeatInfo> bets, int maxPlayers, ref int index)
        {
            foreach (SeatInfo info in bets.Values)
                info.BetLevel = 0;
            foreach (PokerAction action in actions)
            {
                SeatInfo info = bets[action.Name];
                int offset = 5 * 3 * info.Index + 3 * info.BetLevel + index;
                switch (action.ActionType)
                {
                    case FastPokerEngine.Action.ActionTypes.Fold:
                        inputs[offset] = 1;
                        break;
                    case FastPokerEngine.Action.ActionTypes.Check:
                    case FastPokerEngine.Action.ActionTypes.Call:
                        inputs[offset + 1] = 1;
                        break;
                    case FastPokerEngine.Action.ActionTypes.Bet:
                    case FastPokerEngine.Action.ActionTypes.Raise:
                        inputs[offset + 2] = 1;
                        break;
                    default:
                        break;
                }
                info.BetLevel++;
                if (info.BetLevel > 5)
                    throw new Exception("Bet Level Maxed on player " + action.Name + " at " + info.BetLevel);
            }
            index += maxPlayers * 3 * 5;
        }
        public static int AddBetsAsSingleNodes(HandHistory history,int maxPlayers, double[] inputs, int index)
        {
            //Each round has:
            //1) number of players in
            //2) bet level
            //3) relative position
            //4) flag if you are the aggressor
            //There are 4 rounds of betting
            int endIndex = 4 * 4 + index;
            ulong hc = history.HoleCards[history.Hero];
            string heroName = history.Players[history.Hero].Name;
            int playersIn = history.Players.Length;

            var temp = new List<PokerAction>();
            foreach (PokerAction action in history.PredealActions)
                temp.Add(action);
            foreach (PokerAction action in history.PreflopActions)
                temp.Add(action);
            addBetsAsSingleNodes(heroName, temp, maxPlayers, ref playersIn, inputs, ref index);

            if (history.CurrentRound >= Round.Flop)
                addBetsAsSingleNodes(heroName, history.FlopActions,maxPlayers, ref playersIn, inputs, ref index);

            if (history.CurrentRound >= Round.Turn)
                addBetsAsSingleNodes(heroName, history.TurnActions,maxPlayers, ref playersIn, inputs, ref index);

            if (history.CurrentRound >= Round.River)
                addBetsAsSingleNodes(heroName, history.RiverActions,maxPlayers, ref playersIn, inputs, ref index);

            return endIndex;
        }

        
        private static void addBetsAsSingleNodes(string hero, List<PokerAction> actions,int maxPlayers, ref int playersIn, double[] inputs, ref int index)
        {
            bool aggressor = false;
            int betLevel = 0;
            bool foundPosition = false;
            List<string> beforePlayers = new List<string>();
            foreach (PokerAction action in actions)
            {
                if (action.ActionType == PokerAction.ActionTypes.Raise
                    || action.ActionType == PokerAction.ActionTypes.Bet
                    || action.ActionType == PokerAction.ActionTypes.PostBigBlind)
                {
                    aggressor = action.Name == hero;
                    betLevel++;
                }
                else if (action.ActionType == PokerAction.ActionTypes.Fold)
                {
                    playersIn--;
                    beforePlayers.Remove(action.Name);
                }

                if (!foundPosition)
                {
                    if (action.Name == hero)
                        foundPosition = true;
                    else if(action.ActionType != PokerAction.ActionTypes.Fold)
                        beforePlayers.Add(action.Name);
                }
            }

            inputs[index++] = (double)betLevel / 4.0;//bet level (number of raises)
            inputs[index++] = aggressor ? 1 : 0;//are we the last person to bet this round?
            inputs[index++] = (double)playersIn / (double)maxPlayers;//number of players in
            inputs[index++] = (double)beforePlayers.Count / (double)(playersIn-1);//our position relative to the other players in the hand
        }

        public static int AddPreviousRoundBetInfo(HandHistory history, double[] inputs, int index)
        {
            int endIndex = index + 2;
            if (history.CurrentRound == Round.Preflop)
                return endIndex;

            string hero = history.Players[history.Hero].Name;
            List<PokerAction> actions = null;
            switch (history.CurrentRound)
            {
                case Round.Flop:
                    actions = new List<PokerAction>();
                    foreach (PokerAction action in history.PredealActions)
                        actions.Add(action);
                    foreach (PokerAction action in history.PreflopActions)
                        actions.Add(action);
                    break;
                case Round.Turn: actions = history.FlopActions; break;
                case Round.River: actions = history.TurnActions; break;
            }

            int aggressor = 0;
            int betLevel = 0;
            foreach(PokerAction action in actions)
                switch (action.ActionType)
                {
                    case FastPokerEngine.Action.ActionTypes.PostBigBlind: betLevel++; break;
                    case FastPokerEngine.Action.ActionTypes.Bet:
                    case FastPokerEngine.Action.ActionTypes.Raise:
                        betLevel++;
                        if (action.Name == hero)
                            aggressor = 1;
                        else
                            aggressor = -1;
                        break;
                    default:
                        break;
                }

            inputs[index] = betLevel / 4.0;
            inputs[index + 1] = aggressor;
            return endIndex;
        }

        public static int AddBetsDecisionInfoAndPositionAsSingleNodes(HandHistory history, double[] inputs, int index)
        {
            List<PokerAction> actions = null;
            double betSize = history.BigBlind;
            switch (history.CurrentRound)
            {
                case Round.Preflop: 
                    actions = new List<PokerAction>();
                    foreach (PokerAction action in history.PredealActions)
                        actions.Add(action);
                    foreach (PokerAction action in history.PreflopActions)
                        actions.Add(action);
                    break;
                case Round.Flop: actions = history.FlopActions; break;
                case Round.Turn: actions = history.TurnActions; betSize *= 2;  break;
                case Round.River: actions = history.RiverActions; betSize *= 2; break;
            }

            double betsToCall = 0;
            string hero = history.Players[history.Hero].Name;
            foreach (PokerAction action in actions)
            {
                if (action.ActionType == FastPokerEngine.Action.ActionTypes.Raise
                    || action.ActionType == FastPokerEngine.Action.ActionTypes.Bet
                    || action.ActionType == FastPokerEngine.Action.ActionTypes.PostBigBlind)
                {
                    if (action.Name == hero)
                        betsToCall = 0;
                    else
                        betsToCall++;
                }
                else if (action.ActionType == FastPokerEngine.Action.ActionTypes.Call && action.Name == hero)
                    betsToCall = 0;
                else if (action.ActionType == FastPokerEngine.Action.ActionTypes.PostSmallBlind && action.Name == hero)
                    betsToCall -= 0.5;
            }

            int sb = (int)history.Button % history.Players.Length;
            //Console.WriteLine("Seat {0} is the button", (history.Button - 1) % history.Players.Length);
            //Console.WriteLine("button+1={0}", history.Button);
            //Console.WriteLine("Seat {0} is the sb", sb);
            int playersBefore = 0;
            for (int i = sb; i != history.Hero; i = (i + 1) % history.Players.Length)
                if (!history.Folded[i])
                    playersBefore++;

            int playersAfter = 0;
            for (int i = (history.Hero + 1) % history.Players.Length; i != sb; i = (i + 1) % history.Players.Length)
                if (!history.Folded[i])
                    playersAfter++;

            inputs[index++] = betsToCall / 4.0;
            inputs[index++] = betsToCall * betSize / history.Pot;
            inputs[index++] = history.CurrentBetLevel / 4.0;
            inputs[index++] = (double)playersBefore / (double)history.Players.Length;
            inputs[index++] = (double)playersAfter / (double)history.Players.Length;
            return index;
        }

        public static int AddBetsDecisionInfoAsSingleNodes(HandHistory history, double[] inputs, int index)
        {
            List<PokerAction> actions = null;
            double betSize = history.BigBlind;
            switch (history.CurrentRound)
            {
                case Round.Preflop:
                    actions = new List<PokerAction>();
                    foreach (PokerAction action in history.PredealActions)
                        actions.Add(action);
                    foreach (PokerAction action in history.PreflopActions)
                        actions.Add(action);
                    break;
                case Round.Flop: actions = history.FlopActions; break;
                case Round.Turn: actions = history.TurnActions; betSize *= 2; break;
                case Round.River: actions = history.RiverActions; betSize *= 2; break;
            }

            double betsToCall = 0;
            string hero = history.Players[history.Hero].Name;
            foreach (PokerAction action in actions)
            {
                if (action.ActionType == FastPokerEngine.Action.ActionTypes.Raise
                    || action.ActionType == FastPokerEngine.Action.ActionTypes.Bet
                    || action.ActionType == FastPokerEngine.Action.ActionTypes.PostBigBlind)
                {
                    if (action.Name == hero)
                        betsToCall = 0;
                    else
                        betsToCall++;
                }
                else if (action.ActionType == FastPokerEngine.Action.ActionTypes.Call && action.Name == hero)
                    betsToCall = 0;
                else if (action.ActionType == FastPokerEngine.Action.ActionTypes.PostSmallBlind && action.Name == hero)
                    betsToCall -= 0.5;
            }


            inputs[index++] = betsToCall / 4.0;
            inputs[index++] = betsToCall * betSize / history.Pot;
            inputs[index++] = history.CurrentBetLevel / 4.0;
            return index;
        }

        public static int AddRoundAsSingleNode(HandHistory history, double[] inputs, int index)
        {
            switch (history.CurrentRound)
            {
                case Round.Preflop:
                    inputs[index] = 0.25;
                    break;
                case Round.Flop:
                    inputs[index] = 0.5;
                    break;
                case Round.Turn:
                    inputs[index] = 0.75;
                    break;
                case Round.River:
                    inputs[index] = 1;
                    break;
                default:
                    break;
            }
            return index + 1;
        }

        public static int AddRoundAsSeparateNodes(HandHistory history, double[] inputs, int index)
        {
            switch (history.CurrentRound)
            {
                case Round.Preflop:
                    inputs[index] = 1;
                    break;
                case Round.Flop:
                    inputs[index + 1] = 1;
                    break;
                case Round.Turn:
                    inputs[index + 2] = 1;
                    break;
                case Round.River:
                    inputs[index + 3] = 1;
                    break;
                default:
                    break;
            }
            return index + 4;
        }

        public static int AddPlayersAsSeparateNodes(HandHistory history, double[] inputs, int index)
        {
            for (int i = 0; i < history.Players.Length; i++)
                if (i == history.Hero)
                    inputs[index + i] = 1;
                else if (!history.Folded[i])
                    inputs[index + i] = -1;

            return index + history.Players.Length;
        }
        public static int AddPlayersAsSingleNode(HandHistory history, double[] inputs, int index)
        {
            inputs[index] = (double)history.Players.Length / 10.0;
            return index + 1;
        }


        public static int AddCardsAsSingleNodes(HandHistory history, double[] inputs, int index)
        {
            //Each card has a rank and a suit
            //2 hole cards and 5 board cards = 7 total cards
            int endIndex = 2 * 7 + index;
            addCardsAsSingleNodes(history.HoleCards[history.Hero], inputs, ref index);

            if (history.CurrentRound >= Round.Flop)
                addCardsAsSingleNodes(history.Flop, inputs, ref index);
            if (history.CurrentRound >= Round.Turn)
                addCardsAsSingleNodes(history.Turn, inputs, ref index);
            if (history.CurrentRound >= Round.River)
                addCardsAsSingleNodes(history.River, inputs, ref index);

            return endIndex;
        }
        private static void addCardsAsSingleNodes(ulong mask, double[] inputs, ref int index)
        {
            foreach (string card in KeithRuleHand.Cards(mask))
            {
                setRankAsSingleNode(card[0], inputs, index);
                index++;
                setSuitAsSingleNode(card[1], inputs, index);
                index++;
            }
        }

        public static int AddCardsAsSeparateNodes(HandHistory history, double[] inputs, int index)
        {
            //13 different ranks
            //4 different suits
            //2 hole cards and 5 board cards = 7 total cards
            int endIndex = 13* 7 + 4 * 7 + index;
            addCards(history.HoleCards[history.Hero], inputs, ref index);
            
            if (history.CurrentRound >= Round.Flop)
                addCards(history.Flop, inputs, ref index);
            if (history.CurrentRound >= Round.Turn)
                addCards(history.Turn, inputs, ref index);
            if (history.CurrentRound >= Round.River)
                addCards(history.River, inputs, ref index);

            return endIndex;
        }

        

        private static void addCards(ulong mask, double[] inputs, ref int index)
        {
            foreach (string card in KeithRuleHand.Cards(mask))
            {
                setRank(card[0], inputs, index);
                index += 13;
                setSuit(card[1], inputs, index);
                index += 4;
            }
        }

        private static void setRank(char rank, double[] inputs, int offset)
        {
            switch (rank)
            {
                case '2': inputs[offset] = 1; break;
                case '3': inputs[offset + 1] = 1; break;
                case '4': inputs[offset + 2] = 1; break;
                case '5': inputs[offset + 3] = 1; break;
                case '6': inputs[offset + 4] = 1; break;
                case '7': inputs[offset + 5] = 1; break;
                case '8': inputs[offset + 6] = 1; break;
                case '9': inputs[offset + 7] = 1; break;
                case 'T': inputs[offset + 8] = 1; break;
                case 'J': inputs[offset + 9] = 1; break;
                case 'Q': inputs[offset + 10] = 1; break;
                case 'K': inputs[offset + 11] = 1; break;
                case 'A': inputs[offset + 12] = 1; break;
                default: throw new Exception("Unknown card rank: " + rank);
            }
        }

        private static void setRankAsSingleNode(char rank, double[] inputs, int offset)
        {
            switch (rank)
            {
                case '2': inputs[offset] = 1.0 / 13.0; break;
                case '3': inputs[offset] = 2.0 / 13.0; break;
                case '4': inputs[offset] = 3.0 / 13.0; break;
                case '5': inputs[offset] = 4.0 / 13.0; break;
                case '6': inputs[offset] = 5.0 / 13.0; break;
                case '7': inputs[offset] = 6.0 / 13.0; break;
                case '8': inputs[offset] = 7.0 / 13.0; break;
                case '9': inputs[offset] = 8.0 / 13.0; break;
                case 'T': inputs[offset] = 9.0 / 13.0; break;
                case 'J': inputs[offset] = 10.0 / 13.0; break;
                case 'Q': inputs[offset] = 11.0 / 13.0; break;
                case 'K': inputs[offset] = 12.0 / 13.0; break;
                case 'A': inputs[offset] = 13.0 / 13.0; break;
                default: throw new Exception("Unknown card rank: " + rank);
            }
        }

        private static void setSuit(char suit, double[] inputs, int offset)
        {
            switch (suit)
            {
                case 'c': inputs[offset] = 1; break;
                case 'd': inputs[offset + 1] = 1; break;
                case 'h': inputs[offset + 2] = 1; break;
                case 's': inputs[offset + 3] = 1; break;
                default: throw new Exception("Unknown card suit: " + suit);
            }
        }

        private static void setSuitAsSingleNode(char suit, double[] inputs, int offset)
        {
            switch (suit)
            {
                case 'c': inputs[offset] = 1.0 / 4.0; break;
                case 'd': inputs[offset] = 2.0 / 4.0; break;
                case 'h': inputs[offset] = 3.0 / 4.0; break;
                case 's': inputs[offset] = 4.0 / 4.0; break;
                default: throw new Exception("Unknown card suit: " + suit);
            }
        }

        public static int AddAnalysisAsSingleNodes(HandHistory history, double[] inputs, int index)
        {
            ulong hand = history.HoleCards[history.Hero];
            ulong board = history.Board;
            int endIndex = index + 10;
            

            inputs[index++] = KeithRuleHand.IsConnected(hand) ? 1 : 0;
            inputs[index++] = KeithRuleHand.IsSuited(hand) ? 1 : 0;




            if (history.CurrentRound > Round.Preflop)
            {
                switch (KeithRuleHand.EvaluateType(hand | board))
                {
                    case HoldemHand.Hand.HandTypes.StraightFlush: inputs[index++] = 8.0 / 8.0;
                        break;
                    case HoldemHand.Hand.HandTypes.FourOfAKind: inputs[index++] = 7.0 / 8.0;
                        break;
                    case HoldemHand.Hand.HandTypes.FullHouse: inputs[index++] = 6.0 / 8.0;
                        break;
                    case HoldemHand.Hand.HandTypes.Flush: inputs[index++] = 5.0 / 8.0;
                        break;
                    case HoldemHand.Hand.HandTypes.Straight: inputs[index++] = 4.0 / 8.0;
                        break;
                    case HoldemHand.Hand.HandTypes.Trips: inputs[index++] = 3.0 / 8.0;
                        break;
                    case HoldemHand.Hand.HandTypes.TwoPair: inputs[index++] = 2.0 / 8.0;
                        break;
                    case HoldemHand.Hand.HandTypes.Pair: inputs[index++] = 1.0 / 8.0;
                        break;
                    case HoldemHand.Hand.HandTypes.HighCard: inputs[index++] = 0.0 / 8.0;
                        break;
                    default:
                        break;
                };

                if (history.CurrentRound < Round.River)
                {
                    inputs[index++] = Math.Min(KeithRuleHand.StraightDrawCount(hand, board, 0UL) / 8.0, 1);
                    inputs[index++] = Math.Min(KeithRuleHand.Outs(hand, board) / 15.0, 1);
                    inputs[index++] = Math.Min(KeithRuleHand.OutsDiscounted(hand, board) / 15.0, 1);
                    inputs[index++] = KeithRuleHand.IsBackdoorFlushDraw(hand, board, 0UL) ? 1 : 0;
                    inputs[index++] = KeithRuleHand.IsFlushDraw(hand, board, 0UL) ? 1 : 0;
                    inputs[index++] = KeithRuleHand.IsGutShotStraightDraw(hand, board, 0UL) ? 1 : 0;
                    inputs[index++] = KeithRuleHand.IsOpenEndedStraightDraw(hand, board, 0UL) ? 1 : 0;
                }
            }
            else
            {
                //just check quickly if it's a pair
                IEnumerable<string> cards = KeithRuleHand.Cards(hand);
                inputs[index] = cards.ElementAt(0)[0] == cards.ElementAt(1)[0] ? 1.0 / 8.0 : 0;
            }

            return endIndex;
        }

        private static HandProbabilitiesLookup[] FlopTables;
        private static HandProbabilitiesLookup[] TurnTables;

        public static void Initialize(string tableDir)
        {
            string[] dirs = Directory.GetDirectories(tableDir + "\\Flop");
            FlopTables = new HandProbabilitiesLookup[dirs.Length];
            for (int i = 1; i <= dirs.Length; i++)
                FlopTables[i-1] = new HandProbabilitiesLookup(tableDir + @"\Flop\" + i);


            dirs = Directory.GetDirectories(tableDir + "\\Turn");
            TurnTables = new HandProbabilitiesLookup[dirs.Length];
            for (int i = 1; i <= dirs.Length; i++)
                TurnTables[i-1] = new HandProbabilitiesLookup(tableDir + @"\Turn\" + i);

            //initialize Hand
            ulong hc = Hand.RandomHand(0UL, 2);
            Hand.PocketHand169Type(hc);
        }

        public static int AddProbabilitiesAsSingleNodes(HandHistory history, double[] inputs, int index)
        {
            int endIndex = index + 4;

            int numOpponents = -1;
            foreach (bool folded in history.Folded)
                if (!folded)
                    numOpponents++;

            ulong hc = history.HoleCards[history.Hero];
            
            switch (history.CurrentRound)
            {
                case Round.Preflop:
                    //Console.WriteLine("Preflop: {0} {1}", numOpponents, (int)Hand.PocketHand169Type(hc));
                    double[] probs = PreflopTables[numOpponents - 1][(int)Hand.PocketHand169Type(hc)];
                    inputs[index] = probs[0];
                    inputs[index+1] = probs[1];
                    inputs[index+2] = probs[2];
                    inputs[index+3] = probs[3];
                    break;
                case Round.Flop:
                    //Console.WriteLine("Flop: {0}", numOpponents);
                    float ppot, npot, hs, wp;
                    FlopTables[numOpponents - 1].GetProbabilities(hc, history.Board,
                        out ppot, out npot, out hs, out wp);
                    inputs[index] = ppot;
                    inputs[index+1] = npot;
                    inputs[index+2] = wp;
                   inputs[index+3] = hs;
                    break;
                case Round.Turn:
                    //Console.WriteLine("Turn: {0}", numOpponents);
                    if (TurnTables.Length >= numOpponents)
                    {
                        TurnTables[numOpponents - 1].GetProbabilities(hc, history.Board,
                            out ppot, out npot, out hs, out wp);

                        inputs[index] = ppot;
                        inputs[index+1] = npot;
                        inputs[index+2] = wp;
                        inputs[index+3] = hs;
                    }
                    else
                    {
                        Hand.HandPotential(hc, history.Board, out inputs[index], out inputs[index+1], numOpponents, 0, 100);
                        inputs[index+2] = Hand.HandStrength(hc, history.Board, numOpponents, 0, 100);
                        inputs[index+3] = Hand.WinOdds(hc, history.Board, 0UL, numOpponents, 0, 100);
                    }

                    break;
                case Round.River:
                    //Console.WriteLine("River: {0}", numOpponents);
                    inputs[index] = 0;
                    inputs[index + 1] = 0;
                    inputs[index + 2] = Math.Pow(Hand.HandStrength(hc, history.Board), numOpponents);
                    inputs[index + 3] = inputs[index + 2];//on the river, hs and wp are equivalent
                    break;
                default: throw new Exception("Must be a bettable round.");
            }

            return endIndex;
        }

        public static int AddCurrentAndPreviousProbabilities(HandHistory history, double[] inputs, int index)
        {
            int endIndex = index + 5;

            int numOpponents = -1;
            foreach (bool folded in history.Folded)
                if (!folded)
                    numOpponents++;

            ulong hc = history.HoleCards[history.Hero];

            switch (history.CurrentRound)
            {
                case Round.Preflop:
                    double[] probs = PreflopTables[numOpponents - 1][(int)Hand.PocketHand169Type(hc)];
                    inputs[index] = probs[0];
                    inputs[index + 1] = probs[1];
                    inputs[index + 2] = probs[2];
                    inputs[index + 3] = probs[3];
                    break;
                case Round.Flop:
                    float ppot, npot, hs, wp;
                    FlopTables[numOpponents - 1].GetProbabilities(hc, history.Board,
                        out ppot, out npot, out hs, out wp);
                    inputs[index] = ppot;
                    inputs[index + 1] = npot;
                    inputs[index + 2] = wp;
                    inputs[index + 3] = hs;
                    probs = PreflopTables[numOpponents - 1][(int)Hand.PocketHand169Type(hc)];
                    inputs[index + 4] = inputs[index + 3] - probs[3];
                    break;
                case Round.Turn:
                    if (TurnTables.Length >= numOpponents)
                    {
                        TurnTables[numOpponents - 1].GetProbabilities(hc, history.Board,
                            out ppot, out npot, out hs, out wp);

                        inputs[index] = ppot;
                        inputs[index + 1] = npot;
                        inputs[index + 2] = wp;
                        inputs[index + 3] = hs;
                    }
                    else
                    {
                        Hand.HandPotential(hc, history.Board, out inputs[index], out inputs[index + 1], numOpponents, 0, 100);
                        inputs[index + 2] = Hand.HandStrength(hc, history.Board, numOpponents, 0, 100);
                        inputs[index + 3] = Hand.WinOdds(hc, history.Board, 0UL, numOpponents, 0, 100);
                    }
                    FlopTables[numOpponents - 1].GetProbabilities(hc, history.Flop,
                        out ppot, out npot, out hs, out wp);
                    inputs[index + 4] = inputs[index + 3] - hs;
                    break;
                case Round.River:
                    inputs[index] = 0;
                    inputs[index + 1] = 0;
                    inputs[index + 2] = Math.Pow(Hand.HandStrength(hc, history.Board), numOpponents);
                    inputs[index + 3] = inputs[index + 2];//on the river, hs and wp are equivalent

                    if (TurnTables.Length >= numOpponents)
                    {
                        TurnTables[numOpponents - 1].GetProbabilities(hc, history.Flop | history.Turn,
                            out ppot, out npot, out hs, out wp);

                        inputs[index+4] = inputs[index+3] - hs;
                    }
                    else
                        inputs[index + 4] = inputs[index+3] - Hand.HandStrength(hc, history.Flop | history.Turn, numOpponents, 0, 100);
                    break;
                default: throw new Exception("Must be a bettable round.");
            }

            return endIndex;
        }

        #region Probability Lookup Tables
        private static readonly double[][][] PreflopTables = new double[][][]{
#region 1 Opponent
            new double[][] {
                new [] { 0.00002, 0.148720643221975, 0.851236461929027, 0.999636927381021 },
                new [] { 0.0010023211648027, 0.171993442303634, 0.824475691134414, 0.994723061070643 },
                new [] { 0.00184850376592105, 0.192694356388283, 0.798603567298595, 0.989766890204751 },
                new [] { 0.00255242602016011, 0.21031585259132, 0.776402154524766, 0.985182509743068 },
                new [] { 0.00340144298745263, 0.232926129707282, 0.749879293847059, 0.979736205151978 },
                new [] { 0.00470008589812159, 0.258954473995559, 0.719645131538495, 0.975336833364584 },
                new [] { 0.00568541784941459, 0.282695266539958, 0.689679225086423, 0.970661252796819 },
                new [] { 0.00663603464400439, 0.309258732159558, 0.659864752118737, 0.965379702169854 },
                new [] { 0.00784945973894847, 0.335283484378435, 0.634798319032213, 0.960211385376948 },
                new [] { 0.00819432369406775, 0.358929253369951, 0.600516358780325, 0.955063901185206 },
                new [] { 0.0092040287488388, 0.389176811877638, 0.571084163937966, 0.950386064998413 },
                new [] { 0.0097840155314674, 0.419923960524187, 0.537904311186566, 0.94492010785996 },
                new [] { 0.0107765314926661, 0.447075064710958, 0.504465578200258, 0.940280226195655 },
                new [] { 0.026824623131799, 0.292863714128926, 0.672215305299337, 0.937517957190059 },
                new [] { 0.0249287864564788, 0.308799728093743, 0.651961854526718, 0.937608881603991 },
                new [] { 0.0286566054808687, 0.29310205532575, 0.660708933514343, 0.92657316046774 },
                new [] { 0.0273657434036245, 0.311919247563574, 0.643091845161744, 0.927535615088254 },
                new [] { 0.0316060495479058, 0.294274824636095, 0.655578013093721, 0.917812217915957 },
                new [] { 0.0296015180265655, 0.312329928539707, 0.6356244111613, 0.91685504917343 },
                new [] { 0.0344558077658602, 0.297493140434023, 0.644566489759635, 0.907963571188122 },
                new [] { 0.0314404993514916, 0.311705577172503, 0.626216833596907, 0.908517089182143 },
                new [] { 0.0362923417384141, 0.307226969486488, 0.627687481167692, 0.897927898175586 },
                new [] { 0.0324325199566055, 0.322167619294354, 0.609998575955, 0.897331835856224 },
                new [] { 0.0388007197626728, 0.3075445393681, 0.622279078748727, 0.888252315851714 },
                new [] { 0.0347932405405624, 0.324438668512806, 0.600395141871808, 0.888372093023256 },
                new [] { 0.0428585403233038, 0.31055424672498, 0.609098564123256, 0.878251618249826 },
                new [] { 0.0382798195134711, 0.328586842793618, 0.590393818677646, 0.87754875593763 },
                new [] { 0.0439500152392563, 0.315030986487859, 0.597302347520046, 0.867675318761384 },
                new [] { 0.0399455251002945, 0.332698886710258, 0.575562729481881, 0.867651592666896 },
                new [] { 0.0498484848484849, 0.310589680589681, 0.601615972153337, 0.859471983195777 },
                new [] { 0.0451183311314446, 0.328456557550847, 0.578245858792781, 0.858816630794739 },
                new [] { 0.0534877601602553, 0.314853325637456, 0.589322390941806, 0.84985053774659 },
                new [] { 0.0479258122949883, 0.330776637237463, 0.568134770504676, 0.850744162290998 },
                new [] { 0.0560132066328835, 0.317119344267681, 0.582234314132518, 0.839523221655523 },
                new [] { 0.0512866537212604, 0.33330205766689, 0.555049004390439, 0.840119803500705 },
                new [] { 0.0611371352092446, 0.319612195599661, 0.574045706898536, 0.830820144532745 },
                new [] { 0.0546433006761419, 0.334702392064871, 0.550589112870611, 0.830019048133688 },
                new [] { 0.0918513165026856, 0.244173058918218, 0.635115549816859, 0.788136432823857 },
                new [] { 0.0833232030990413, 0.255249487410145, 0.616830883256933, 0.787487439890907 },
                new [] { 0.0928661283122996, 0.244742929026498, 0.625465745059341, 0.776072627279712 },
                new [] { 0.0840120244737251, 0.253820956239738, 0.604358637473877, 0.776903462467567 },
                new [] { 0.0939492517891997, 0.245738451528953, 0.618753096986351, 0.768213143552092 },
                new [] { 0.0870223262130521, 0.259389853803801, 0.596181859307849, 0.767765241488519 },
                new [] { 0.0945475736745389, 0.25110171510243, 0.602488172731477, 0.757649015216326 },
                new [] { 0.0867613664210681, 0.266100047377105, 0.57773077770434, 0.757286111572866 },
                new [] { 0.0932376538595704, 0.258774771183203, 0.582393957001743, 0.74664980525519 },
                new [] { 0.0839742858539606, 0.27292763211498, 0.562893721687429, 0.748577330398348 },
                new [] { 0.0965528521774688, 0.2590308730321, 0.578227362020996, 0.738290584312336 },
                new [] { 0.0887568603085844, 0.271629388008698, 0.550573817038866, 0.738713187258432 },
                new [] { 0.0998365433849305, 0.2612548741581, 0.568058853989965, 0.731115875969829 },
                new [] { 0.0886834904727369, 0.277704061449199, 0.539774076908865, 0.727635441626693 },
                new [] { 0.101706068929436, 0.26406464587012, 0.556401058407771, 0.716965676351379 },
                new [] { 0.090240565499943, 0.277143020017265, 0.533980325788026, 0.718625865684689 },
                new [] { 0.104973755537541, 0.2676116310872, 0.549665248685226, 0.710570484898664 },
                new [] { 0.0949462250741511, 0.2777111353945, 0.525909989023052, 0.708418705828196 },
                new [] { 0.109775404852333, 0.26668042140012, 0.541919944947815, 0.699638275230531 },
                new [] { 0.0966874897254644, 0.280642774946572, 0.513858205342604, 0.700605185493654 },
                new [] { 0.109753515679074, 0.268062895437557, 0.533306721878125, 0.689117832104979 },
                new [] { 0.0999534701474262, 0.28413821815154, 0.503774399267141, 0.691490792978805 },
                new [] { 0.150743590992483, 0.198671115224133, 0.604066720463957, 0.651156946164755 },
                new [] { 0.138803492878518, 0.208579301097248, 0.582078493659991, 0.650003149436251 },
                new [] { 0.152190789043603, 0.198319114859123, 0.595699916545878, 0.640134096175609 },
                new [] { 0.139761691014445, 0.207141108300008, 0.575486268003389, 0.640830912051238 },
                new [] { 0.15019399632428, 0.202658770675924, 0.573465715117459, 0.630362349962703 },
                new [] { 0.136806183701998, 0.214464271030343, 0.554034247309906, 0.631043083280993 },
                new [] { 0.145811985588206, 0.208285128747562, 0.559457608176435, 0.622475905601468 },
                new [] { 0.134844405536652, 0.220380066730534, 0.534518819220756, 0.621696025293586 },
                new [] { 0.147117936888881, 0.213286238858878, 0.54519181630747, 0.611655379742465 },
                new [] { 0.132968626524477, 0.224082869642435, 0.515457556090242, 0.610729066745928 },
                new [] { 0.148393618495602, 0.215196426096668, 0.538461538461538, 0.602391996895503 },
                new [] { 0.132636503661239, 0.226037213669723, 0.510971104835988, 0.60207862813248 },
                new [] { 0.149826330162449, 0.216588150849373, 0.528907620819483, 0.591580109501386 },
                new [] { 0.136313272447309, 0.22667687770382, 0.502097667393858, 0.59296285316854 },
                new [] { 0.155300636158424, 0.217541555509953, 0.520043815480292, 0.583082835769679 },
                new [] { 0.13836679394014, 0.229962639077062, 0.490595611285266, 0.581812490861237 },
                new [] { 0.155342627231723, 0.215244067158376, 0.513119055428009, 0.572324781327997 },
                new [] { 0.139105306983915, 0.230326564599771, 0.483129672732402, 0.571631608868675 },
                new [] { 0.15855493610933, 0.220381699475389, 0.501855512624674, 0.56283596089325 },
                new [] { 0.141550159430954, 0.231240291063691, 0.472572619923613, 0.560969288875668 },
                new [] { 0.209658149142218, 0.156495839398248, 0.573997899527394, 0.52655088671285 },
                new [] { 0.193860822471983, 0.164730926708318, 0.551377336827436, 0.526940112036227 },
                new [] { 0.203326347158319, 0.161261932441504, 0.559722690716708, 0.516551356624156 },
                new [] { 0.187380254474593, 0.169833952871436, 0.532362546655182, 0.514447505218595 },
                new [] { 0.198401186747981, 0.165048623701994, 0.541156375738473, 0.505978639685216 },
                new [] { 0.179274285098731, 0.173734887844814, 0.514311817560915, 0.504806155480818 },
                new [] { 0.196456816159752, 0.169691402774574, 0.522348593313766, 0.496844821481406 },
                new [] { 0.176637301495305, 0.175512924539237, 0.496641811502824, 0.497100738137824 },
                new [] { 0.192995572150811, 0.17361547103724, 0.507612122080527, 0.488110016641929 },
                new [] { 0.174175467966551, 0.182915220299203, 0.477491706077504, 0.486888943794725 },
                new [] { 0.193407914125075, 0.171963878530446, 0.498823105018713, 0.475921177737623 },
                new [] { 0.176354131095826, 0.18244792428899, 0.469249637838943, 0.477003652307594 },
                new [] { 0.198522931737231, 0.174471229758014, 0.491428495608099, 0.468097059526656 },
                new [] { 0.176268522860975, 0.183323871349937, 0.462072057815055, 0.470208782247814 },
                new [] { 0.197635837443444, 0.173346105245995, 0.483302354942222, 0.455621368406829 },
                new [] { 0.177208171445939, 0.184280562834214, 0.453476646856205, 0.457351270198367 },
                new [] { 0.199380504798212, 0.173947515446299, 0.474001980128001, 0.446764616631868 },
                new [] { 0.177845919545406, 0.182189616067993, 0.443176233105143, 0.447400072345813 },
                new [] { 0.25222957658575, 0.127024274874355, 0.538828847050133, 0.415870620238407 },
                new [] { 0.23367124077966, 0.133803107100177, 0.513066752029593, 0.41440437188184 },
                new [] { 0.246067572789988, 0.130306618318163, 0.525569217365019, 0.406192764529876 },
                new [] { 0.227617962982688, 0.133649048348641, 0.500596063332833, 0.404950718361639 },
                new [] { 0.242683291931999, 0.130770226641961, 0.506997523473551, 0.394890212015247 },
                new [] { 0.221640630723769, 0.137447799223399, 0.479475181453221, 0.394566886460121 },
                new [] { 0.239344520275643, 0.132325901139677, 0.490214721495919, 0.385886983671536 },
                new [] { 0.215925825373711, 0.141051904139003, 0.463419930686663, 0.38676350291729 },
                new [] { 0.230591108461092, 0.136987364311326, 0.47032994382422, 0.37489988210837 },
                new [] { 0.21064938261476, 0.14286007301965, 0.442166005232155, 0.377084896534891 },
                new [] { 0.233945816108106, 0.13534126374614, 0.467798282575071, 0.365743830723717 },
                new [] { 0.20962190904939, 0.141070496509428, 0.434464127546501, 0.365746891423888 },
                new [] { 0.234164431505953, 0.135168618343582, 0.456837879512947, 0.355713653523111 },
                new [] { 0.210468497312838, 0.142276655177483, 0.427566426994583, 0.358300107016585 },
                new [] { 0.235292193721944, 0.134968397125079, 0.448882781798927, 0.346526600998614 },
                new [] { 0.212291743798593, 0.141359167386565, 0.417162746676338, 0.344952255038237 },
                new [] { 0.290390840964504, 0.0975797705348479, 0.507311245051535, 0.316729065015926 },
                new [] { 0.267746774431529, 0.10186156696231, 0.479234560227323, 0.315285047549181 },
                new [] { 0.28432871066077, 0.100261903739642, 0.49063026358722, 0.308308215329027 },
                new [] { 0.260870451617112, 0.104941040236165, 0.462721017716657, 0.307598215894756 },
                new [] { 0.276782069078142, 0.100514411692659, 0.474115862746129, 0.29722638000635 },
                new [] { 0.253284402164999, 0.105888141709037, 0.447303932371323, 0.29737876802097 },
                new [] { 0.272727646691019, 0.10116579458321, 0.457485474756586, 0.286264100264542 },
                new [] { 0.247051597051597, 0.106146601146601, 0.4247455076569, 0.288236809283661 },
                new [] { 0.263850811057802, 0.104956101700447, 0.439135016465423, 0.276850669998241 },
                new [] { 0.237097617142204, 0.108751571967532, 0.406975413012082, 0.278522509072431 },
                new [] { 0.266966971890447, 0.101855115709051, 0.432660704057805, 0.26728438898977 },
                new [] { 0.240446613669382, 0.107246045608136, 0.398672839506173, 0.269319510659351 },
                new [] { 0.266862753052862, 0.101533530772039, 0.424552406818425, 0.257328093770726 },
                new [] { 0.23670578285231, 0.105804809420414, 0.392067746400642, 0.257231409602323 },
                new [] { 0.322899176461182, 0.0745404795536722, 0.47723725630241, 0.232253027648717 },
                new [] { 0.295919955439786, 0.0794547926803296, 0.45069083253724, 0.231983558797624 },
                new [] { 0.313255607759299, 0.0740050215029706, 0.46388985263773, 0.222998679302115 },
                new [] { 0.290128221088012, 0.0783808456782439, 0.434881116832015, 0.222505659968096 },
                new [] { 0.305828982632713, 0.0760546180262435, 0.44663671522611, 0.211851855186509 },
                new [] { 0.278560147945699, 0.0779128855139231, 0.416331815340655, 0.213125769711353 },
                new [] { 0.299663080982342, 0.0748122589811244, 0.429156632929614, 0.20439759662551 },
                new [] { 0.271037398908001, 0.0784186542961534, 0.395822398562393, 0.203143384682222 },
                new [] { 0.291448409784436, 0.0726163084806057, 0.411396139349748, 0.191597522496202 },
                new [] { 0.25884398511513, 0.0776986951364176, 0.375212112153477, 0.192937076754504 },
                new [] { 0.291561326427923, 0.0712672737537125, 0.403743315508021, 0.183139443022432 },
                new [] { 0.263718393896415, 0.0767064443031578, 0.368833470806058, 0.183915616909552 },
                new [] { 0.349083721050678, 0.0542214434306727, 0.454430582498354, 0.159414639098195 },
                new [] { 0.321424768395272, 0.056764660108287, 0.424393213520023, 0.160825305886393 },
                new [] { 0.337785882469168, 0.0536940378377488, 0.440876656472987, 0.151462056525972 },
                new [] { 0.311464252621912, 0.0559723828032111, 0.405227602306087, 0.150964067600176 },
                new [] { 0.328129831242168, 0.0531212875718075, 0.418240834353217, 0.140174672489083 },
                new [] { 0.298176131286051, 0.0545332683402388, 0.387283514374209, 0.140031133026202 },
                new [] { 0.32134382212398, 0.0503364431297338, 0.401545829078489, 0.130217373708304 },
                new [] { 0.288117142760327, 0.0514426823896174, 0.366354880367328, 0.13053495294752 },
                new [] { 0.308317107546417, 0.049349474936251, 0.383161374247122, 0.120039260740164 },
                new [] { 0.277915723958801, 0.0515392343442429, 0.347539574529045, 0.121278967166674 },
                new [] { 0.36666023748826, 0.036671474573997, 0.432694642770411, 0.103472286022368 },
                new [] { 0.335527660792411, 0.0383652941898025, 0.400687853257972, 0.101346602380729 },
                new [] { 0.358514382514021, 0.034106047398977, 0.415195873165777, 0.0923731623240668 },
                new [] { 0.326605332331956, 0.0361842642328854, 0.381218488876353, 0.090567882190454 },
                new [] { 0.343200837669137, 0.0314247680581823, 0.397910102641496, 0.0819164790705376 },
                new [] { 0.312073650251603, 0.0331926875118003, 0.358392270497362, 0.0819602449749862 },
                new [] { 0.334487268565823, 0.0284955245841337, 0.377856674290375, 0.0732075065291726 },
                new [] { 0.299176102958942, 0.0311840600486446, 0.341712205636398, 0.0722084735028992 },
                new [] { 0.381173370001146, 0.0210062368020429, 0.413789010989011, 0.0565877581816467 },
                new [] { 0.347730705908312, 0.0227862925810886, 0.383322724950021, 0.0563799235696708 },
                new [] { 0.366807568275316, 0.0182821857799834, 0.395878601275566, 0.0464786313972509 },
                new [] { 0.336312472590836, 0.0187600500268001, 0.363705324065907, 0.0471559616501772 },
                new [] { 0.35840784754475, 0.0137348561228369, 0.376278885164932, 0.0368164235215157 },
                new [] { 0.320106615525886, 0.0149237357690214, 0.341620050213628, 0.0359681991754679 },
                new [] { 0.373338641016475, 0.00892015728073311, 0.38387506729386, 0.0235429362262551 },
                new [] { 0.339810737298838, 0.00900171615451867, 0.351873572442743, 0.0232716360883827 },
                new [] { 0.359006005082469, 0.00477088425244007, 0.368635275339186, 0.0135720920631644 },
                new [] { 0.324614555432155, 0.00485072189194486, 0.333218773683005, 0.0129747018963541 },
                new [] { 0.357201949826835, 0.000109495267371221, 0.359537282744319, 0.00382207387111601 },
                new [] { 0.31953474814377, 0.000145745447479009, 0.321693569963435, 0.00349549808601262 }
            },
#endregion
#region 2 Opponents
            new double[][] {
                new [] { 2.07165867351695E-05, 0.263768761458862, 0.735692547709819, 0.999212918903713 },
                new [] { 0.00171544419608047, 0.30100327493892, 0.688113819712382, 0.989739848967707 },
                new [] { 0.00340167753960857, 0.333400642021332, 0.649185297461159, 0.979240364367398 },
                new [] { 0.00510983019161863, 0.362424053590902, 0.614665890294267, 0.969783699950276 },
                new [] { 0.00676036383795659, 0.392650350081104, 0.577728984365728, 0.960439677775273 },
                new [] { 0.00828583272878018, 0.423131930770028, 0.536909375066904, 0.949898869227858 },
                new [] { 0.00985292605163015, 0.452555795536357, 0.500522416413374, 0.941184910623543 },
                new [] { 0.0122037731969362, 0.477895440291986, 0.467307773065132, 0.931605830322661 },
                new [] { 0.0132843385183885, 0.502833372900775, 0.434101986269063, 0.92219798975915 },
                new [] { 0.0144488416192596, 0.523275826531454, 0.404420726860341, 0.911795945799978 },
                new [] { 0.0166218730618152, 0.55067707256564, 0.373112901731963, 0.902975687768414 },
                new [] { 0.0175094838290746, 0.57456625009968, 0.337534325177261, 0.895060474563752 },
                new [] { 0.0191389040784953, 0.596834600662381, 0.307243963363863, 0.885681680674283 },
                new [] { 0.045159459180514, 0.415569202188048, 0.513184111238289, 0.879180527238259 },
                new [] { 0.0421017770096147, 0.435967626755534, 0.482467142360269, 0.87911473458271 },
                new [] { 0.0492992811277891, 0.41498013506084, 0.491778637078175, 0.86074276173307 },
                new [] { 0.0446279898106591, 0.437546177343052, 0.470473262063952, 0.86059891903591 },
                new [] { 0.052735790590435, 0.413246709593387, 0.479902932169085, 0.843190728816695 },
                new [] { 0.0471001739274474, 0.434036980288223, 0.4537870610059, 0.842092156662726 },
                new [] { 0.0561714053530313, 0.410484148234583, 0.471568689289769, 0.82329204428609 },
                new [] { 0.0507822651904985, 0.429409012942461, 0.445107925003939, 0.823915080544564 },
                new [] { 0.0582103962708803, 0.415747017846205, 0.447172047866336, 0.806111389027847 },
                new [] { 0.0500752854573592, 0.439484085490819, 0.416422348366706, 0.805597561411238 },
                new [] { 0.0617289157896957, 0.413206234051079, 0.433649782441238, 0.790017704017728 },
                new [] { 0.0520382791115569, 0.436856181700412, 0.401929510217186, 0.787811027403428 },
                new [] { 0.0642908530318602, 0.410930113052415, 0.42616681816757, 0.771951190319453 },
                new [] { 0.0565102341780757, 0.432716946794825, 0.390958468776288, 0.770672802511416 },
                new [] { 0.0669356260565644, 0.409350660125906, 0.414963081761661, 0.752397317386954 },
                new [] { 0.0561451877034058, 0.430582906656164, 0.381289259282294, 0.753252265824904 },
                new [] { 0.0744032956320297, 0.394118386985189, 0.415824968632371, 0.736149020734569 },
                new [] { 0.0636187247470709, 0.418049914180019, 0.382762098892555, 0.735444524645518 },
                new [] { 0.0789484728619543, 0.388305406624768, 0.405743664312422, 0.719688690580415 },
                new [] { 0.0657521584774069, 0.413482343549776, 0.373973590188253, 0.718548401534908 },
                new [] { 0.0807017359616051, 0.386605895758289, 0.397704381400034, 0.701505571872065 },
                new [] { 0.068879504854168, 0.408365935953963, 0.366650527443547, 0.70169692115503 },
                new [] { 0.0832986969804028, 0.381226750348095, 0.389084085283079, 0.685730329024396 },
                new [] { 0.0709855736820817, 0.402370852822518, 0.352127516012166, 0.684209426709009 },
                new [] { 0.141550599207949, 0.286916689931859, 0.471747176814755, 0.614502925377425 },
                new [] { 0.128481065076506, 0.300215334285773, 0.445143652963326, 0.614304812834225 },
                new [] { 0.144223532363271, 0.283413823381836, 0.459596699891113, 0.601782975691635 },
                new [] { 0.131366806713993, 0.297529854916489, 0.432272919754393, 0.598745612038291 },
                new [] { 0.146378956364053, 0.282164184548685, 0.450234540686802, 0.584580119474024 },
                new [] { 0.132223522781463, 0.296310746954524, 0.422075763713606, 0.58428226363009 },
                new [] { 0.141104101607528, 0.283790581183325, 0.42412326208757, 0.571057680777072 },
                new [] { 0.124627106135728, 0.300134085877327, 0.392058541018994, 0.568020682126276 },
                new [] { 0.137315443195358, 0.289747707610216, 0.403336858914536, 0.555882477841747 },
                new [] { 0.122013971184432, 0.302469905819248, 0.369145649725686, 0.555373099443243 },
                new [] { 0.138506681676986, 0.28405987449705, 0.392747391053542, 0.541213323144476 },
                new [] { 0.122708810664445, 0.302306811081025, 0.359998969974764, 0.539661726494081 },
                new [] { 0.138691159586682, 0.283968270535435, 0.383355674013113, 0.526515735052163 },
                new [] { 0.122411107277651, 0.298346036027559, 0.350595397239989, 0.525568801337501 },
                new [] { 0.142162799924823, 0.273931338359054, 0.375499598234318, 0.511546649667432 },
                new [] { 0.1189885506088, 0.293973239772881, 0.338459617505671, 0.512541674719753 },
                new [] { 0.143851026988916, 0.270029135172686, 0.368039052292625, 0.498985068154029 },
                new [] { 0.123233781173355, 0.290142816834154, 0.331795028524857, 0.496010761379974 },
                new [] { 0.144858202329347, 0.26727946936857, 0.360672008646941, 0.485134288433964 },
                new [] { 0.121676583173837, 0.284706041441292, 0.321907570080062, 0.484828539856407 },
                new [] { 0.142629976434277, 0.264462680653167, 0.351008770570552, 0.468881897705918 },
                new [] { 0.121752623330254, 0.278145660996191, 0.31388300105469, 0.4694780398487 },
                new [] { 0.216072384511226, 0.1888074819721, 0.442028833990235, 0.414159340030251 },
                new [] { 0.198134978077076, 0.199613988713366, 0.413736509156634, 0.416101669423974 },
                new [] { 0.215530080550428, 0.187243035744252, 0.432500546744009, 0.402077151335312 },
                new [] { 0.195912456081646, 0.197773757737996, 0.4022285791001, 0.403081442402806 },
                new [] { 0.206781466171756, 0.188712670828412, 0.408422459893048, 0.388485240869961 },
                new [] { 0.184549221167462, 0.197676614045209, 0.378428739473054, 0.389558958062497 },
                new [] { 0.200791896676338, 0.189049619240156, 0.385871919150253, 0.38024596140057 },
                new [] { 0.177271055535123, 0.201717759556931, 0.354902041828553, 0.377594635092977 },
                new [] { 0.19253992379604, 0.191259447417185, 0.365533905711789, 0.366563204065002 },
                new [] { 0.165688475797608, 0.200715367343742, 0.32923010805501, 0.365877475707217 },
                new [] { 0.192217339779556, 0.1890658920013, 0.357418654431119, 0.353727768527233 },
                new [] { 0.168489868208574, 0.198348700317758, 0.321173773592664, 0.35516953579556 },
                new [] { 0.190699122797758, 0.183241320342436, 0.350444351267603, 0.3434867611209 },
                new [] { 0.165764546684709, 0.195859688873504, 0.312078504220006, 0.340504811295303 },
                new [] { 0.192614320884186, 0.181163899219528, 0.341946138211382, 0.332093774773521 },
                new [] { 0.165567034024133, 0.189887910663126, 0.303787394949464, 0.331244534242031 },
                new [] { 0.189901908440899, 0.175601687551689, 0.334031467102575, 0.319286735637985 },
                new [] { 0.163088139235972, 0.186908700141175, 0.296368843655705, 0.321346733305667 },
                new [] { 0.187429130213156, 0.171523089246518, 0.326811909655082, 0.307778121343301 },
                new [] { 0.162365775767838, 0.180364146858992, 0.28842069236267, 0.309052325002489 },
                new [] { 0.271150746191046, 0.120638552337372, 0.419812437590719, 0.270930127723516 },
                new [] { 0.247978099986469, 0.127106471120918, 0.389341728323819, 0.267825418927354 },
                new [] { 0.256550594275339, 0.119997704291931, 0.395450045375794, 0.25917798006277 },
                new [] { 0.233478410262284, 0.125904561593486, 0.364479716730116, 0.258335942391985 },
                new [] { 0.246730269407012, 0.118827176601985, 0.377476666120845, 0.24900274396509 },
                new [] { 0.220899359848882, 0.130590827998741, 0.340416472353454, 0.24882751433038 },
                new [] { 0.236017755898472, 0.120835831571418, 0.356979903879302, 0.23816018378765 },
                new [] { 0.209071912004741, 0.126243671182176, 0.318959321546644, 0.238116645687227 },
                new [] { 0.22693097802851, 0.122204031408699, 0.33694629637204, 0.230336336732918 },
                new [] { 0.196289693709472, 0.129193511394675, 0.299296310191335, 0.229514171259902 },
                new [] { 0.223862645616025, 0.117795354993225, 0.32837088879, 0.222290350251906 },
                new [] { 0.197357328145266, 0.125426934716818, 0.294410050616064, 0.222647553554643 },
                new [] { 0.222544108265267, 0.114796531997448, 0.31842626950643, 0.2106473558746 },
                new [] { 0.191632478810471, 0.120020200337366, 0.285205887083802, 0.212338337950809 },
                new [] { 0.219281328510221, 0.109916915988199, 0.312727806577029, 0.203461526914648 },
                new [] { 0.18950160284858, 0.116318773690311, 0.276784535883613, 0.202937495843737 },
                new [] { 0.218020424194815, 0.105446451950772, 0.305344624699528, 0.193411461015943 },
                new [] { 0.185474481911899, 0.110507821595648, 0.265960098507433, 0.194414254489246 },
                new [] { 0.300029151787109, 0.0744203479474018, 0.387779101534417, 0.166585105033259 },
                new [] { 0.269876825583347, 0.0780122125230821, 0.35761407366685, 0.164670312623568 },
                new [] { 0.287057690092468, 0.0729111034316652, 0.370379477092042, 0.159275219199808 },
                new [] { 0.255728314238953, 0.0763585323913899, 0.339487957017696, 0.157981832898642 },
                new [] { 0.273233947597247, 0.0738373859386295, 0.346965819777006, 0.150667432523798 },
                new [] { 0.239011196241339, 0.077495447149215, 0.311968712955552, 0.14809483384392 },
                new [] { 0.258237627897265, 0.0728126957611192, 0.329975657700417, 0.142378127418107 },
                new [] { 0.230159809944428, 0.0763675942690291, 0.291503852296813, 0.143658864448771 },
                new [] { 0.246871118255551, 0.0723426686569034, 0.311492526565924, 0.136381382553802 },
                new [] { 0.213144720716229, 0.0761668847759706, 0.271315380372098, 0.134892895041337 },
                new [] { 0.243983838888715, 0.0687836472879114, 0.304287934444758, 0.12845943591746 },
                new [] { 0.211824076979395, 0.072659763623052, 0.262820704876612, 0.129043587896254 },
                new [] { 0.239081490129042, 0.0648491734245033, 0.295620253675877, 0.12050054687788 },
                new [] { 0.203179987952189, 0.0707703201124462, 0.254914384928554, 0.121561718914687 },
                new [] { 0.23531746860832, 0.0609970123059566, 0.287268404738737, 0.112617875073391 },
                new [] { 0.201385476028651, 0.0662728080723584, 0.251603851276472, 0.114047383712223 },
                new [] { 0.309616468084218, 0.0428917221375957, 0.364430414661804, 0.0943203895138273 },
                new [] { 0.278892512378249, 0.0449625673084215, 0.325708903935244, 0.0952830246666953 },
                new [] { 0.295931538187084, 0.042825361512792, 0.339563595182964, 0.0899769876745503 },
                new [] { 0.264166945887083, 0.0451132936840342, 0.30867979870019, 0.0889758604206501 },
                new [] { 0.281897348628094, 0.0415511155930921, 0.321498054474708, 0.0834122043852305 },
                new [] { 0.247272234160182, 0.0430131642083742, 0.286927751248861, 0.0840855307131373 },
                new [] { 0.268378692333223, 0.0406208513109857, 0.301281791468234, 0.0780173444976077 },
                new [] { 0.233406104370461, 0.0425868004534195, 0.267794038373231, 0.0780324731596938 },
                new [] { 0.251504005486113, 0.0389223112330247, 0.285662103198431, 0.0733595839971144 },
                new [] { 0.216496460121546, 0.0415544138838419, 0.245700233543126, 0.0714619981281048 },
                new [] { 0.247482413533205, 0.0367169835022192, 0.279446762324873, 0.0674594859944564 },
                new [] { 0.212275461697026, 0.0386290017247907, 0.24257639509545, 0.0674060617649871 },
                new [] { 0.241605250191326, 0.0326983760889849, 0.271247619920375, 0.0618971641880908 },
                new [] { 0.204748891409714, 0.0358100443436114, 0.233283517092579, 0.0620339958284385 },
                new [] { 0.31320942408377, 0.0229633507853403, 0.3411620352928, 0.0497921316880351 },
                new [] { 0.280717767866404, 0.0250285240120319, 0.305830079304376, 0.0502180006228589 },
                new [] { 0.298742890416229, 0.0225895001715658, 0.323252164228219, 0.0462515389887252 },
                new [] { 0.266725661884949, 0.0237015876958745, 0.287628865979381, 0.0463231642744748 },
                new [] { 0.28146065654296, 0.0211636340833795, 0.304030696552214, 0.0408172440043398 },
                new [] { 0.247876471932047, 0.0217648646964757, 0.265367913026831, 0.0414055398746591 },
                new [] { 0.264976287993102, 0.0196216857081268, 0.284721661960468, 0.0367341656704093 },
                new [] { 0.229103915662651, 0.0206450803212851, 0.246261617284931, 0.0376155773309222 },
                new [] { 0.249363219561895, 0.0183080873714743, 0.264959467226924, 0.0341283241874209 },
                new [] { 0.210767450587941, 0.0203955049620549, 0.226822500049377, 0.0333375046182083 },
                new [] { 0.245699484043168, 0.0161249645345355, 0.260536188760735, 0.0305506079145048 },
                new [] { 0.206116287543595, 0.0177502472541773, 0.216683656805788, 0.0304188779946628 },
                new [] { 0.312082353186086, 0.0108458063575994, 0.319463390170512, 0.0223814807040691 },
                new [] { 0.27653028566709, 0.0118571974272411, 0.28768303615898, 0.0230365958263603 },
                new [] { 0.293079769779949, 0.00971306362502228, 0.304242205223956, 0.0201264237581251 },
                new [] { 0.256903835348469, 0.010462406093272, 0.26752710325239, 0.0209434209273541 },
                new [] { 0.27435985196911, 0.00885083534772285, 0.281760019560301, 0.0179585019821812 },
                new [] { 0.237159103856153, 0.00960672004308782, 0.248282770463652, 0.0171297424890152 },
                new [] { 0.256652279584311, 0.00791464130070399, 0.266127427570837, 0.0148669052346249 },
                new [] { 0.220785161773268, 0.00846814438991932, 0.227509695154877, 0.0151032532837049 },
                new [] { 0.240320489550381, 0.00653943888399108, 0.246869107869406, 0.0132953070483585 },
                new [] { 0.203841432594411, 0.00717947659965134, 0.205259416507615, 0.0128673180858298 },
                new [] { 0.302485410587745, 0.00437682367653189, 0.305111598246313, 0.00831169447112249 },
                new [] { 0.267213916881819, 0.00448696593653702, 0.266617526124717, 0.00892926238339872 },
                new [] { 0.281762221005774, 0.00337220231148201, 0.290198298580146, 0.00684906977993446 },
                new [] { 0.244716453244377, 0.00401377380824276, 0.248584287810595, 0.00732012032409878 },
                new [] { 0.266713905928355, 0.00310848134393975, 0.267644777028155, 0.00554431596371425 },
                new [] { 0.224311688311688, 0.00337142857142857, 0.228800775778472, 0.00552968916444242 },
                new [] { 0.245050509253961, 0.00226277296324424, 0.252165486690662, 0.004105627685021 },
                new [] { 0.208018892140254, 0.00240772113558191, 0.210244991034537, 0.00413879498429596 },
                new [] { 0.292646575117178, 0.00124372995641806, 0.290017040188937, 0.00255209165054975 },
                new [] { 0.255440741430315, 0.00129812366825958, 0.256481646914021, 0.00240699307357247 },
                new [] { 0.275082090757388, 0.000608279054745115, 0.274656747171755, 0.00175578465620702 },
                new [] { 0.234800588698644, 0.000891339496704117, 0.233544459174154, 0.00158716924048145 },
                new [] { 0.254868645300744, 0.00044685538512699, 0.25667731881485, 0.0010098370004277 },
                new [] { 0.21691301262073, 0.000565721397640427, 0.218131766040467, 0.00104713603818616 },
                new [] { 0.266059394513942, 0.000103043917317561, 0.267843028770155, 0.000387539567494009 },
                new [] { 0.227196392473954, 0.000155496812315348, 0.227908635293428, 0.000349468628470044 },
                new [] { 0.249720334769639, 3.62529002320186E-05, 0.247922410397794, 0.000151125440041722 },
                new [] { 0.20805414098097, 5.15432035131848E-05, 0.208604770827468, 0.000106688162355675 },
                new [] { 0.24022024391499, 0, 0.242090729783037, 5.9127164793321E-06 },
                new [] { 0.200090617952652, 0, 0.200695571077455, 1.77610429284408E-05 }
    },
#endregion
#region 3 Opponents
    new double[][] {
        new [] { 3.73083284625238E-05, 0.36104513064133, 0.63788879625473, 0.998672749589624 },
new [] { 0.0025413357310555, 0.403860082921225, 0.582482399949261, 0.983761971536832 },
new [] { 0.00532949360460767, 0.437074575510509, 0.537902225785037, 0.969824767396926 },
new [] { 0.00705489355337289, 0.469262601585481, 0.489511694078318, 0.955350097518127 },
new [] { 0.00963574623779869, 0.494875136266242, 0.452621968406476, 0.940595122601477 },
new [] { 0.0122501234760456, 0.524760196521875, 0.416512396094232, 0.927231291074836 },
new [] { 0.0141521243834656, 0.548965974812849, 0.377722490083842, 0.913618932509662 },
new [] { 0.0161909510158295, 0.57218621463293, 0.342374748090172, 0.89843110403239 },
new [] { 0.0181509254150574, 0.586773336008427, 0.31205319747094, 0.885924720912184 },
new [] { 0.020553818218501, 0.602668213457077, 0.288348311124884, 0.872318828768088 },
new [] { 0.0221164683782091, 0.618315591734502, 0.262912010781066, 0.861389716840537 },
new [] { 0.0237315156536366, 0.630357008360322, 0.239334325232821, 0.847163964072126 },
new [] { 0.0255341813396829, 0.637922175574911, 0.21989948204723, 0.832539274237442 },
new [] { 0.0602238787239917, 0.468608036543433, 0.4144798388064, 0.821967437977099 },
new [] { 0.0554383248635893, 0.490054814019403, 0.385395370696745, 0.823767814726841 },
new [] { 0.0632106101061763, 0.459058829428503, 0.398619704902427, 0.79918866364185 },
new [] { 0.0550828369788278, 0.48418339256219, 0.371330861822192, 0.799505687570722 },
new [] { 0.0658048541370772, 0.453978708161034, 0.38341064539796, 0.77129045374456 },
new [] { 0.0590555111821086, 0.478702825479233, 0.358882379660148, 0.773921165840516 },
new [] { 0.0703537067398737, 0.446416542748567, 0.373313009321686, 0.751287490910726 },
new [] { 0.0610603950052236, 0.46587234465947, 0.342647623256842, 0.749767434881767 },
new [] { 0.070737029558936, 0.446249639448701, 0.344867025578225, 0.724594566133109 },
new [] { 0.0582303391912557, 0.47133294893329, 0.310901071932952, 0.721433760475723 },
new [] { 0.0734110919947374, 0.434495496407246, 0.335941311772496, 0.700689613995283 },
new [] { 0.0607292389098662, 0.460648326526419, 0.299319135092428, 0.701703076650024 },
new [] { 0.0757716088012216, 0.430298630754212, 0.323427177090767, 0.675484357275061 },
new [] { 0.0624922304268914, 0.451959175555058, 0.287374278741989, 0.676862946307307 },
new [] { 0.0794881445238991, 0.418692761259566, 0.313263153039929, 0.650388590357563 },
new [] { 0.0647416337351808, 0.441511180031014, 0.274509020324275, 0.652071767399404 },
new [] { 0.0864826592153342, 0.397548393622536, 0.320730592550594, 0.627867268765024 },
new [] { 0.0705578849581963, 0.421062542368626, 0.280678970152654, 0.627756480671942 },
new [] { 0.0893315792770782, 0.384620685334837, 0.310350923212025, 0.607217459663834 },
new [] { 0.072743092504726, 0.405110357174155, 0.271643968871595, 0.610019800500616 },
new [] { 0.091202951106665, 0.376659997499062, 0.302746056073474, 0.582959290187891 },
new [] { 0.0766679989009068, 0.395523692953314, 0.264538191850106, 0.584991433981546 },
new [] { 0.0952972325537414, 0.362863266721301, 0.297069555302167, 0.56229195496877 },
new [] { 0.0755570716887865, 0.381280331604083, 0.25683888870165, 0.564073979453593 },
new [] { 0.171048024012006, 0.265120060030015, 0.381220111154194, 0.478049111484729 },
new [] { 0.153639100186108, 0.278575086496546, 0.353287848352613, 0.478279150086343 },
new [] { 0.16973936761289, 0.259817910900091, 0.36930566621362, 0.458517923380739 },
new [] { 0.149484729953362, 0.271475853768617, 0.339768965437415, 0.460054982611374 },
new [] { 0.168785551225295, 0.256467012246648, 0.35802634023971, 0.443501956609743 },
new [] { 0.150121263147313, 0.267218738611945, 0.3242165341113, 0.440175709278289 },
new [] { 0.160391594673105, 0.256542297003307, 0.330838496393564, 0.422688937952987 },
new [] { 0.139277643915654, 0.270215608184871, 0.296515412816794, 0.426093474757318 },
new [] { 0.154476774250508, 0.25344003430575, 0.309603497148321, 0.408554927204909 },
new [] { 0.131580265151041, 0.264193342430426, 0.271470209834796, 0.406743088334457 },
new [] { 0.157035413280925, 0.243408040112375, 0.302708063425093, 0.390490108929836 },
new [] { 0.130763619610704, 0.256753816818316, 0.263343138135441, 0.39212040144123 },
new [] { 0.15257096104223, 0.237673862420543, 0.290763130006723, 0.378013442867812 },
new [] { 0.127578395889752, 0.251670420895447, 0.253657518640091, 0.376367458797913 },
new [] { 0.155171114126338, 0.228434845972159, 0.280718994620456, 0.359393320473543 },
new [] { 0.127353474698192, 0.242290611121536, 0.245128165016649, 0.360731130287084 },
new [] { 0.151605824855376, 0.222639886295631, 0.275744133283668, 0.345076642117238 },
new [] { 0.126479703297262, 0.2325229893817, 0.237999465157221, 0.344063451129736 },
new [] { 0.150816042510121, 0.213410931174089, 0.270821885548316, 0.328995778045838 },
new [] { 0.126053572768427, 0.223419953480229, 0.231036581991256, 0.330874517867059 },
new [] { 0.154077569311813, 0.203060829619643, 0.26208662782245, 0.315501535734937 },
new [] { 0.122562197255966, 0.214716847150719, 0.220605445892174, 0.312883227624026 },
new [] { 0.238800308520996, 0.140674194241784, 0.35843290123172, 0.259208751798805 },
new [] { 0.217147258083465, 0.148086989439607, 0.327414707765481, 0.260704013469168 },
new [] { 0.235094927563547, 0.139627074773518, 0.348294123755351, 0.250244390302593 },
new [] { 0.211200581067712, 0.142768587279125, 0.315503286374317, 0.249510160877431 },
new [] { 0.216349289979427, 0.138146670680917, 0.317526415355511, 0.237828518945126 },
new [] { 0.192535051155741, 0.144966527725148, 0.285675519826329, 0.236813725490196 },
new [] { 0.205297721640017, 0.13663240529898, 0.300002263954449, 0.226430414326908 },
new [] { 0.179102236901366, 0.142383525997098, 0.262967929377584, 0.226738187004947 },
new [] { 0.195592680590628, 0.137319782557315, 0.279954190032383, 0.216909062475565 },
new [] { 0.16662487776747, 0.142642228518416, 0.241986305182876, 0.215860835318788 },
new [] { 0.195048842668551, 0.129433071661156, 0.271088737316418, 0.205516769747571 },
new [] { 0.165181445250868, 0.136194383086147, 0.231947839346983, 0.202463928300213 },
new [] { 0.19294136955863, 0.126186748813251, 0.262504304168657, 0.195938533213105 },
new [] { 0.158991614173742, 0.12963470140981, 0.223704927057528, 0.193663794768767 },
new [] { 0.187393877347183, 0.118276817818618, 0.257329926894279, 0.183168204593375 },
new [] { 0.155469645263251, 0.125391948218761, 0.216833700440529, 0.18390624065099 },
new [] { 0.188628158844765, 0.109619434416366, 0.247103836156673, 0.174705614221682 },
new [] { 0.153392536800723, 0.117113449280525, 0.211057629481204, 0.173713401070162 },
new [] { 0.185597309923507, 0.105498228861851, 0.242261976755693, 0.165577309046844 },
new [] { 0.148592290158555, 0.113656288355084, 0.205253779638598, 0.163896239003098 },
new [] { 0.277931251970987, 0.0710186061179439, 0.337750436540759, 0.133990082593161 },
new [] { 0.24881913567029, 0.0740071292715799, 0.307673186219676, 0.132739682920251 },
new [] { 0.25619335347432, 0.0698577542799597, 0.311332524494094, 0.127317087909598 },
new [] { 0.227455646366989, 0.073027758397289, 0.278855568458683, 0.125194178417917 },
new [] { 0.241925784871339, 0.0685901130014282, 0.292439348246838, 0.118759649056746 },
new [] { 0.209571837584591, 0.0730153565851119, 0.256747936901259, 0.119841959882124 },
new [] { 0.227950161885685, 0.0678975017952304, 0.274108278123536, 0.113089328158378 },
new [] { 0.195733222866611, 0.0727134643870352, 0.23619070710633, 0.112510356255178 },
new [] { 0.21453007518797, 0.0650689223057644, 0.253668600330535, 0.104324996022034 },
new [] { 0.178373043489264, 0.0681441459680379, 0.215551277984973, 0.106843617437348 },
new [] { 0.210155524752979, 0.0634879255312385, 0.248227661851257, 0.0992767245739998 },
new [] { 0.17606336613651, 0.0649641860053816, 0.208323838666623, 0.0995295323974245 },
new [] { 0.206386945803561, 0.0577690738678313, 0.239434053922759, 0.0935388911952297 },
new [] { 0.171885657984488, 0.0598994517848571, 0.200625597358589, 0.0938834740849896 },
new [] { 0.204072211894751, 0.0530235923795741, 0.236325639562947, 0.0865583953897392 },
new [] { 0.167332761794296, 0.0577863788799516, 0.193174667385832, 0.0852922859908311 },
new [] { 0.199741491818091, 0.050227135829736, 0.228159194239596, 0.0811106582740872 },
new [] { 0.160817071458655, 0.0529909512079946, 0.187628267807064, 0.0797666986499044 },
new [] { 0.278188234696727, 0.032732881407571, 0.307818939385339, 0.0631371431998081 },
new [] { 0.251070945438148, 0.0350593717120096, 0.280057220164563, 0.0631586940015186 },
new [] { 0.265015156660755, 0.0334075443693949, 0.289960671591074, 0.057627587651071 },
new [] { 0.231281292671825, 0.0345294087897638, 0.257311916043041, 0.0579854631914766 },
new [] { 0.249311071486465, 0.0319463321570632, 0.270219386259611, 0.0549270458870797 },
new [] { 0.214221965230727, 0.0332734152074717, 0.235447659528091, 0.0551124938346549 },
new [] { 0.231415083447718, 0.0308583377163162, 0.250670005711524, 0.0507819715534758 },
new [] { 0.196159225796696, 0.0320946584508373, 0.214188793122164, 0.0508092231760306 },
new [] { 0.219354309027295, 0.0289942024226042, 0.23416936764482, 0.045406849904506 },
new [] { 0.18102595565127, 0.0302440307010405, 0.19136997944977, 0.0463223857719271 },
new [] { 0.214049917471115, 0.0261779122692943, 0.229372236380666, 0.0429351893429127 },
new [] { 0.175080963020611, 0.0282742951823865, 0.189206937013553, 0.0420225089788482 },
new [] { 0.207814354425636, 0.0242551065025923, 0.223319943545761, 0.0394076742056796 },
new [] { 0.170295053334165, 0.0265672759029381, 0.183167685357428, 0.0378976953344576 },
new [] { 0.202345893427975, 0.0220053361524327, 0.216055287453918, 0.0349495252748902 },
new [] { 0.162978256259278, 0.0232095407991417, 0.174010762561661, 0.0361507361354794 },
new [] { 0.274432717349317, 0.0143487308569364, 0.285134261144513, 0.0273537851358863 },
new [] { 0.240154459580142, 0.0152384186760248, 0.249439185862012, 0.0268980477223427 },
new [] { 0.25789533189287, 0.0136927792573238, 0.266883131153305, 0.0239412142506328 },
new [] { 0.221693767275082, 0.0138760925321846, 0.232991977039984, 0.0243140644967301 },
new [] { 0.239540736080528, 0.0126706511481598, 0.24930406733977, 0.0210486060078885 },
new [] { 0.206094841867939, 0.0136520471842653, 0.212751160036078, 0.0216034078615218 },
new [] { 0.224248029800066, 0.0115868028279654, 0.232199216928233, 0.0196904924120479 },
new [] { 0.185353338447801, 0.0126205777671576, 0.191262379751258, 0.0195105646233466 },
new [] { 0.210241762088104, 0.0110005500275014, 0.213527191720827, 0.0177598491198659 },
new [] { 0.169526038165176, 0.0114159383565186, 0.174415529461547, 0.01834158724226 },
new [] { 0.204342601054042, 0.00993327741822415, 0.210431287413888, 0.0156534305760433 },
new [] { 0.164668220317747, 0.00950984814313637, 0.167599148649218, 0.0159439396549787 },
new [] { 0.197492241780056, 0.00797180656590404, 0.204060283108307, 0.0134143928902957 },
new [] { 0.160422102543669, 0.00875310085988612, 0.162440219169983, 0.013597157668931 },
new [] { 0.263324587706147, 0.00534107946026987, 0.269939261176939, 0.00934793377264744 },
new [] { 0.229171099235304, 0.00571957797774746, 0.230646531074431, 0.00946924365725227 },
new [] { 0.24718581726865, 0.00506615359628191, 0.250282578875171, 0.00867143753164414 },
new [] { 0.209776287196493, 0.00565248520933037, 0.213838345661681, 0.00868051664761734 },
new [] { 0.231094906944167, 0.00471532919751851, 0.233886230150098, 0.00736427726803275 },
new [] { 0.193757369068296, 0.00441827148598451, 0.194329292139226, 0.007580115347936 },
new [] { 0.212961461205445, 0.00419822090252392, 0.217892419954678, 0.00631175153931373 },
new [] { 0.174801500629545, 0.00378369350155459, 0.175452673810906, 0.00651717414129294 },
new [] { 0.195728671433384, 0.00311334057075831, 0.200651928042509, 0.00530051874098054 },
new [] { 0.154444290413238, 0.0031380357660462, 0.158903084295768, 0.00553567938908347 },
new [] { 0.192980599208029, 0.00280178317933903, 0.196181887950883, 0.00424537123620722 },
new [] { 0.150584592924475, 0.00274991504839095, 0.152697464233702, 0.0043330679923702 },
new [] { 0.247767940296283, 0.00152736213383666, 0.252090899086607, 0.00278270419483382 },
new [] { 0.214134340088699, 0.00173328467972829, 0.214341474890923, 0.00320306960837469 },
new [] { 0.23626880366913, 0.00141455936787268, 0.23514568808936, 0.00231194781603501 },
new [] { 0.197780873718778, 0.00134963677977908, 0.197143285435895, 0.00229610374043914 },
new [] { 0.218940339490764, 0.00111707438841737, 0.22064605534548, 0.00182298821555072 },
new [] { 0.180063681441768, 0.00123964861939641, 0.180349618434041, 0.00179347220675058 },
new [] { 0.202062848707749, 0.000956991668543121, 0.203023723618251, 0.00126613573874151 },
new [] { 0.161173628409418, 0.000957578640363504, 0.162797979376279, 0.00159528363918943 },
new [] { 0.186677642156169, 0.000598078431876937, 0.186750483558994, 0.00100596615310805 },
new [] { 0.143647177929638, 0.000790939002784105, 0.145127674937639, 0.00115324144646007 },
new [] { 0.235734110705745, 0.000261839509836438, 0.240048635376334, 0.000560215720400069 },
new [] { 0.200016328376206, 0.000420769694533762, 0.201555725305819, 0.000700447093889717 },
new [] { 0.22291501388338, 0.00024791749305831, 0.223790453161769, 0.000490281119651222 },
new [] { 0.184572282405678, 0.000336197235711617, 0.184786634077178, 0.000451085214097718 },
new [] { 0.208038852799066, 0.000155262144604951, 0.205541770057899, 0.00028357785704691 },
new [] { 0.16657641279542, 0.000206294562848928, 0.164898468376857, 0.000341663787477446 },
new [] { 0.191802628807869, 8.05242749718165E-05, 0.190944155803096, 0.000216598948374761 },
new [] { 0.148257568138389, 0.000113144926078648, 0.15005442388431, 0.000211537319636601 },
new [] { 0.227103620474407, 1.87265917602996E-05, 0.231010587515956, 0.000140343620274483 },
new [] { 0.188829587780916, 2.49981251406145E-05, 0.190186955688521, 6.32289930968817E-05 },
new [] { 0.212232774650875, 3.11996904990703E-05, 0.214557267894539, 2.99443783172757E-05 },
new [] { 0.174047450556818, 1.24151116739295E-05, 0.174204961836068, 3.35088202661345E-05 },
new [] { 0.196475293414069, 1.24591961326655E-05, 0.195863413605944, 2.60099283612259E-05 },
new [] { 0.154258231502451, 0, 0.156502112450546, 2.61897635438491E-05 },
new [] { 0.208776082217378, 0, 0.206939741643757, 0 },
new [] { 0.164589410961455, 0, 0.165185459156716, 7.48687924412467E-06 },
new [] { 0.190619843559094, 0, 0.189413318795605, 0 },
new [] { 0.151152125841793, 0, 0.149038112898714, 7.42384986006043E-06 },
new [] { 0.180784864756361, 0, 0.182682288897867, 0 },
new [] { 0.141715890991635, 0, 0.142311965108624, 0 }
    },
#endregion
#region 4 Opponents
    new double[][] {
        new [] { 2.9570488652325E-05, 0.44016411621202, 0.559300883018317, 0.998427302393038 },
new [] { 0.00313910729767706, 0.484244670991659, 0.4968180193064, 0.979049245919011 },
new [] { 0.00610745581683534, 0.516721741952616, 0.450958999305073, 0.959859492400474 },
new [] { 0.0092008178504756, 0.543248288736777, 0.403229364882381, 0.941464686667948 },
new [] { 0.0122824597338678, 0.569369741088401, 0.364783693954643, 0.921898865372027 },
new [] { 0.015382231862246, 0.58778569636876, 0.326176459018973, 0.905067169024517 },
new [] { 0.0174973727668518, 0.608317069509083, 0.294689880861073, 0.885717659669171 },
new [] { 0.020512357113812, 0.620175002635582, 0.268141604073197, 0.86824721414839 },
new [] { 0.022987738884194, 0.626310249225341, 0.246533555185801, 0.850685179322509 },
new [] { 0.0242722801142225, 0.633635833570051, 0.225165643278402, 0.833404415567917 },
new [] { 0.0263266775978056, 0.639824080175307, 0.207502923837029, 0.816810648056955 },
new [] { 0.028201471250563, 0.64052694790572, 0.19249460987122, 0.797849345669298 },
new [] { 0.0315764052405965, 0.636946809152931, 0.179374186338552, 0.784551434313905 },
new [] { 0.0684052855596257, 0.483656883536757, 0.355475024485798, 0.772101225881329 },
new [] { 0.0617437589024665, 0.509146112902017, 0.323933161953728, 0.77354392802506 },
new [] { 0.0715574398989671, 0.47427570550118, 0.336466626237081, 0.738594831861007 },
new [] { 0.0627818883943048, 0.498879816054005, 0.303664754213573, 0.741842943016902 },
new [] { 0.0739575373458594, 0.463658021306244, 0.324720554154236, 0.712137042555511 },
new [] { 0.0645978233582894, 0.486399681034865, 0.288518874121987, 0.711721441461147 },
new [] { 0.0781460220647592, 0.445712619965916, 0.313082804330403, 0.677448468880765 },
new [] { 0.0667706053428477, 0.467941414565406, 0.274784044715447, 0.676152912067344 },
new [] { 0.0769111306000146, 0.44407293722137, 0.281299909284884, 0.649699116036555 },
new [] { 0.0651057401812689, 0.462621685129238, 0.248992842094888, 0.649455533556911 },
new [] { 0.0795094585393292, 0.428683501583824, 0.273512023052464, 0.619375395462352 },
new [] { 0.0666187199715286, 0.444473278367637, 0.236792684423714, 0.620047937153116 },
new [] { 0.0824729343048468, 0.408655122569987, 0.264346522261738, 0.591723893933758 },
new [] { 0.0640741126882233, 0.431182139080443, 0.227023444720074, 0.591613458316352 },
new [] { 0.0842724154826066, 0.391007082089885, 0.256911314605767, 0.565546379173761 },
new [] { 0.0646369530227591, 0.414394019163947, 0.218063811719034, 0.56194637736745 },
new [] { 0.0945461812906187, 0.368473256450873, 0.259927122070532, 0.534321878998355 },
new [] { 0.0745184459680052, 0.387090790371887, 0.219638367820538, 0.534676423507889 },
new [] { 0.0937054526015681, 0.353498455690188, 0.254289366304508, 0.509783662198489 },
new [] { 0.0750450983067291, 0.367282125911441, 0.214813142285254, 0.510606722750282 },
new [] { 0.0986136056643263, 0.33755139604269, 0.247098810390872, 0.484822643563458 },
new [] { 0.0759487958603469, 0.355036928955008, 0.209121332867857, 0.484265087004813 },
new [] { 0.100095123437523, 0.321777321977973, 0.242166998254586, 0.461059218364878 },
new [] { 0.0760172891040159, 0.33622718593188, 0.197281893906182, 0.456376816873702 },
new [] { 0.183662712141234, 0.224465099704524, 0.323253621434305, 0.367207902284369 },
new [] { 0.165386688224198, 0.237057995748376, 0.293491754968964, 0.368182864509001 },
new [] { 0.180110647150158, 0.223645929119496, 0.311789282702578, 0.349038318866385 },
new [] { 0.159248365037179, 0.230134081882522, 0.278020506015348, 0.348661001014605 },
new [] { 0.178434261886473, 0.211518866297754, 0.300904232415592, 0.330705852131637 },
new [] { 0.152393042843677, 0.224108310274577, 0.264953612525238, 0.331874342068813 },
new [] { 0.166639142445352, 0.2042697573865, 0.271484448221136, 0.314325811574399 },
new [] { 0.141775378617484, 0.217551357025041, 0.233115414560599, 0.314091249341552 },
new [] { 0.159132509152013, 0.205065114325152, 0.252306990056503, 0.29682157898132 },
new [] { 0.128407088817494, 0.211198791880728, 0.212480620155039, 0.297973865136821 },
new [] { 0.157730747493977, 0.193506648026414, 0.243775139444168, 0.28162436733059 },
new [] { 0.129631265830241, 0.201434293455852, 0.206230359013427, 0.280238679598186 },
new [] { 0.156374427165502, 0.185057471264368, 0.236770708428715, 0.265839664922234 },
new [] { 0.126608318372232, 0.192728904847397, 0.198362071032878, 0.26676316729728 },
new [] { 0.155519279311961, 0.173347541528741, 0.230644965383214, 0.250291608115431 },
new [] { 0.123637586902893, 0.182297974134709, 0.191825987950093, 0.250149970005999 },
new [] { 0.15194233640209, 0.162734822095049, 0.225534375978986, 0.235807694136668 },
new [] { 0.120848571343621, 0.172241548479172, 0.183956264513344, 0.236570828458513 },
new [] { 0.151489806586513, 0.151758643865283, 0.219218949910918, 0.220612299000832 },
new [] { 0.119783720721529, 0.1607661012893, 0.178051876958198, 0.21800619796824 },
new [] { 0.149626194616608, 0.142543957432262, 0.21482061689275, 0.206725443550584 },
new [] { 0.116448879739849, 0.152532891792715, 0.172804236490959, 0.205860534124629 },
new [] { 0.239477699003859, 0.0998010709264411, 0.304646131787138, 0.161308214154749 },
new [] { 0.210846837032666, 0.102015342018065, 0.270007645448492, 0.162108558529267 },
new [] { 0.230256309683648, 0.091458355333625, 0.291466689876408, 0.150993876762959 },
new [] { 0.200876639947161, 0.0967306133477439, 0.257387237632114, 0.152156235850713 },
new [] { 0.212957241254578, 0.0917798308942109, 0.267503843536558, 0.142465627442866 },
new [] { 0.183695554825805, 0.0942547093769591, 0.228899445764054, 0.141085946036038 },
new [] { 0.198884469579268, 0.0885976632900379, 0.245311053734106, 0.132915193167412 },
new [] { 0.168647845468054, 0.0905176335021379, 0.206308206355601, 0.133143738731203 },
new [] { 0.187010052918076, 0.085252687601401, 0.22628780689588, 0.124808471051768 },
new [] { 0.153140600551375, 0.0886819163996722, 0.18783181393589, 0.123223638428012 },
new [] { 0.18361258258437, 0.0805458008451878, 0.222524554557063, 0.115066687415897 },
new [] { 0.148663739116, 0.0837068224002897, 0.179586623316303, 0.116223852709292 },
new [] { 0.180712763082835, 0.0751343561413517, 0.215693621678205, 0.1080597999945 },
new [] { 0.143669922049996, 0.0781665322701072, 0.173209610797603, 0.109415348245135 },
new [] { 0.176343024647045, 0.0705162718353673, 0.207359716479622, 0.100909223027595 },
new [] { 0.139985130111524, 0.0716951672862454, 0.167028673217261, 0.101824378324254 },
new [] { 0.174031418298996, 0.0648356730467992, 0.203690972880713, 0.0916371316431142 },
new [] { 0.135793758077214, 0.0678040374931297, 0.162261498755646, 0.0923297668491166 },
new [] { 0.17113341478125, 0.0590523173994402, 0.199774121061748, 0.0872607336231221 },
new [] { 0.131721878086004, 0.0626552353592483, 0.157033163206921, 0.0857450227163595 },
new [] { 0.260069639115937, 0.0376965977160731, 0.28982824992597, 0.0657623767813504 },
new [] { 0.230380252909646, 0.0384695116533894, 0.2543961352657, 0.0652882769516216 },
new [] { 0.240455108775178, 0.036395263763009, 0.267128128884448, 0.0591449255566179 },
new [] { 0.203783976366848, 0.0381960423377819, 0.22504804156485, 0.0596121980023986 },
new [] { 0.222793233026869, 0.0355577999910883, 0.239396010166403, 0.0551295994199746 },
new [] { 0.185146318061885, 0.0370367594147278, 0.203317892162626, 0.0546707489915325 },
new [] { 0.205236122864057, 0.0325494737154649, 0.224093003423076, 0.0515551787635144 },
new [] { 0.170237212750185, 0.0356782802075612, 0.1869317395117, 0.0506304754224552 },
new [] { 0.191353628864756, 0.0320604118449944, 0.204966288841118, 0.0448355906490468 },
new [] { 0.155885141942749, 0.0331298524269543, 0.166630495619496, 0.0467214015047504 },
new [] { 0.186784101444508, 0.029996710231181, 0.198047640850444, 0.0435867576110464 },
new [] { 0.150186692054803, 0.0309492286866102, 0.163358672797431, 0.0437974198142924 },
new [] { 0.184893572433614, 0.0267865161811813, 0.194301214821051, 0.0395404192987275 },
new [] { 0.142246642246642, 0.0278343056120834, 0.157240036672741, 0.0386298901618906 },
new [] { 0.179622839182818, 0.0240739354935269, 0.192223185891876, 0.0366790616284792 },
new [] { 0.140826529404804, 0.025137557685481, 0.148200074925926, 0.0352955644830696 },
new [] { 0.174580261170827, 0.0221343756477451, 0.186342898514083, 0.031984464818453 },
new [] { 0.135519745105848, 0.0223415336539934, 0.142943828981796, 0.0329684353818667 },
new [] { 0.25022998397531, 0.013235206837201, 0.258797865435927, 0.0235244458161617 },
new [] { 0.217077517077517, 0.0143822393822394, 0.226238540825536, 0.0225607862479677 },
new [] { 0.230786251659537, 0.0123026995132025, 0.239195775305656, 0.0210123591253542 },
new [] { 0.196288105909941, 0.0133352132743626, 0.204166569107214, 0.021001777494189 },
new [] { 0.215082592311244, 0.0122287263357016, 0.224648003390469, 0.0192390735109012 },
new [] { 0.178194501064456, 0.0123658105720886, 0.185148825429282, 0.0190966594737768 },
new [] { 0.201124782969729, 0.0110062655695629, 0.205598696650058, 0.0169936866130428 },
new [] { 0.162183123222677, 0.0127891239222008, 0.166649231699096, 0.0168967923177203 },
new [] { 0.185160517847789, 0.0100651051410611, 0.190019526510994, 0.0152638644104943 },
new [] { 0.146076871289553, 0.0101979925332028, 0.149961661954282, 0.015447214687121 },
new [] { 0.18214172610879, 0.00892008867307083, 0.185031377962346, 0.0127994190856113 },
new [] { 0.142103298999851, 0.00918047469771608, 0.143616120326562, 0.013971764060858 },
new [] { 0.176867841937998, 0.00772021346765006, 0.178471436570789, 0.0122313713905915 },
new [] { 0.136861773908917, 0.00784482375098246, 0.137801187814737, 0.0115550965076496 },
new [] { 0.17273225906874, 0.00660420712453859, 0.175003139735349, 0.0100089275031522 },
new [] { 0.134425470246366, 0.00717135045493254, 0.134033409657444, 0.00988237870589201 },
new [] { 0.235099186397294, 0.00396897949233629, 0.237051912963767, 0.00705492599018124 },
new [] { 0.198997300424219, 0.00416060992613249, 0.204459158004651, 0.00696859328672353 },
new [] { 0.21970304975923, 0.00347265094456106, 0.219781006226294, 0.00637540023872227 },
new [] { 0.181154159567375, 0.0038743610637825, 0.185503544140661, 0.00594842573797799 },
new [] { 0.204295622343202, 0.00310239391453501, 0.2041371611958, 0.00521981261280872 },
new [] { 0.16430462890365, 0.00405343432400846, 0.165308762453759, 0.00567623174684097 },
new [] { 0.186528000943438, 0.00328729159603166, 0.190441538796512, 0.00463934307625524 },
new [] { 0.149252739463036, 0.00321203886936232, 0.149210895815782, 0.00487477883370118 },
new [] { 0.174233808433646, 0.00306476626541614, 0.176095757191967, 0.00395489503790678 },
new [] { 0.13066540233802, 0.0028151067518113, 0.131805299780554, 0.00386228910168719 },
new [] { 0.168849230214097, 0.00240558094779889, 0.168831612770903, 0.00332763656421525 },
new [] { 0.127624415362144, 0.00259675111025864, 0.128937938312045, 0.00335275116144019 },
new [] { 0.163722374159214, 0.00175179244585705, 0.166452758838022, 0.0027847940251858 },
new [] { 0.123696612665685, 0.00183357879234168, 0.123006208999759, 0.00281834018916626 },
new [] { 0.221094570022027, 0.00116789616072617, 0.223161851068828, 0.00135948639604249 },
new [] { 0.185573274839934, 0.00097818354280294, 0.185008933479934, 0.00182503399125809 },
new [] { 0.205077053143679, 0.000845433915248958, 0.20774720847243, 0.00134205216890042 },
new [] { 0.171269337097818, 0.00094112111052291, 0.168581122972348, 0.00137996969478317 },
new [] { 0.192511897679952, 0.000803093396787626, 0.191652097902098, 0.00130807111561719 },
new [] { 0.152580941650854, 0.000652277039848197, 0.151973387460532, 0.00119855084307156 },
new [] { 0.176237682272815, 0.000598342370026741, 0.176767277410026, 0.000792895654931811 },
new [] { 0.135369431848493, 0.000726119557808008, 0.135252977335958, 0.0010387645881801 },
new [] { 0.159523564140693, 0.000375436168489863, 0.163370829318574, 0.000810798568646102 },
new [] { 0.120792753322874, 0.000436635978804653, 0.120580486376893, 0.000737476555529253 },
new [] { 0.159005725416126, 0.000346771337504427, 0.158797495802244, 0.000601162548929207 },
new [] { 0.115953497315446, 0.000428936975846411, 0.115431479744228, 0.000604439885705912 },
new [] { 0.211483768291066, 0.000213672064956308, 0.21075002057589, 0.000348706615462648 },
new [] { 0.17193475319174, 0.00021327293051715, 0.171888810824287, 0.0003204867787919 },
new [] { 0.196914016852205, 0.000153760993911065, 0.194834317977439, 0.000192003296837097 },
new [] { 0.159491525423729, 0.000117907148120855, 0.156701111371211, 0.000258304255222731 },
new [] { 0.180201457287181, 0.00010430480845167, 0.180024084995273, 0.000154055278658813 },
new [] { 0.141086439451019, 0.000192054839043271, 0.144415854049099, 0.000200023639157355 },
new [] { 0.163998062754997, 8.07185417828525E-05, 0.165737105122256, 0.000104250709358088 },
new [] { 0.127182692878779, 5.93930035041872E-05, 0.121871521616945, 0.000149760383386581 },
new [] { 0.15250811448805, 7.37680731779286E-06, 0.152096584738813, 7.3050021001881E-05 },
new [] { 0.109743464749254, 6.85515812564743E-05, 0.108161720008105, 5.15739429685962E-05 },
new [] { 0.201288827211527, 1.47631982992796E-05, 0.197230034067266, 1.35189941868325E-05 },
new [] { 0.159409495151127, 8.19318029465656E-05, 0.159930007764412, 4.51630385692349E-05 },
new [] { 0.184818433288211, 2.94273438879407E-05, 0.184859874636628, 3.16269823340713E-05 },
new [] { 0.143260921037055, 0, 0.149196308317326, 9.03342366757001E-06 },
new [] { 0.169255276782159, 1.47499151879877E-05, 0.167945763318641, 0 },
new [] { 0.130905322377674, 0, 0.129362114751028, 9.01867768147834E-06 },
new [] { 0.157022245062771, 2.93664194993025E-05, 0.155673520633487, 4.52902653103742E-06 },
new [] { 0.112959950762738, 1.46539470406354E-05, 0.115958890828001, 0 },
new [] { 0.19354719327333, 0, 0.190084562527593, 0 },
new [] { 0.152502096729103, 0, 0.151207431023641, 0 },
new [] { 0.176568483063328, 0, 0.178207659783348, 0 },
new [] { 0.13654129081155, 0, 0.137212754260583, 0 },
new [] { 0.163425749538407, 0, 0.162465714555944, 0 },
new [] { 0.123303651855462, 0, 0.122899756602297, 0 },
new [] { 0.173308858221279, 0, 0.170694144368733, 0 },
new [] { 0.132218260367811, 0, 0.13019661547491, 0 },
new [] { 0.16027559638465, 0, 0.158135751849541, 0 },
new [] { 0.116653689638455, 0, 0.116412866797844, 0 },
new [] { 0.152796615166883, 0, 0.150267063613677, 0 },
new [] { 0.109221101855911, 0, 0.110723525170703, 0 }
    },
#endregion
#region 5 Opponents
    new double[][] {
        new [] { 4.24621237855833E-05, 0.505511583667369, 0.494296426292278, 0.997750382913547 },
new [] { 0.00352965424846705, 0.549440923378163, 0.427436889910096, 0.974273805569544 },
new [] { 0.00778665580945595, 0.578774153052742, 0.380599480410266, 0.950019618863803 },
new [] { 0.0108545389380984, 0.601675968119059, 0.336256141597111, 0.927338244714926 },
new [] { 0.0141112941296337, 0.616965817045628, 0.300297601932256, 0.903679193088479 },
new [] { 0.0170781560970362, 0.630520144155774, 0.266979557780559, 0.881426126640046 },
new [] { 0.0212276652133026, 0.637736171667949, 0.241089644407323, 0.859246360141015 },
new [] { 0.0218266890618037, 0.639898193535118, 0.218448659531631, 0.837198083169845 },
new [] { 0.026176657998519, 0.636747205813973, 0.201779147459471, 0.816246150521368 },
new [] { 0.0297881052308322, 0.640171290135462, 0.18720210027273, 0.795298805636707 },
new [] { 0.0311812809235844, 0.634912726772952, 0.17439377196141, 0.775313749385329 },
new [] { 0.0345087375950398, 0.625989365939833, 0.164028294408196, 0.755769024364066 },
new [] { 0.035298561274594, 0.614977852556399, 0.156570537990359, 0.739261606580861 },
new [] { 0.0761601892647134, 0.485985153691862, 0.313208456717857, 0.725628947650145 },
new [] { 0.0661057896796366, 0.515405685897545, 0.279303806040905, 0.723489840163238 },
new [] { 0.0767373611561364, 0.471881451654183, 0.29327939621755, 0.688996496791404 },
new [] { 0.0681210055389859, 0.492398806987644, 0.260600682778801, 0.687918774045736 },
new [] { 0.0793791731780082, 0.451813112658048, 0.278403379608248, 0.64881096155113 },
new [] { 0.0681709849851895, 0.472881413639304, 0.24583856574131, 0.651861351155762 },
new [] { 0.0823961088830105, 0.429319907841966, 0.267526553336396, 0.615707587382779 },
new [] { 0.0686081115551399, 0.450556925431511, 0.229862135896515, 0.615312935964473 },
new [] { 0.0806814280810161, 0.418880878819087, 0.241323707016682, 0.57952914917362 },
new [] { 0.0655200655200655, 0.441322003822004, 0.20297132476969, 0.582656795075584 },
new [] { 0.084665219397959, 0.398215415121109, 0.231719822259617, 0.547675894259104 },
new [] { 0.0661002476304329, 0.420852190248484, 0.192876091879651, 0.548327316743972 },
new [] { 0.086508490742791, 0.379622217301698, 0.223959421349488, 0.52040432685594 },
new [] { 0.0666472941096031, 0.397584697959044, 0.186247962732441, 0.516268655117368 },
new [] { 0.0895880652488127, 0.360374079427352, 0.217908198645117, 0.486788498347894 },
new [] { 0.067386454590351, 0.378041927078899, 0.175060729774759, 0.485235297288556 },
new [] { 0.0988634995959492, 0.327633637660976, 0.224055494591629, 0.454679565984286 },
new [] { 0.0744807070637595, 0.34823577962721, 0.18133312910061, 0.455235017500161 },
new [] { 0.100182180668225, 0.311494568953664, 0.218938700823422, 0.428876329212415 },
new [] { 0.075920495275334, 0.325135909176656, 0.176821845487513, 0.429502813255461 },
new [] { 0.103970047792405, 0.287996687313446, 0.211927734248527, 0.399411107616558 },
new [] { 0.0756907701352146, 0.303861050593077, 0.168266327865553, 0.398948503639795 },
new [] { 0.105764268758386, 0.271312828981319, 0.206102412731006, 0.374611106895468 },
new [] { 0.0754428126390743, 0.287414330218069, 0.164274352133669, 0.370687632011265 },
new [] { 0.189710473785679, 0.181708835923771, 0.284865583376285, 0.278326308808113 },
new [] { 0.1647408753493, 0.192959146937306, 0.253269299844351, 0.276989465942766 },
new [] { 0.18468289136606, 0.175067506750675, 0.269730438544314, 0.263047532800759 },
new [] { 0.157520763904477, 0.186003142645002, 0.236521164021164, 0.262207771268877 },
new [] { 0.180306751627364, 0.167708637480034, 0.260687583444593, 0.245482525281772 },
new [] { 0.149995729050995, 0.173588451353891, 0.221739471311529, 0.246905200555933 },
new [] { 0.166012042772606, 0.160509741495657, 0.23225331949935, 0.228668628172122 },
new [] { 0.135785970478687, 0.172325493171472, 0.194569847221159, 0.230342170858826 },
new [] { 0.153458287520616, 0.153904961517317, 0.214898565706164, 0.215840061928159 },
new [] { 0.1244277699377, 0.160146972773896, 0.177220126590371, 0.213043803898911 },
new [] { 0.153879881015836, 0.145403119035747, 0.209489154882242, 0.198287910552061 },
new [] { 0.119993186789544, 0.153754437551547, 0.165877761691949, 0.198526869849342 },
new [] { 0.152045080844067, 0.132801795012332, 0.20341829931275, 0.184709592414195 },
new [] { 0.116996447632613, 0.143072883595614, 0.159657398212512, 0.184461959144282 },
new [] { 0.151223912704058, 0.125305761324012, 0.197562914246888, 0.170900755124056 },
new [] { 0.113939892677742, 0.132918445370228, 0.154000445732115, 0.170299947302195 },
new [] { 0.147280680604829, 0.11418902628044, 0.191524098891817, 0.159876503227757 },
new [] { 0.113334824731978, 0.120364474884273, 0.148429827145944, 0.158334859963145 },
new [] { 0.147633454694631, 0.108541442319197, 0.187682014460735, 0.145293248440861 },
new [] { 0.11055406690293, 0.112360618895324, 0.14236763163671, 0.145728076021757 },
new [] { 0.145879866671248, 0.0975825573004364, 0.182815128668453, 0.134495688038995 },
new [] { 0.108037319181717, 0.101258238466147, 0.139167949940114, 0.136373455456666 },
new [] { 0.229584502703813, 0.0620165651310836, 0.265149207274269, 0.0976785944690551 },
new [] { 0.198830149035808, 0.0652142946172611, 0.228700105280944, 0.0978055515186746 },
new [] { 0.22052021641549, 0.0578245805669813, 0.251989843575272, 0.0894055017119061 },
new [] { 0.187076234727241, 0.062966787127861, 0.217958804638025, 0.0892599055745287 },
new [] { 0.199807464200375, 0.0557924051503326, 0.228078566000157, 0.0834136830151486 },
new [] { 0.165843740923613, 0.0613691889767815, 0.19266361983638, 0.0825418289921332 },
new [] { 0.189167853649328, 0.0539633780096446, 0.208300704776821, 0.0781214501314466 },
new [] { 0.149694257506918, 0.0559218392375226, 0.169536166964878, 0.0768245563053609 },
new [] { 0.17139912393713, 0.0517478313149532, 0.191088516746411, 0.0693727295266636 },
new [] { 0.135217814844564, 0.0534838411976826, 0.151173824634913, 0.0712260693342532 },
new [] { 0.167147835593973, 0.0462263828624142, 0.187366394559524, 0.0643281769024865 },
new [] { 0.131960804397555, 0.0500273140086722, 0.14521633150518, 0.0632553175433112 },
new [] { 0.164366459733552, 0.0426692661570297, 0.180864907205033, 0.0570022930100871 },
new [] { 0.126127279711709, 0.0437009530429692, 0.14061740693945, 0.0583736135565848 },
new [] { 0.162877543610933, 0.0373696858629692, 0.178273015084735, 0.0529710011182646 },
new [] { 0.124595995006584, 0.040537305265318, 0.134937651977049, 0.053951400578359 },
new [] { 0.160288436775689, 0.0350244656193665, 0.172753498649644, 0.0475506477140447 },
new [] { 0.116983998204761, 0.0366643075382783, 0.129801920768307, 0.048922268840278 },
new [] { 0.155081495685523, 0.0316138200246542, 0.170642111965519, 0.0440922097890151 },
new [] { 0.113421874463777, 0.032670990768386, 0.12511214467844, 0.0424982314733435 },
new [] { 0.240084782296307, 0.0192915648800115, 0.251590030937554, 0.0291940285195054 },
new [] { 0.20525293500868, 0.0202485518331987, 0.211845508875212, 0.030284162274157 },
new [] { 0.218421189032292, 0.0175279462326578, 0.226848379839148, 0.0269636451250201 },
new [] { 0.180666563536671, 0.0186407466611664, 0.189382511188213, 0.02744680164035 },
new [] { 0.198292122500772, 0.0179018484858877, 0.206046699788878, 0.0247167757165494 },
new [] { 0.159960241294214, 0.0166146833013436, 0.169522425198014, 0.0238752326551569 },
new [] { 0.182191123824532, 0.0158784536064816, 0.190373491572754, 0.0217611608827366 },
new [] { 0.14581974248927, 0.0165064377682403, 0.149901416902409, 0.0217318323224637 },
new [] { 0.167561316602517, 0.0136355006299946, 0.17311835946207, 0.0187745550569999 },
new [] { 0.130351923701437, 0.0157075221768335, 0.134328717350241, 0.0200299834020453 },
new [] { 0.165845466634842, 0.0130937361475773, 0.172830433103723, 0.0176268086708622 },
new [] { 0.126835469309397, 0.013083934346841, 0.130030996661898, 0.0175002981773233 },
new [] { 0.162245555038815, 0.0121722176510571, 0.167493705541568, 0.0157259274906334 },
new [] { 0.120340778972194, 0.0115102626766701, 0.124187849758133, 0.0161439262973661 },
new [] { 0.157296207591709, 0.00991608801281941, 0.163295267204967, 0.0146165180636071 },
new [] { 0.116532278773561, 0.00960402650095013, 0.121624470103286, 0.0137770195858256 },
new [] { 0.154959069366652, 0.00847910383455407, 0.158387524084778, 0.0119218415417559 },
new [] { 0.113454725925419, 0.008916194616056, 0.116293554262883, 0.0115721749643767 },
new [] { 0.222907842214436, 0.00476738308145514, 0.225366578386409, 0.0078245489155639 },
new [] { 0.188786052892045, 0.00511382382052128, 0.188899997468931, 0.00822054975935915 },
new [] { 0.201324174318213, 0.00432540770179526, 0.206322774350629, 0.00708376200274348 },
new [] { 0.165500404036931, 0.00498598765538229, 0.1708705165081, 0.00668595306975249 },
new [] { 0.187289425527184, 0.00384806143216295, 0.191035535043252, 0.00596321873754296 },
new [] { 0.151799472656919, 0.00472554189638051, 0.153086389306765, 0.00550928335936144 },
new [] { 0.175246582453712, 0.00356463055892023, 0.175836572535652, 0.00522901703043213 },
new [] { 0.1324378330373, 0.00385981691487908, 0.13581130230506, 0.00556525999616556 },
new [] { 0.158209820743293, 0.00344527638962324, 0.163561201369137, 0.00468767274737424 },
new [] { 0.119923988221598, 0.00364651099089228, 0.119229286190014, 0.00463397967028274 },
new [] { 0.152991277226535, 0.00256199338508731, 0.159106802671875, 0.00400059902017414 },
new [] { 0.117004076437503, 0.00294122705928895, 0.116746383229997, 0.00400509739668669 },
new [] { 0.154676382340334, 0.00245177882554651, 0.154362858478243, 0.00358157012618229 },
new [] { 0.109954970309108, 0.00258337668861341, 0.110924594277483, 0.00305478010159718 },
new [] { 0.150300583991755, 0.00190656131913432, 0.149432012955542, 0.00281376713134836 },
new [] { 0.108468450036656, 0.00209707943327707, 0.108060720975922, 0.00306962841906073 },
new [] { 0.203136790834877, 0.0010633189270769, 0.205304815799755, 0.00153706759861502 },
new [] { 0.167676112807739, 0.000861583607732073, 0.167356785147185, 0.00152411404209965 },
new [] { 0.188466648497649, 0.000877222865708251, 0.188160081990776, 0.00139178470863874 },
new [] { 0.151227284471622, 0.00107656532598398, 0.149021491175411, 0.00138794745166392 },
new [] { 0.173490230905861, 0.000719360568383659, 0.173991827601304, 0.00122818858780442 },
new [] { 0.137099004424779, 0.000803719579646018, 0.136452425219458, 0.00131180625630676 },
new [] { 0.160819711292359, 0.000511892980240931, 0.160525589819246, 0.00112055931458228 },
new [] { 0.120940526056947, 0.000678345515288212, 0.122754247622145, 0.000905719430629395 },
new [] { 0.147104247104247, 0.000514800514800515, 0.147044959047787, 0.000836470096194061 },
new [] { 0.104362057561522, 0.000559673015280769, 0.105964438186938, 0.000781657113079729 },
new [] { 0.144337788787394, 0.000438664395933323, 0.1458976306918, 0.00066705800736432 },
new [] { 0.103415693823846, 0.000476854877165716, 0.10241729557964, 0.000679926147859062 },
new [] { 0.141209540034072, 0.000238500851788756, 0.142010795115269, 0.000430585383486785 },
new [] { 0.0993956195330257, 0.000387423376265583, 0.0965569368922181, 0.000516952856031294 },
new [] { 0.187790328939474, 9.42765559917037E-05, 0.190428146898045, 0.000308326954154971 },
new [] { 0.155401704720501, 0.000188272344504159, 0.153157743304481, 0.000284045232863498 },
new [] { 0.176518996022493, 0.000214305307913866, 0.177315923193433, 0.000176858352537649 },
new [] { 0.138965528986988, 0.000204373594931535, 0.13916158446326, 0.000203452263674134 },
new [] { 0.163842762151975, 8.88952103260676E-05, 0.163976474237381, 0.000161630123396239 },
new [] { 0.126419534154077, 5.968011458582E-05, 0.125013107763291, 0.000145369183886634 },
new [] { 0.150976515900337, 9.37318927025461E-05, 0.150040820190021, 0.000144722454492828 },
new [] { 0.112145879713815, 8.49718743096035E-05, 0.109270349966855, 5.31807400631787E-05 },
new [] { 0.136901909781641, 8.55636936135259E-05, 0.137838590627394, 9.04322662325918E-05 },
new [] { 0.0973895752961371, 5.09848574973233E-05, 0.0945002573109883, 7.46372098478467E-05 },
new [] { 0.135066067246714, 6.83748995743662E-05, 0.133844999711466, 0.000127565935642985 },
new [] { 0.0919427865816012, 2.57254579131509E-05, 0.0930130808409433, 4.95016830572239E-05 },
new [] { 0.181395626513833, 2.55859175110019E-05, 0.181756822493555, 2.15784646922371E-05 },
new [] { 0.141649827037024, 0, 0.142847841270969, 2.67339649678123E-05 },
new [] { 0.166149544819121, 1.70479729960108E-05, 0.167292847046885, 1.06476995645091E-05 },
new [] { 0.127882366997218, 1.70680503166123E-05, 0.129386066830878, 4.24673532222104E-05 },
new [] { 0.154066437571592, 0, 0.155379713097762, 0 },
new [] { 0.115632602371515, 0, 0.117262818017358, 1.06910708176531E-05 },
new [] { 0.139579708661686, 1.7057278340668E-05, 0.142152633665213, 0 },
new [] { 0.099489752529977, 0, 0.102066529423014, 2.12071086228104E-05 },
new [] { 0.129425568819508, 2.5507601265177E-05, 0.129260744985673, 0 },
new [] { 0.0865994901886986, 0, 0.0883184458329666, 0 },
new [] { 0.173444812625969, 0, 0.171976608754732, 0 },
new [] { 0.130585646580907, 0, 0.136275878111189, 1.06699672432006E-05 },
new [] { 0.161985162445638, 0, 0.159774978363304, 0 },
new [] { 0.122656782549421, 0, 0.120450109082558, 0 },
new [] { 0.148412482114874, 0, 0.146248763168616, 0 },
new [] { 0.108563263362025, 0, 0.105962538553322, 0 },
new [] { 0.132995545278335, 0, 0.133427239724694, 0 },
new [] { 0.0918000376576916, 0, 0.0936545240893067, 0 },
new [] { 0.16780581393357, 0, 0.166844856260394, 0 },
new [] { 0.130212491294821, 0, 0.128477403384576, 0 },
new [] { 0.155284343953409, 0, 0.154567924373587, 0 },
new [] { 0.114349583747302, 0, 0.115223129963817, 0 },
new [] { 0.141724037289474, 0, 0.14074987147731, 0 },
new [] { 0.0995834418120281, 0, 0.102736486486486, 0 },
new [] { 0.150092128160508, 0, 0.148858230807514, 0 },
new [] { 0.107293497363796, 0, 0.109609761016436, 0 },
new [] { 0.136159417814069, 0, 0.1370949364204, 0 },
new [] { 0.094056720516689, 0, 0.0952591202605, 0 },
new [] { 0.132107306981398, 0, 0.131180797171255, 0 },
new [] { 0.0918400631781349, 0, 0.0914567420884502, 0 },

    },
#endregion
        };
        #endregion
    }
}
