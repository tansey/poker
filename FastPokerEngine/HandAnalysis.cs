using System;
using System.Collections.Generic;
using System.Text;
using KeithRuleHand = HoldemHand.Hand;

namespace FastPokerEngine
{
    [Serializable]
    public class HandAnalysis
    {
        public double PositiveHandPotential { get; set; }
        public double NegativeHandPotential { get; set; }

        public int StraightDrawOuts { get; set; }
        public int Outs { get; set; }
        public int DiscountedOuts { get; set; }

        public bool IsSuited { get; set; }
        public bool IsConnected { get; set; }
        public bool IsBackdoorFlushDraw { get; set; }
        public bool IsFlushDraw { get; set; }
        public bool IsGutShotStraightDraw { get; set; }
        public bool IsOpenEndedStraightDraw { get; set; }
        
        public double HandStrength { get; set; }
        public double WinProbability { get; set; }

        public HandAnalysis()
        {
        }

        public HandAnalysis(ulong hand, ulong board, int numOpponents)
        {
            

            //StraightDrawOuts = KeithRuleHand.StraightDrawCount(hand, board, 0UL);
            //Outs = KeithRuleHand.Outs(hand, board);
            //DiscountedOuts = KeithRuleHand.OutsDiscounted(hand, board);

            //IsConnected = KeithRuleHand.IsConnected(hand);
            //IsSuited = KeithRuleHand.IsSuited(hand);
            //IsBackdoorFlushDraw = KeithRuleHand.IsBackdoorFlushDraw(hand, board, 0UL);
            //IsFlushDraw = KeithRuleHand.IsFlushDraw(hand, board, 0UL);
            //IsGutShotStraightDraw = KeithRuleHand.IsGutShotStraightDraw(hand, board, 0UL);
            //IsOpenEndedStraightDraw = KeithRuleHand.IsOpenEndedStraightDraw(hand, board, 0UL);

            if (KeithRuleHand.BitCount(board) == 4)
            {
                double ppot, npot;
                KeithRuleHand.HandPotential(hand, board, out ppot, out npot, numOpponents, 0, 100);
                PositiveHandPotential = ppot;
                NegativeHandPotential = npot;

                HandStrength = KeithRuleHand.HandStrength(hand, board, numOpponents, 0, 100);
                WinProbability = KeithRuleHand.WinOdds(hand, board, 0UL, numOpponents, 0, 100);
            }
        }
    }
}
