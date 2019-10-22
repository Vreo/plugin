using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSMonitor : MonoBehaviour 
{   
	private float deltaTime = 0;

	private void Awake()
    {
        Application.targetFrameRate = 60;
    }
    
    void OnGUI()
    {
        //FPS
        GUIStyle debugTextStyle = new GUIStyle();
        debugTextStyle.fontSize = (int)(25f * (Screen.width / 2048f));

        float fMsec = deltaTime * 1000.0f;
        float fps = 1.0f / deltaTime;
        //string fpsText = string.Format("{1:0.} FPS : {0:0.0} ms", fMsec, fps);
        string fpsText = string.Format("{0:0.} FPS", fps);
        debugTextStyle.normal.textColor = Color.Lerp(Color.red, Color.Lerp(Color.cyan, Color.blue, 0.5f), fps / 60f);
        GUI.Label(new Rect(new Vector2(20, 10), new Vector2(400, 200)), fpsText, debugTextStyle);
    }

    private void Update()
    {
        //FPS
        deltaTime += ((Time.deltaTime - deltaTime) * 0.1f);
    }
}
