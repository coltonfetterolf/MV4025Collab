using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class BrainRegistry
{
    static Dictionary<string, object> dict;

    static BrainRegistry()
    {
        dict = new Dictionary<string, object>();
    }

    public static void Add(string name, object obj)
    {
        dict[name] = obj;
    }

    public static object Get(string name)
    {
        if (dict.ContainsKey(name))
            return dict[name];
        return null;
    }

    public static List<string> GetNames()
    {
        return new List<string>(dict.Keys);
    }
}
