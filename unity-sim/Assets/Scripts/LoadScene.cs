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
    public List<NamedObject<string>> scenes;

    public static string GetArg(string name, string defaultValue)
    {
        string argString = name + '=';
        //string[] args = System.Environment.GetCommandLineArgs();
        string[] args = { "robot=default", "scene=ARC2025" };

        foreach (string arg in args)
        {
            if (arg.StartsWith(argString))
            {
                return arg.Substring(argString.Length);
            }
        }

        return defaultValue;
    }
    void Start()
    {
        string sceneName = GetArg("scene", "ARC2025");
        string scene = scenes.First(scene => scene.name == sceneName).obj;

        SceneManager.LoadScene(scene);
    }
}
