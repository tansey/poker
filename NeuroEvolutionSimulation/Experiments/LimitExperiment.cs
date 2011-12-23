using System.Collections;
using SharpNeatLib.Evolution;
using SharpNeatLib.Experiments;
using SharpNeatLib.NeuralNetwork;
using NeuroEvolutionSimulation.Players;

namespace NeuroEvolutionSimulation.Experiments
{
    public class LimitExperiment : IExperiment
    {
        private IActivationFunction activationFunction = new SteepenedSigmoid();

        public IActivationFunction ActivationFunction
        {
            get { return activationFunction; }
            set { activationFunction = value; }
        }

        #region IExperiment Members

        public AbstractExperimentView CreateExperimentView()
        {
            return null;
        }

        public NeatParameters DefaultNeatParameters
        {
            get
            {
                var np = new NeatParameters();
                np.connectionWeightRange = 3;
                np.pMutateAddConnection = 0.03;
                np.pMutateAddNode = 0.01;
                np.populationSize = 150;
                np.elitismProportion = 0.2;
                
                return np;
            }
        }

        public string ExplanatoryText
        {
            get
            {
                return
                    @"An experiment that uses competitive co-evolution of 
                            neural networks to play limit texas hold'em.";
            }
        }

        public int InputNeuronCount
        {
            get { return SimpleLimitNeuralNetPlayer2.INPUT_NODE_COUNT; }
        }

        public void LoadExperimentParameters(Hashtable parameterTable) {}

        public int OutputNeuronCount
        {
            get { return 3; }
        }

        public IPopulationEvaluator PopulationEvaluator
        {
            get { return null; }
        }

        public void ResetEvaluator(IActivationFunction activationFn) {
            
        }

        public IActivationFunction SuggestedActivationFunction
        {
            get { return activationFunction; }
        }

        #endregion
    }
}