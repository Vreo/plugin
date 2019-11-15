using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

[ExecuteInEditMode]
public class EditorWebRequestHelper : MonoBehaviour
{
    private static EditorWebRequestHelper _instance;

    private readonly List<EditorWebRequestCoroutine> _coroutines = new List<EditorWebRequestCoroutine>();

    // Prevent accidental EditorWebRequestHelper instantiation
    private EditorWebRequestHelper()
    {
    }

    public static EditorWebRequestHelper Instance
    {
        get
        {
            if (_instance == null) Init();

            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    internal static void Init()
    {
        if (ReferenceEquals(_instance, null))
        {
            var instances = FindObjectsOfType<EditorWebRequestHelper>();

            if (instances.Length > 1)
            {
                Debug.LogError(typeof(EditorWebRequestHelper) + " Something went really wrong " +
                               " - there should never be more than 1 " + typeof(EditorWebRequestHelper) +
                               " Reopening the scene might fix it.");
            }
            else if (instances.Length == 0)
            {
                var singleton = new GameObject {hideFlags = HideFlags.HideAndDontSave};
                _instance = singleton.AddComponent<EditorWebRequestHelper>();
                singleton.name = typeof(EditorWebRequestHelper).ToString();

                Debug.Log("[Singleton] An _instance of " + typeof(EditorWebRequestHelper) +
                          " is needed in the scene, so '" + singleton.name +
                          "' was created with DontDestroyOnLoad.");
            }
            else
            {
                Debug.Log("[Singleton] Using _instance already created: " + _instance.gameObject.name);
            }
        }
    }

    private void Update()
    {
        if (_coroutines.Any())
        {
            _coroutines.ForEach(c =>
            {
                if (!c.isDone)
                    c.Update();
            });

            _coroutines.RemoveAll(c => c.isDone);
        }
    }
    
    public void UpdateExternal()
    {
        Update();
    }

    public void SendRequest(UnityWebRequest request, Action onComplete, Action<string> onError)
    {
        var coroutine = new EditorWebRequestCoroutine();
        _coroutines.Add(coroutine);
        coroutine.StartCoroutine(request, onComplete, onError);
    }
}