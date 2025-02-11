using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    protected static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance == null && !IsQuittingGame)
            {
                
                T attemptLoadFromResources = Resources.Load<T>(typeof(T).Name);
                if (attemptLoadFromResources != null)
                {
                    _instance = GameObject.Instantiate(attemptLoadFromResources).GetComponent<T>();
                    _instance.gameObject.name = typeof(T).Name;
                } else
                {
                    GameObject newGameObject = new GameObject(typeof(T).Name);
                    _instance = newGameObject.AddComponent<T>();
                }
            }
            return _instance;
        }
    }

    protected bool creationFailed = false;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BeforeFirstSceneLoaded()
    {
        IsQuittingGame = false;
        _instance = null;
    }
    // Start is called before the first frame update
    protected virtual void Awake() {
        IsQuittingGame = false;
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            creationFailed = true;
            return;
        }
        _instance = (T)(this as T);
        DontDestroyOnLoad(gameObject);
    }
    
    public static bool IsQuittingGame {get; protected set;}
    public virtual void OnApplicationQuit() {
        IsQuittingGame = true;
    }

}
