using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VREO;

public class VreoAdCanvasSettingsPopup : EditorWindow
{
    static void Init()
    {
        VreoAdCanvasSettingsPopup window = ScriptableObject.CreateInstance<VreoAdCanvasSettingsPopup>();
        window.name = "Register Ad Spot";
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 30);

        window.ShowPopup();
    }

    VreoAdCanvas GetSelectedAdCanvas()
    {
        var adCanvas = Selection.activeGameObject;
        if (!adCanvas)
        {
            Close();
            return null;
        }
        
        var adCanvasComp = adCanvas.GetComponent<VreoAdCanvas>();
        if (!adCanvasComp)
        {
            Close();
            return null;
        }

        return adCanvasComp;
    }

    private string SpotId;

    public SerializedObject serializedObject;
    
    private void OnSelectionChange()
    {
        var adCanvas = GetSelectedAdCanvas();
        if (adCanvas && adCanvas.isRegistered)
        {
            Close();
        }
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Register newly created VREO_AD_CANVAS object by setting its unique Spot Id:", EditorStyles.wordWrappedLabel);
        
        GUILayout.Space(5);

        SpotId = EditorGUILayout.TextField("Spot Id: ", SpotId);
        
        GUILayout.Space(5);
        
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(SpotId));
        
        if (GUILayout.Button("Register Ad Spot"))
        {
            var spotId = serializedObject.FindProperty("spotId");
            var isRegistered = serializedObject.FindProperty("isRegistered");
            
            spotId.stringValue = SpotId;
            isRegistered.boolValue = true;

            serializedObject.ApplyModifiedProperties();

            Close();
        }
        
        EditorGUI.EndDisabledGroup();
    }
}
