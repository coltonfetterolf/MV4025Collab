using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Threading;
using UnityEngine.SceneManagement;

public static class ExperimentControl
{
    private static Dictionary<string, string> json_parameters;

    private static Mutex mut = new Mutex(false,"CoAMutex");

    public static bool testMode = false;

    public static bool initialized = false;

    static string current_design_point;

    static string brainName = "my brain"; // Name of brain whose performance will be measured

    public enum MobilityModel { NoGradePenalty, SlightGradePenalty };

    enum CurricName { MoveToEnemyFromKills, MoveToEnemyFromKills2, LargeScenario, DeterminismTest, Test, Lab5, Lab5Test };

    static CurricName curric_name = CurricName.Lab5;
    //static CurricName curric_name = CurricName.Lab5Test;
    //static CurricName curric_name = CurricName.MoveToEnemyFromKills2;
    //static CurricName curric_name = CurricName.Test;
    //static CurricName curric_name = CurricName.LargeScenario;
    static Curriculum curric;

    public static bool run_complete = false;

    public static class Parameters
    {
        public static float train_duration;
        public static float test_duration;
        public static float reward_timeout;
        public static bool respawnOnDeath = true;
        public static float respawnWidth;
        public static bool load_brain;
        public static bool ranged_state;
        public static float learning_rate;
        public static int num_hidden_units;
        public static string type_hidden_units; // relu, leaky-relu, sigmoid, or tanh
        public static float discount_factor; // Sometimes called gamma or lambda
        public static float loss_factor; // Penalty for friendly losses measured in kills
        public static MobilityModel mobility_model;
        public static int seed;

        // Apply user-supplied JSON values
        public static void SetValues()
        {
            seed = System.Environment.TickCount;
            if (ExperimentControl.json_parameters.ContainsKey("seed"))
                seed = int.Parse(json_parameters["seed"]);

            train_duration = 2400f;
            if (ExperimentControl.json_parameters.ContainsKey("train_duration"))
                train_duration = float.Parse(json_parameters["train_duration"]);

            test_duration = 60f;
            if (ExperimentControl.json_parameters.ContainsKey("test_duration"))
                test_duration = float.Parse(json_parameters["test_duration"]);

            reward_timeout = 10f;
            if (ExperimentControl.json_parameters.ContainsKey("reward_timeout"))
                reward_timeout = float.Parse(json_parameters["reward_timeout"]);

            respawnOnDeath = true;

            respawnWidth = 80f;
            if (ExperimentControl.json_parameters.ContainsKey("respawnWidth"))
                respawnWidth = float.Parse(ExperimentControl.json_parameters["respawnWidth"]);

            load_brain = false;
            if (ExperimentControl.json_parameters.ContainsKey("load_brain"))
                load_brain = bool.Parse(ExperimentControl.json_parameters["load_brain"]);

            ranged_state = true;
            if (ExperimentControl.json_parameters.ContainsKey("ranged_state"))
                ranged_state = bool.Parse(ExperimentControl.json_parameters["ranged_state"]);

            learning_rate = 0.1f;
            if (ExperimentControl.json_parameters.ContainsKey("learning_rate"))
                learning_rate = float.Parse(ExperimentControl.json_parameters["learning_rate"]);

            num_hidden_units = 0;
            if (ExperimentControl.json_parameters.ContainsKey("num_hidden_units"))
                num_hidden_units = int.Parse(ExperimentControl.json_parameters["num_hidden_units"]);

            type_hidden_units = "tanh";
            if (ExperimentControl.json_parameters.ContainsKey("type_hidden_units"))
                type_hidden_units = ExperimentControl.json_parameters["type_hidden_units"];

            discount_factor = 0.9f;
            if (ExperimentControl.json_parameters.ContainsKey("discount_factor"))
                discount_factor = float.Parse(json_parameters["discount_factor"]);

            loss_factor = 0.0f;
            if (ExperimentControl.json_parameters.ContainsKey("loss_factor"))
                loss_factor = float.Parse(json_parameters["loss_factor"]);

            mobility_model = MobilityModel.NoGradePenalty;
            if (ExperimentControl.json_parameters.ContainsKey("mobility_model"))
                mobility_model = (MobilityModel)System.Enum.Parse(typeof(MobilityModel), json_parameters["mobility_model"]);
        }
    }


