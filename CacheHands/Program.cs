using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FastPokerEngine;
using System.Xml.Serialization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using HoldemHand;
using System.Runtime.Serialization;
using System.Threading;
using System.Diagnostics;
namespace FastPokerEngine
{
    class Program
    {
        public static ulong[] HoleCards { get; set; }
        public static ulong Flop { get; set; }
        public static ulong Turn { get; set; }
        public static ulong River { get; set; }
        static void Main(string[] args)
        {
            //cache();
            //LookUpTableCreator.CreatePocketHandsLookUp(1, 5);
            //ulong[] flops = LookUpTableCreator.EnumerateFlop(KeithRuleHand.ParseHand("AsKs"));
            //while (DateTime.Now.ToShortDateString() != "7/14/2009")
            //    Thread.Sleep(60000);
            //Console.WriteLine("Generating flop");
            //LookUpTableCreator.CreateFlopLookUpTable();
            //for (var i = int.Parse(args[0]); i < 6; i++)
            //{
            //    Console.WriteLine("Generating turn for {0} opponent{1}", i, i > 1 ? "s" : "");
            //    LookUpTableCreator.CreateTurnLookUpTable(i);
            //}
            //int opponents = int.Parse(args[0]);
            //Console.WriteLine("Generating Turn for {0} Opponent{1}", opponents, opponents > 1 ? "s" : "");
            //LookUpTableCreator.CreateTurnLookUpTable(opponents);

            
            
            for (int opponents = 4; opponents <= 4; opponents++)
            {
                //HandProbabilitiesLookup lookup = new HandProbabilitiesLookup(args[0] + @"Turn\" + opponents);
                List<long> lookupTimes = new List<long>();
                List<long> mcTimes = new List<long>();
                for (var i = 0; i < 10; i++)
                {
                    ulong pockets = Hand.RandomHand(0UL, 2);
                    ulong board = Hand.RandomHand(pockets, 4);
                    
                    //DateTime start = DateTime.Now;
                    //float ppot, npot, hs, wp;
                    //lookup.GetProbabilities(pockets, board, out ppot, out npot, out hs, out wp);
                    //DateTime end = DateTime.Now;
                    //TimeSpan lookupTime = end - start;

                    
                    double ppotMC, npotMC, hsMC, wpMC;
                    Hand.HandPotential(pockets, board, out ppotMC, out npotMC, opponents, 0, 100);
                    hsMC = Hand.HandStrength(pockets, board, opponents, 0.1, 10000);
                    wpMC = Hand.WinOdds(pockets, board, 0UL, opponents, 0.1, 10000);
                    double wpOne = Hand.WinOdds(pockets, board, 0UL, 1, 0.1, 10000);

                    DateTime startMC = DateTime.Now;
                    double hsComplete = Hand.HandStrength(pockets, board);
                    DateTime endMC = DateTime.Now;
                    TimeSpan mcTime = endMC - startMC;


                    //Console.WriteLine("Pockets: {0} Board: {1}", Hand.MaskToString(pockets), Hand.MaskToString(board));
                    //Console.WriteLine("PPOT\tLookup: {0} MonteCarlo: {1} Difference: {2}", ppot, ppotMC, Math.Abs(ppotMC - ppot));
                    //Console.WriteLine("NPOT\tLookup: {0} MonteCarlo: {1} Difference: {2}", npot, npotMC, Math.Abs(npotMC - npot));
                    //Console.WriteLine("HS\tLookup: {0} MonteCarlo: {1} Difference: {2}", hs, hsMC, Math.Abs(hsMC - hs));
                    //Console.WriteLine("WP\tLookup: {0} MonteCarlo: {1} Difference: {2}", wp, wpMC, Math.Abs(wpMC - wp));
                    //Console.WriteLine("Lookup Time: {0} MonteCarlo Time: {1}", lookupTime.Ticks, mcTime.Ticks);

                    Console.WriteLine("HS: {0} WP: {1} HS-Complete: {2} WP-One: {3} Time: {4}", hsMC, wpMC, hsComplete, wpOne, mcTime.Ticks);
                    if (i == 0)
                        continue;
                    //lookupTimes.Add(lookupTime.Ticks);
                    mcTimes.Add(mcTime.Ticks);
                }
                //Console.WriteLine("Lookup: {0} MC: {1}", lookupTimes.Average(), mcTimes.Average());
            }
            
            
            //LookUpTableCreator.TestDifferentEnum();

            //HandProbabilitiesLookup lookup = new HandProbabilitiesLookup(@"Flop\5");
            
            //Console.WriteLine("Expected: {0}", sizeof(int) * 1000);
            //using (BinaryWriter stream = new BinaryWriter(new FileStream("test.dat", FileMode.Create)))
            //{
            //    for(int i = 0; i < 1000; i++)
            //        stream.Write(i);
            //}
            /*
            List<double> stdevs = new List<double>();
            for (int i = 0; i < 300; i++)
            {
                ulong pockets = Hand.RandomHand(0UL, 2);
                ulong board = Hand.RandomHand(pockets, 4);
                List<double> results = new List<double>();
                for (int j = 0; j < 30; j++)
                {
                    double start = Hand.CurrentTime;
                    //double ppot, npot;
                    //Hand.HandPotential(pockets, board, out ppot, out npot, 5, 0.001, 1000);
                    //double hs = Hand.HandStrength(pockets, board, 5, 0.001, 1000);
                    
                    //double wp = Hand.WinOdds(pockets, board, 0UL, 5, 0.001, 1000);
                    HandAnalysis pockets = new HandAnalysis(pockets, board, 5);
                    double end = Hand.CurrentTime;

                    results.Add(end - start);
                }
                Console.WriteLine("Pockets: {0} Board: {1} Avg: {2} Stdev: {3}",
                    Hand.MaskToString(pockets), Hand.MaskToString(board), 
                    Math.Round(results.Average(), 4), Math.Round(Stdev(results),4));

                stdevs.Add(results.Average());
            }
            Console.WriteLine("Avg Stdev: {0} (Stdev: {1}) Worst: {2}", 
                Math.Round(stdevs.Average(), 4), 
                Math.Round(Stdev(stdevs),4),
                Math.Round(stdevs.Max(), 4));
             */
        }
        public static double Stdev(IEnumerable<double> data)
        {
            return Math.Sqrt((double)Variance(data));
        }
        /// <summary>
        /// Get variance
        /// </summary>
        public static double Variance(IEnumerable<double> data)
        {
            int len = data.Count();

            // Get average
            double avg = data.Average();

            double sum = 0;

            for (int i = 0; i < len; i++)
                sum += (data.ElementAt(i) - avg) * (data.ElementAt(i) - avg);

            return sum / (len - 1);
        }
        private static void cache()
        {
            const int HANDS_TO_CACHE = 100;
            Random random = new MersenneTwister();
            DateTime start = DateTime.Now;
            List<CachedHand> cached = new List<CachedHand>();
            for (int i = 0; i < HANDS_TO_CACHE; i++)
            {
                if (i % 100 == 0)
                    Console.WriteLine("Hand #{0}", i + 1);
                cached.Add(new CachedHand(6, random));
            }
            CachedHands hands = new CachedHands();
            hands.Hands = cached;
            XmlSerializer ser = new XmlSerializer(typeof(CachedHands));
            using (TextWriter txt = new StreamWriter("test.xml"))
                ser.Serialize(txt, hands);
            double time = DateTime.Now.Subtract(start).TotalMilliseconds;
            Console.WriteLine("Time: {0}", time);
        }

        

        private static void cacheCards(int numPlayers, Random random)
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
}
