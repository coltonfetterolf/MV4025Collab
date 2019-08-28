using System;
using System.Collections.Generic;
using UnityEngine;

public class PerformanceStats
{
    public int n_updates;
    public float start_time;
    public float runtime;
    public float running_ave_reward;
    public float reward_slope;

    public PerformanceStats()
    {
        this.n_updates = 0;
        this.running_ave_reward = 0f;
    }

    public PerformanceStats(PerformanceStats perf)
    {
        this.n_updates = perf.n_updates;
        this.running_ave_reward = perf.running_ave_reward;
    }

    public void AddReward(float reward)
    {
        ++n_updates;
        runtime = Time.fixedTime - start_time;
        float nu = 1f / (float)n_updates;
        running_ave_reward = (1f - nu) * running_ave_reward + nu * reward;
    }

    public void Begin()
    {
        this.start_time = Time.fixedTime;
    }
}

abstract public class LessonCompletionTest
{
    abstract public bool LessonFinished(PerformanceStats perf);
}

public class UpdatesTest : LessonCompletionTest
{
    int update_thresh;
    int total_updates;

    public UpdatesTest( int update_thresh )
    {
        this.update_thresh = update_thresh;
        this.total_updates = 0;
    }

    override public bool LessonFinished(PerformanceStats perf)
    {
        total_updates += perf.n_updates;
        if (total_updates >= update_thresh)
            return true;
        return false;
    }
}

public class RuntimeTest : LessonCompletionTest
{
    float runtime_thresh;

    public RuntimeTest(float runtime_thresh)
    {
        this.runtime_thresh = runtime_thresh;
    }

    override public bool LessonFinished(PerformanceStats perf)
    {
        if (Time.fixedTime - perf.start_time >= runtime_thresh)
            return true;
        return false;
    }
}

public class RewardTest : LessonCompletionTest
{
    int reward_thresh;
    float failure_timeout;

    public RewardTest(int reward_thresh, float failure_timeout)
    {
        this.reward_thresh = reward_thresh;
        this.failure_timeout = failure_timeout;
    }

    override public bool LessonFinished(PerformanceStats perf)
    {
        if (perf.running_ave_reward >= reward_thresh)
            return true;
        if (failure_timeout > 0  && Time.fixedTime - perf.start_time >= failure_timeout)
            return true;
        return false;
    }
}

public class SlopeTest : LessonCompletionTest
{
    List<float> rewards;
    float slope_thresh;
    int window_size;

    public SlopeTest(float slope_thresh, int window_size)
    {
        this.slope_thresh = slope_thresh;
        rewards = new List<float>();
        this.window_size = window_size;
    }

    override public bool LessonFinished(PerformanceStats perf)
    {
        rewards.Add(perf.running_ave_reward);
        if (rewards.Count > window_size)
            rewards.RemoveAt(0);
        if (rewards.Count < window_size)
            return false;
        float slope = (rewards[window_size - 1] - rewards[0])/(float)window_size;
        if (slope < slope_thresh)
            return true;
        return false;
    }
}

public class Lesson
{
    public string sceneName;
    public bool testMode;
    public PerformanceStats perf;
    float lastRewardTime;
    float rewardTimeoutTime;
    float rewardTimeoutReward;
    List<LessonCompletionTest> tests;

    public Lesson(string sceneName, bool testMode = false, float rewardTimeoutTime = -1f)
    {
        this.sceneName = sceneName;
        this.testMode = testMode;
        this.perf = new PerformanceStats();
        this.lastRewardTime = Time.fixedTime;
        this.rewardTimeoutTime = rewardTimeoutTime;
        this.tests = new List<LessonCompletionTest>();
    }

    public void Begin()
    {
        this.perf.Begin();
        this.lastRewardTime = Time.fixedTime;
    }

    public void AddTest (LessonCompletionTest test)
    {
        tests.Add(test);
    }

    public void AddPerformanceData(float delta_runtime, float reward, out bool rewardTimeoutOccurred)  // Called by Brain
    {
        perf.AddReward(reward);
        if (reward != 0f)
            lastRewardTime = Time.fixedTime;
        rewardTimeoutOccurred = false;
        //Debug.Log("In Lesson:AddPerformanceData, reward " + reward + " rewardTimeoutTime " + rewardTimeoutTime + " Time.fixedTime " + Time.fixedTime + " lastRewardTime " + lastRewardTime);
        if (rewardTimeoutTime > 0 && Time.fixedTime - lastRewardTime > rewardTimeoutTime)
        {
            rewardTimeoutOccurred = true;
            Debug.Log("Lesson:AddPerformanceData, reward timeout occurred at "+ System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.ffff"));
            lastRewardTime = Time.fixedTime;
        }
    }

    public bool LessonFinished()
    {
        foreach (LessonCompletionTest test in tests) {
            if (test.LessonFinished(perf))
                return true;
        }
        return false;
    }

    public void AddMOEs(Dictionary<string, float> moes)
    {
        moes["n_updates"] = perf.n_updates;
        moes["runtime"] = perf.runtime;
        moes["running_ave_reward"] = perf.running_ave_reward;
    }
}

public class Curriculum
{
    List<Lesson> lessonSequence = new List<Lesson>();
    int current_lesson_index;

	public Curriculum()
	{
        current_lesson_index = -1;
	}

    public void AddLesson(Lesson lesson)
    {
        lessonSequence.Add(lesson);
    }

    public Lesson GetCurrentLesson()
    {
        return lessonSequence[current_lesson_index];
    }

    public Lesson NextLesson()
    {
        if (current_lesson_index==-1)
            current_lesson_index = 0;
        if (lessonSequence[current_lesson_index].LessonFinished())
            ++current_lesson_index;
        if (current_lesson_index == lessonSequence.Count)
            return null;
        lessonSequence[current_lesson_index].Begin();
        return lessonSequence[current_lesson_index];
    }

    public Dictionary<string, float> GetMOEs()
    {
        Dictionary<string, float> moes = new Dictionary<string, float>();
        foreach (Lesson lesson in lessonSequence)
            lesson.AddMOEs(moes);
        return moes;
    }

    public void AddPerformanceData(float delta_runtime, float reward, out bool rewardTimeoutOccurred)
    {
        lessonSequence[current_lesson_index].AddPerformanceData(delta_runtime, reward, out rewardTimeoutOccurred);
    }
}