    static ExperimentControl()
    {

        if (initialized) return;

        // Set timeScale impossibly high if this is a server build to run as fast as possible
        if (SystemInfo.graphicsDeviceID == 0)
            Time.timeScale = 100f;

        json_parameters = new Dictionary<string, string>();
        if (File.Exists("todo.txt"))
        {
            try
            {
                mut.WaitOne();
                string[] lines = File.ReadAllLines("todo.txt");
                current_design_point = lines[0];
                json_parameters = ParseJSONStringDict(lines[0]);
                string[] newArray = new string[lines.Length - 1];
                System.Array.Copy(lines, 1, newArray, 0, newArray.Length);

                string[] lines_running = new string[0];
                if (File.Exists("running.txt"))
                    lines_running = File.ReadAllLines("running.txt");
                string[] new_lines_running = new string[lines_running.Length + 1];
                System.Array.Copy(lines_running, 0, new_lines_running, 0, lines_running.Length);
                new_lines_running[lines_running.Length] = current_design_point;
                File.WriteAllLines("running.txt", new_lines_running);

                File.Delete("todo.txt");
                while (File.Exists("todo.txt"))
                    Thread.Sleep(20);
                if (newArray.Length > 0)
                    File.WriteAllLines("todo.txt", newArray);
                mut.ReleaseMutex();
            }
            catch (System.Exception e)
            {
                Debug.LogFormat("Read/write of experiment files failed: {0}", e.ToString());
            }
        }

        Parameters.SetValues();

        Random.InitState(Parameters.seed);

        curric = new Curriculum();

        if (curric_name == CurricName.MoveToEnemyFromKills)
        {
            // Learning to attack from kills alone
            // Requires about 240 seconds of training, but still fails occasionally

            Parameters.respawnOnDeath = true;
            Parameters.respawnWidth = 13f;
            
            Lesson lesson1 = new Lesson("close-kills", rewardTimeoutTime: Parameters.reward_timeout);
            lesson1.AddTest(new RuntimeTest(Parameters.train_duration));
            curric.AddLesson(lesson1);

            Lesson lesson2 = new Lesson("far-kills", testMode: true);
            lesson2.AddTest(new RuntimeTest(Parameters.test_duration));
            curric.AddLesson(lesson2);
        }

        if (curric_name == CurricName.MoveToEnemyFromKills2)
        {
            // Learning to concentrate force (2 units vs 2 units)

            Parameters.respawnOnDeath = true;
            Parameters.respawnWidth = 15f;

            Lesson lesson1 = new Lesson("close-kills2", rewardTimeoutTime: Parameters.reward_timeout);
            lesson1.AddTest(new RuntimeTest(Parameters.train_duration));
            curric.AddLesson(lesson1);

            Lesson lesson2 = new Lesson("far-kills2", testMode: true);
            lesson2.AddTest(new RuntimeTest(Parameters.test_duration));
            curric.AddLesson(lesson2);
        }

        if (curric_name == CurricName.LargeScenario)
        {
            Parameters.respawnOnDeath = true;
            Parameters.respawnWidth = 25f;

            Lesson lesson1 = new Lesson("large", rewardTimeoutTime: Parameters.reward_timeout);
            lesson1.AddTest(new RuntimeTest(Parameters.train_duration));
            curric.AddLesson(lesson1);

            Lesson lesson2 = new Lesson("large", testMode: true);
            lesson2.AddTest(new RuntimeTest(Parameters.test_duration));
            curric.AddLesson(lesson2);
        }

        if (curric_name == CurricName.DeterminismTest)
        {
            Lesson lesson1 = new Lesson("determinism test", rewardTimeoutTime: Parameters.reward_timeout);
            lesson1.AddTest(new RuntimeTest(Parameters.train_duration));
            curric.AddLesson(lesson1);
        }

        if (curric_name == CurricName.Test)
        {
            // Test only load (presumably pre-trained) brain from file

            Parameters.load_brain = true;

            Lesson lesson2 = new Lesson("large", testMode: true, rewardTimeoutTime: 10f);
            lesson2.AddTest(new RuntimeTest(Parameters.test_duration));
            curric.AddLesson(lesson2);
        }

        if (curric_name == CurricName.Lab5)
        {
            // Learning to concentrate force (2 units vs 1 units)

            Parameters.respawnOnDeath = true;
            Parameters.respawnWidth = 15f;

            Lesson lesson1 = new Lesson("Lab5", rewardTimeoutTime: Parameters.reward_timeout);
            lesson1.AddTest(new RuntimeTest(Parameters.train_duration));
            curric.AddLesson(lesson1);

            Lesson lesson2 = new Lesson("Lab5", testMode: true);
            lesson2.AddTest(new RuntimeTest(Parameters.test_duration));
            curric.AddLesson(lesson2);
        }

        if (curric_name == CurricName.Lab5Test)
        {
            // Test only load (presumably pre-trained) brain from file

            Parameters.load_brain = true;

            Parameters.respawnOnDeath = true;
            Parameters.respawnWidth = 15f;

            Lesson lesson2 = new Lesson("Lab5", testMode: true, rewardTimeoutTime: 10f);
            lesson2.AddTest(new RuntimeTest(Parameters.test_duration));
            curric.AddLesson(lesson2);
        }

        Lesson lesson = curric.NextLesson();
        testMode = lesson.testMode;
        SceneManager.LoadScene(lesson.sceneName);

        initialized = true;

        Debug.Log("Starting run at " + System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.ffff") + " using scene " + curric.GetCurrentLesson().sceneName);
        //Debug.Log("Unity time " + Time.time + " SimTime " + Time.fixedTime);
    }

    public static void ForceConstructorToRun()
    {

    }

    public static void Start()
    {

    }

    public static void AddPerformanceData(float delta_runtime, float reward)
    {
        bool rewardTimeoutOccurred;
        curric.AddPerformanceData(delta_runtime, reward, out rewardTimeoutOccurred);
        if (rewardTimeoutOccurred)
            SceneManager.LoadScene(curric.GetCurrentLesson().sceneName);
    }

    // Update is called once per frame
    public static void FixedUpdate()
    {
        if (ExperimentControl.run_complete)
            return;
        Lesson current_lesson = curric.GetCurrentLesson();
        Brain training_brain = (Brain)BrainRegistry.Get(brainName);
        if ( current_lesson.LessonFinished() )
        {
            Lesson next_lesson = curric.NextLesson();
            if (next_lesson != null)
            {
                Debug.Log("Starting new lesson at " + System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.ffff") + " using scene "+next_lesson.sceneName);
                //Debug.Log("Unity time " + Time.time + " SimTime " + Time.fixedTime);
                testMode = next_lesson.testMode;
                SceneManager.LoadScene(next_lesson.sceneName);
                Debug.Break();
            }
            else // curriculum finished
            {
                Debug.Log("Out of lessons: curric complete");
                mut.WaitOne();
                using (StreamWriter sw = File.AppendText("result.txt"))
                {
                    string str = "";
                    str += "end_time, " + System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.ffff") + ", ";
                    str += "runtime, " + Time.time + ", ";
                    foreach (var param in ExperimentControl.json_parameters.Keys)
                    {
                        str += param + ", " + ExperimentControl.json_parameters[param] + ", ";
                    }
                    List<string> brain_names = BrainRegistry.GetNames();
                    foreach (var nm in brain_names)
                    {
                        Dictionary<string, float> moes = curric.GetMOEs();
                        foreach (var moe_name in moes.Keys)
                        {
                            str += moe_name + ", ";
                            str += moes[moe_name] + ", ";
                        }
                    }
                    sw.WriteLine(str);
                }

                string[] lines_running = new string[0];
                if (File.Exists("running.txt"))
                    lines_running = File.ReadAllLines("running.txt");
                List<string> lst = new List<string>(lines_running);
                lst.Remove(current_design_point);
                string[] new_lines_running = lst.ToArray();
                File.WriteAllLines("running.txt", new_lines_running);

                mut.ReleaseMutex();

                run_complete = true;
            }
        }
    }

    static public void Update()
    {
        if (run_complete)
        {
            Application.Quit();
            #if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                UnityEditor.EditorApplication.isPlaying = false;
            }
            #endif
        }
    }

    // json_str must be an object whose values are all strings
    static public Dictionary<string,string> ParseJSONStringDict(string json_str)
    {
        Dictionary<string, string> result = new Dictionary<string, string>();
        string buf = json_str;
        void EatWhiteSpace()
        {
            if (buf == "")
                return;
            while (System.Char.IsWhiteSpace(buf[0]))
                buf = buf.Substring(1);
        }
        void Eat(char c)
        {
            if (buf[0] == c)
                buf = buf.Substring(1);
            else
                throw new System.Exception("In Eat(), invalid JSON string dictionary");
            EatWhiteSpace();
        }
        void EatOptional(char c)
        {
            if (buf[0] == c)
                buf = buf.Substring(1);
            EatWhiteSpace();
        }
        string GetName()
        {
            Eat('"');
            var ind = buf.IndexOf('"');
            if (ind == -1)
                throw new System.Exception("In GetName(), invalid JSON string dictionary");
            string name = buf.Substring(0, ind);
            buf = buf.Substring(ind);
            Eat('"');
            EatWhiteSpace();
            return name;
        }
        Eat('{');
        while (buf[0] != '}')
        {
            string key = GetName();
            Eat(':');
            string value = GetName();
            result[key] = value;
            EatOptional(',');
        }
        Eat('}');
        return result;
    }

    static public void ParseJSONStringDictTest()
    {
        string test_str = "{\"a\":\"1\",\"b\":\"bob\",\"c\":\"8.8\",\"d\":\"true\"}";
        Dictionary<string, string> dict = ParseJSONStringDict(test_str);
        string debug_str = "read dict with keys: ";
        foreach (var s in dict.Keys)
            debug_str += s + " ";
        debug_str += " values: ";
        foreach (var s in dict.Values)
            debug_str += s + " ";
        Debug.Log(debug_str);
    }
}
