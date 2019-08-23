using UnityEngine;

public static class Util
{
    static System.Diagnostics.Stopwatch stopwatch;
    static long count;

    static Util()
    {
        stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        count = 0;
    }

    public static void SWLog(string str) { Debug.Log(stopwatch.Elapsed + ": " + str); }

    public static void CLog(string str){ Debug.Log( System.String.Format("{0,6}  ",count++) + str); }
}
