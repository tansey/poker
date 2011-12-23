using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastPokerEngine;
using System.Xml.Serialization;
using System.IO;

namespace AlwaysCallVsAlwaysRaise
{
    class Program
    {
        static void Main(string[] args)
        {
            HandEngine engine = new HandEngine();

            Console.WriteLine("Loading cached hands");
            List<CachedHand> cachedHands;
            XmlSerializer ser = new XmlSerializer(typeof(CachedHands));
            using (TextReader txt = new StreamReader("test.xml"))
                cachedHands = ((CachedHands)ser.Deserialize(txt)).Hands;

            var seats = new Seat[6];
            seats[0] = new Seat(1, "AlwaysRaise", 100000, new AlwaysRaisePlayer());
            seats[1] = new Seat(2, "AwaysCall", 100000, new AlwaysCallPlayer());
            seats[2] = new Seat(3, "AwaysCall2", 100000, new AlwaysCallPlayer());
            seats[3] = new Seat(4, "AwaysCall3", 100000, new AlwaysCallPlayer());
            seats[4] = new Seat(5, "AwaysCall4", 100000, new AlwaysCallPlayer());
            seats[5] = new Seat(6, "AwaysCall5", 100000, new AlwaysCallPlayer());
            var blinds = new double[] { 1, 2 };
            uint handNumber = 0;
            double maxDifference = 0;
            Console.WriteLine("Starting simulation");
            DateTime start = DateTime.Now;
            for (; handNumber < 100; handNumber++)
            {
                HandHistory results = new HandHistory(seats, handNumber, handNumber % (uint)seats.Length + 1, blinds, 0, BettingStructure.NoLimit);
                engine.PlayHand(results, cachedHands[(int)handNumber]);
                double difference = Math.Abs(seats[0].Chips - seats[1].Chips);
                if (difference > maxDifference)
                    maxDifference = difference;
                if (seats[0].Chips == 0 || seats[1].Chips == 0)
                    break;
            }
            int time = DateTime.Now.Subtract(start).Milliseconds;
            Console.WriteLine("Time: {0}", time);
            Console.WriteLine("Hands: {0}", handNumber);
            Console.WriteLine("AlwaysRaise Bankroll: {0}", seats[0].Chips);
            Console.WriteLine("AlwaysCall Bankroll: {0}", seats[1].Chips);
            Console.WriteLine("Max Difference: {0}", maxDifference);
        }        
    }
}
