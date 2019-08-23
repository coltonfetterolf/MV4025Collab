using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

using ConvNetSharp.Core;
using ConvNetSharp.Core.Layers.Double;
using ConvNetSharp.Core.Training.Double;
using ConvNetSharp.Volume;
using ConvNetSharp.Volume.Double;

using System.Runtime.Serialization;

using System.IO;

[System.Serializable]
public class Brain
{

    public interface IAIState
    {
        double[] toDoubleArray();
        string ToString();
    }

    public class Experience
    {
        public IAIState state_before, state_after;
        public int action;
        public float reward;
        public Experience(IAIState state_before, int action, float reward, IAIState state_after)
        {
            this.state_before = state_before;
            this.action = action;
            this.reward = reward;
            this.state_after = state_after;
        }
        override public string ToString()
        {
            string result = "Experience ";
            result += state_before.ToString();
            result += " action " + action;
            result += " reward " + reward;
            result += " " + state_after.ToString();
            return result;
        }
    }

    public class ExperienceReplayBuffer
    {
        Experience[] buffer;
        int count = 0;

        public ExperienceReplayBuffer(int size)
        {
            buffer = new Experience[size];
        }

        public void Add(Experience exp)
        {
            if (count < buffer.Length)
                buffer[count] = exp;
            else
            {
                int index = (int)Mathf.Floor(Random.Range(0f, buffer.Length));
                buffer[index] = exp;
            }
            ++count;
        }

        public Experience GetRandom()
        {
            int max_index = GetNumber();
            int index = (int)Mathf.Floor(Random.Range(0f, max_index));
            return buffer[index];
        }

        public int GetNumber()
        {
            return Mathf.Min(count, buffer.Length);
        }
    }

    int erb_size;
    float epsilon; // initial value
    float epsilon_decrement; // per unit time
    float epsilon_min;
    int n_states, n_actions;
    Net<double> net;
    int batch_size;
    float discount_factor;
    float learning_rate;
    int num_hidden_units;
    string type_hidden_units;
    float leaky_relu_alpha;

    [NonSerialized] ExperienceReplayBuffer erBuffer;

    public Brain(int erb_size, float epsilon_init, float epsilon_decrement, float epsilon_min, int n_states, int n_actions, int batch_size, float gamma)
    {
        erBuffer = new ExperienceReplayBuffer(erb_size);
        this.erb_size = erb_size;
        epsilon = epsilon_init;
        this.epsilon_decrement = epsilon_decrement;
        this.epsilon_min = epsilon_min;
        this.n_states = n_states;
        this.n_actions = n_actions;
        this.batch_size = batch_size;

        learning_rate = ExperimentControl.Parameters.learning_rate;
        num_hidden_units = ExperimentControl.Parameters.num_hidden_units;
        type_hidden_units = ExperimentControl.Parameters.type_hidden_units;
        leaky_relu_alpha = 0.3f; // Matching default value for Keras
        discount_factor = ExperimentControl.Parameters.discount_factor;

        net = new Net<double>();
        net.AddLayer(new InputLayer(1, 1, n_states));
        if (num_hidden_units>0)
        {
            net.AddLayer(new FullyConnLayer(num_hidden_units));
            switch (type_hidden_units)
            {
                case "relu":
                    net.AddLayer(new ReluLayer());
                    break;
                case "leaky-relu":
                    net.AddLayer(new LeakyReluLayer(leaky_relu_alpha));
                    break;
                case "sigmoid":
                    net.AddLayer(new SigmoidLayer());
                    break;
                case "tanh":
                    net.AddLayer(new TanhLayer());
                    break;
                default:
                    throw new Exception("Unknown layer type: " + type_hidden_units);
            }
        }
        net.AddLayer(new FullyConnLayer(n_actions));
        net.AddLayer(new RegressionLayer());
    }

    public void OnStart()
    {
        //perf = new PerformanceStats();
    }

    public void AddExperience(Experience exp)
    {
        erBuffer.Add(exp);
        ExperimentControl.AddPerformanceData(Time.fixedDeltaTime, exp.reward);
    }

