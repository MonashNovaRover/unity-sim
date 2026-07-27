using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class NamedObject<T>
{
    public string name;
    public T obj;
}

public class LoadScene : MonoBehaviour
{
    // Dict does not appear in the editor so use this instead
    public List<NamedObject<string>> scenes;
    public string defaultScene;
    
    public static string GetArg(string name, string defaultValue)
    {
        //For now, always return the default value when in the editor
#if !UNITY_EDITOR
        string argString = name + '=';

        string[] args = System.Environment.GetCommandLineArgs();
       
        foreach (string arg in args)
        {
            if (arg.StartsWith(argString))
            {
                string result = arg.Substring(argString.Length);
                Console.WriteLine("Value for '" + name + "' is '" + result + "'");

                return result;
            }
        }
#endif
        Console.WriteLine("No argument specified for '" + name + "', defaulting to '" + defaultValue + "'");
        
        return defaultValue;
    }
    void Start()
    {
        string sceneName = GetArg("world", defaultScene);
        string scene = scenes.First(scene => scene.name == sceneName).obj;

        SceneManager.LoadScene(scene);
    }
}
