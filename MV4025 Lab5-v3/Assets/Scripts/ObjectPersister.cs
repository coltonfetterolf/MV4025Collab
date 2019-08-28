using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ObjectPersister 
{
    static Dictionary<string, object> dict;

    static ObjectPersister()
    {
        dict = new Dictionary<string, object>();
    }

    public static void Add(string name, object obj)
    {
        dict[name] = obj;
    }

    public static object Get(string name)
    {
        if (dict.ContainsKey(name) )
            return dict[name];
        return null;
    }
}