    public int SelectAction(IAIState current_state)
    {
        float effective_epsilon = Mathf.Max(epsilon - epsilon_decrement * Time.fixedTime, epsilon_min);
        if (ExperimentControl.testMode)
            effective_epsilon = 0f;
        if (Random.Range(0f, 1f) < effective_epsilon)
        {
            //Debug.Log("AISelectAction(): exploratory action at epsilon " + epsilon);
            return Random.Range(0, n_actions); // Uniform distribution on 0 to n_actions-1
        }
        //Debug.Log("AISelectAction(): exploitive action at epsilon " + epsilon);
        var x_array = current_state.toDoubleArray();
        var x = BuilderInstance.Volume.From(x_array, new Shape(x_array.Length));
        var y = net.Forward(x);
        /*
        Util.CLog("Brain:SelectAction, x "+ x + " y " + y);
        for (var i=0;i< net.GetParametersAndGradients().Count; i++)
            Util.CLog("Brain:SelectAction, i "+i+" net.GetParametersAndGradients()[i].Volume " + net.GetParametersAndGradients()[i].Volume);
        */
        double best_score = System.Double.NegativeInfinity;
        int best_index = -1;
        string debug_str = "action scores ";
        for (int i = 0; i < n_actions; i++)
        {
            debug_str += y.Get(i) + " ";
            if (y.Get(i) > best_score)
            {
                best_score = y.Get(i);
                best_index = i;
            }
        }
        //Debug.Log(debug_str);
        return best_index;
    }

    public void Learn()
    {
        if (erBuffer.GetNumber() < 2 * batch_size)
            return;
        if (ExperimentControl.testMode)
            return;
        Volume x_vol, y_vol;
        GenerateSample(out x_vol, out y_vol);
        var trainer = new SgdTrainer(net) { LearningRate = learning_rate, BatchSize = batch_size };
        trainer.Train(x_vol, y_vol);
    }

    void GenerateSample(out Volume x_vol, out Volume y_vol)
    {
        x_vol = new Volume(new NcwhVolumeStorage<double>(new Shape(1, 1, n_states, batch_size)));
        y_vol = new Volume(new NcwhVolumeStorage<double>(new Shape(1, 1, n_actions, batch_size)));
        Volume<double> x_vol_single = new Volume(new NcwhVolumeStorage<double>(new Shape(1, 1, n_states, 1)));
        for (int batch = 0; batch < batch_size; batch++)
        {
            Brain.Experience exp = erBuffer.GetRandom();
            double[] state_before = exp.state_before.toDoubleArray();
            for (int j = 0; j < n_states; j++)
            {
                x_vol_single.Set(0, 0, j, 0, state_before[j]);
                x_vol.Set(0, 0, j, batch, state_before[j]);
            }
            Volume<double> y_vol_single = net.Forward(x_vol_single);
            // All y values should be unchanged, except for the selected action
            for (int i = 0; i < n_actions; i++)
                y_vol.Set(0, 0, i, batch, y_vol_single.Get(i));

            // Determine max Q from state_after
            double[] state_after = exp.state_after.toDoubleArray();
            for (int j = 0; j < n_states; j++)  // changed
            {
                x_vol_single.Set(0, 0, j, 0, state_after[j]);
            }
            y_vol_single = net.Forward(x_vol_single);
            double max_Q = y_vol_single.Get(0);
            for (int i = 1; i < n_actions; i++)
            {
                if (y_vol_single.Get(i) > max_Q)
                    max_Q = y_vol_single.Get(i);
            }
            double desired_Q = exp.reward + discount_factor * max_Q;
            //Debug.Log("****************************************Generate Sample exp num " + batch + exp.ToString() + " new target y " + desired_Q);
            // y value for the selected action overwritten
            y_vol.Set(0, 0, exp.action, batch, desired_Q);
        }
    }
    
    [OnDeserialized]
    void Rebuild()
    {
        erBuffer = new ExperienceReplayBuffer(this.erb_size);
    }

}
