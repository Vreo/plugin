using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class EditorWebRequestCoroutine
{
    public bool isDone;

    private IEnumerator _coroutine;

    public void StartCoroutine(UnityWebRequest request, Action onComplete, Action<string> onError)
    {
        _coroutine = SendRequestCoroutine(request, onComplete, onError);
    }

    public void Update()
    {
        _coroutine?.MoveNext();
    }

    IEnumerator SendRequestCoroutine(UnityWebRequest request, Action onComplete, Action<string> onError)
    {
        request.SendWebRequest();

        while (!request.isDone)
        {
            yield return 0;
        }

        if (request.isNetworkError || request.isHttpError)
        {
            Debug.Log(request.error);
            onError?.Invoke(request.error);
        }
        else
        {
            Debug.Log(request.responseCode);
            onComplete?.Invoke();
        }

        isDone = true;
    }
}