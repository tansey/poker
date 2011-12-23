using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using FastPokerEngine;

namespace PokerEngineTest
{
    [TestFixture]
    public class PotManagerTest
    {
        private PotManager potMan;
        private List<string> players;

        [SetUp]
        public void setup()
        {
            players = new List<string>();
            for (int i = 0; i < 5; i++)
                players.Add("Player" + i);

            potMan = new PotManager(players);
        }

        /// <summary>
        /// Preflop:
        /// Seq0 UTG, raises to $4
        /// Seq4 is on BB and will reraise to $6
        /// Seq0 flat calls
        /// Flop:
        /// Seq4 goes all-in for $194
        /// Seq0 folds.
        /// </summary>
        [Test]
        public void TestTotal()
        {
            
            Action[] actions = new Action[] {
                new Action("Player3", Action.ActionTypes.PostSmallBlind, 1),
                new Action("Player4", Action.ActionTypes.PostBigBlind, 2),
                new Action("Player0", Action.ActionTypes.Raise, 4),
                new Action("Player1", Action.ActionTypes.Fold),
                new Action("Player2", Action.ActionTypes.Fold),
                new Action("Player3", Action.ActionTypes.Fold),
                new Action("Player4", Action.ActionTypes.Raise, 4),
                new Action("Player0", Action.ActionTypes.Call, 2),
                new Action("Player4", Action.ActionTypes.AllIn, 194),
                new Action("Player0", Action.ActionTypes.Fold)
            };

            potMan.AddAction(actions[0]);
            Assert.AreEqual(1, potMan.Total);
            potMan.AddAction(actions[1]);
            Assert.AreEqual(3, potMan.Total);
            potMan.AddAction(actions[2]);
            Assert.AreEqual(7, potMan.Total);
            potMan.AddAction(actions[3]);
            Assert.AreEqual(7, potMan.Total);
            potMan.AddAction(actions[4]);
            Assert.AreEqual(7, potMan.Total);
            potMan.AddAction(actions[5]);
            Assert.AreEqual(7, potMan.Total);
            potMan.AddAction(actions[6]);
            Assert.AreEqual(11, potMan.Total);
            potMan.AddAction(actions[7]);
            Assert.AreEqual(13, potMan.Total);
            potMan.AddAction(actions[8]);
            Assert.AreEqual(207, potMan.Total);
            potMan.AddAction(actions[9]);
            Assert.AreEqual(207, potMan.Total);
        }

        /// <summary>
        /// Preflop:
        /// Player0: All-in for 250 strength=1
        /// Player1: All-in for 275 strength=2
        /// Player2: All-in for 125 strength=5
        /// Player3: All-in for 500 strength=4
        /// Player4: All-in for 500 strength=3
        /// 
        /// </summary>
        [Test]
        public void TestAllInSidePots()
        {
            Action[] actions = new Action[] {
                new Action("Player3", Action.ActionTypes.PostSmallBlind, 1),
                new Action("Player4", Action.ActionTypes.PostBigBlind, 2),
                new Action("Player0", Action.ActionTypes.AllIn, 250),
                new Action("Player1", Action.ActionTypes.AllIn, 275),
                new Action("Player2", Action.ActionTypes.AllIn, 125),
                new Action("Player3", Action.ActionTypes.AllIn, 499),
                new Action("Player4", Action.ActionTypes.AllIn, 498)
            };

            Assert.AreEqual(0, potMan.Total);
            Assert.AreEqual(1, potMan.PotCount, "Started with something besides 1 pot");
            
            potMan.AddAction(actions[0]);
            Assert.AreEqual(1, potMan.Total, "small blind not added correctly"); 
            Assert.AreEqual(1, potMan.PotCount, "pots wrong after small blind");
            
            potMan.AddAction(actions[1]);
            Assert.AreEqual(3, potMan.Total);
            Assert.AreEqual(1, potMan.PotCount);
            
            potMan.AddAction(actions[2]);
            Assert.AreEqual(253, potMan.Total); 
            Assert.AreEqual(1, potMan.PotCount);
            
            potMan.AddAction(actions[3]);
            Assert.AreEqual(528, potMan.Total); 
            Assert.AreEqual(2, potMan.PotCount);
            Assert.AreEqual(503, potMan.Pots[0].Size);
            Assert.AreEqual(25, potMan.Pots[1].Size);

            potMan.AddAction(actions[4]);
            Assert.AreEqual(653, potMan.Total);
            Assert.AreEqual(3, potMan.PotCount);
            Assert.AreEqual(378, potMan.Pots[0].Size);
            Assert.AreEqual(250, potMan.Pots[1].Size);
            Assert.AreEqual(25, potMan.Pots[2].Size);

            potMan.AddAction(actions[5]);
            Assert.AreEqual(1152, potMan.Total);
            Assert.AreEqual(4, potMan.PotCount);
            Assert.AreEqual(502, potMan.Pots[0].Size);
            Assert.AreEqual(375, potMan.Pots[1].Size);
            Assert.AreEqual(50, potMan.Pots[2].Size);
            Assert.AreEqual(225, potMan.Pots[3].Size);

            potMan.AddAction(actions[6]);
            Assert.AreEqual(1650, potMan.Total);
            Assert.AreEqual(4, potMan.PotCount);
            Assert.AreEqual(625, potMan.Pots[0].Size);
            Assert.AreEqual(500, potMan.Pots[1].Size);
            Assert.AreEqual(75, potMan.Pots[2].Size);
            Assert.AreEqual(450, potMan.Pots[3].Size);

            Dictionary<string,ulong> strengths = new Dictionary<string,ulong>();
            strengths["Player0"] = 1;
            strengths["Player1"] = 2;
            strengths["Player2"] = 5;
            strengths["Player3"] = 4;
            strengths["Player4"] = 3;

            
            List<Winner> winners = potMan.GetWinners(strengths);
            Assert.AreEqual(4, winners.Count);
            
            Assert.AreEqual(625, winners[0].Amount);
            Assert.AreEqual("Player2", winners[0].Player);

            Assert.AreEqual(500, winners[1].Amount);
            Assert.AreEqual("Player3", winners[1].Player);

            Assert.AreEqual(75, winners[2].Amount);
            Assert.AreEqual("Player3", winners[2].Player);

            Assert.AreEqual(450, winners[3].Amount);
            Assert.AreEqual("Player3", winners[3].Player);
        }
    }
}
