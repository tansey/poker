using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastPokerEngine;

namespace AlwaysCallVsAlwaysRaise
{
    public class AlwaysRaisePlayer : IPlayer
    {

        #region IPlayer Members

        public void GetAction(HandHistory history, out FastPokerEngine.Action.ActionTypes action, out double amount)
        {
            action = FastPokerEngine.Action.ActionTypes.Raise;
            amount = 0;
        }

        public void NewHand(HandHistory history)
        {
        }
        #endregion
    }
}
