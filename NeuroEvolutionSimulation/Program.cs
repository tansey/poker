using System;
using NeuroEvolutionSimulation.Evaluators;
using SharpNeatLib.Evolution;
using SharpNeatLib.Experiments;
using SharpNeatLib.NeatGenome;
using FastPokerEngine;
using Action = FastPokerEngine.Action;
using NeuroEvolutionSimulation.Experiments;
using System.Xml.Serialization;
using System.IO;
using System.Collections.Generic;
using SharpNeatLib.NeuralNetwork;
using System.Xml;
using SharpNeatLib.NeatGenome.Xml;
using NeuroEvolutionSimulation.Players;

namespace NeuroEvolutionSimulation
{
    public class Program
    {
        //private static Random random;

        public static void Main(string[] args)
        {
            Util.Initialize(args[0]);
            var idgen = new IdGenerator();
            IExperiment experiment = new LimitExperiment();

            XmlSerializer ser = new XmlSerializer(typeof(Settings));
            //Settings settings = new Settings()
            //{
            //    SmallBlind = 1,
            //    BigBlind = 2,
            //    GamesPerIndividual = 100,
            //    LogFile = "mutlithreaded_log.txt",
            //    MaxHandsPerTourney = 200,
            //    PlayersPerGame = 6,
            //    StackSize = 124,
            //    Threads = 4
            //};            
            //ser.Serialize(new StreamWriter("settings.xml"), settings);
            Settings settings = (Settings)ser.Deserialize(new StreamReader("settings.xml"));
            var eval = new PokerPopulationEvaluator<SimpleLimitNeuralNetPlayer2, RingGamePlayerEvaluator>(settings);
            
            var ea = new EvolutionAlgorithm(
                new Population(idgen,
                               GenomeFactory.CreateGenomeList(experiment.DefaultNeatParameters,
                                              idgen, experiment.InputNeuronCount,
                                              experiment.OutputNeuronCount,
                                              experiment.DefaultNeatParameters.pInitialPopulationInterconnections,
                                              experiment.DefaultNeatParameters.populationSize)),
                                              eval,
                experiment.DefaultNeatParameters);

            Console.WriteLine("Starting real evolution");
            for (int i = 0; true; i++)
            {
                Console.WriteLine("Generation {0}", i + 1);
                ea.PerformOneGeneration();
                Console.WriteLine("Champion Fitness={0}", ea.BestGenome.Fitness);
                var doc = new XmlDocument();
                XmlGenomeWriterStatic.Write(doc, (NeatGenome)ea.BestGenome);
                FileInfo oFileInfo = new FileInfo("genomes_simple\\" + "bestGenome" + i.ToString() + ".xml");
                doc.Save(oFileInfo.FullName);
            }
        }
        /*
        public static void OldMain(string[] args)
        {
            random = new MersenneTwister();
            //INetworkInputGenerator inputGen = new LimitNetworkInputGenerator();
            //INetworkOutputInterpreter outputInterp = new LimitOutputInterpreter(random);
            IExperiment experiment = new LimitExperiment();

            var idgen = new IdGenerator();
            //var popEval = new PokerPopulationEvaluator
            //              {
            //                  InputGenerator = inputGen,
            //                  OutputInterpreter = outputInterp,
            //                  Rand = random,
            //                  CachedHands = LoadCachedHands(),
            //                  GamesPerEvaluation = 100,
            //                  PlayersPerGame = 6,
            //                  BettingType = BettingStructure.Limit, 
            //                  ActivationFunction = experiment.SuggestedActivationFunction
            //              };

            var popEval = new SimpleLimitPokerPopulationEvaluator<SimpleLimitNeuralNetPlayer>(random)
            {
                LogFilename = "handlog_simple.txt",
                AnalyzeHands = false
            };

            var ea = new EvolutionAlgorithm(
                new Population(idgen,
                               GenomeFactory.CreateGenomeList(experiment.DefaultNeatParameters,
                                              idgen, experiment.InputNeuronCount,
                                              experiment.OutputNeuronCount,
                                              experiment.DefaultNeatParameters.pInitialPopulationInterconnections,
                                              experiment.DefaultNeatParameters.populationSize)),
                                              popEval,
                //new SingleFilePopulationEvaluator(new PokerNetworkEvaluator(), experiment.SuggestedActivationFunction),
                experiment.DefaultNeatParameters);

            Console.WriteLine("Starting real evolution");
            for (ulong i = 0; true; i++)
            {
                Console.WriteLine("Generation {0}", i + 1);
                ea.PerformOneGeneration();
                Console.WriteLine("Champion Fitness={0}", ea.BestGenome.Fitness);
                var doc = new XmlDocument();
                XmlGenomeWriterStatic.Write(doc, (NeatGenome)ea.BestGenome);
                FileInfo oFileInfo = new FileInfo("genomes_simple\\" + "bestGenome" + i.ToString() + ".xml");
                doc.Save(oFileInfo.FullName);
            }
        }
        */
        private static List<CachedHand> LoadCachedHands()
        {
            Console.WriteLine("Loading cached hands");
            List<CachedHand> cachedHands;
            XmlSerializer ser = new XmlSerializer(typeof(CachedHands));
            using (TextReader txt = new StreamReader("test.xml"))
                cachedHands = ((CachedHands)ser.Deserialize(txt)).Hands;
            return cachedHands;
        }
    }
}