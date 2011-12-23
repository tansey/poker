using System;
using System.Collections.Generic;
using System.Text;
using FastPokerEngine;

namespace PokerEngineTest
{
    /// <summary>
    /// Makes a set of pre-defined actions.
    /// </summary>
    public class SequencePlayer : IPlayer
    {
        private FastPokerEngine.Action[] actions;
        private int curAction;

        public SequencePlayer(params FastPokerEngine.Action[] actions)
        {
            this.actions = actions;
            this.curAction = 0;
        }



        public void NewHand(HandHistory history)
        {
        }

        public void GetAction(HandHistory history,
                                out FastPokerEngine.Action.ActionTypes type, out double amount)
        {
            if (actions != null && curAction < actions.Length)
            {
                FastPokerEngine.Action action = actions[curAction++];
                type = action.ActionType;
                amount = action.Amount;
            }
            else
            {
                type = FastPokerEngine.Action.ActionTypes.Fold;
                amount = 0;
            }
        }
    }
}
