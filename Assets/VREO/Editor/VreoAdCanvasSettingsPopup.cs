using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VREO;

public class VreoAdCanvasSettingsPopup : EditorWindow
{
    enum RegistrationState
    {
        eNone,
        eError,
        eDone
    }

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
    private string Error;
    private RegistrationState Registration;

    public SerializedObject serializedObject;

    private void OnSelectionChange()
    {
        var adCanvas = GetSelectedAdCanvas();
        if (adCanvas)
        {
            SpotId = string.Empty;
            Error = string.Empty;
            Registration = RegistrationState.eNone;
            
            if (adCanvas.isRegistered)
                Close();
        }
        else
        {
            Close();
        }
    }

    void OnGUI()
    {
        if (GetSelectedAdCanvas() == null)
            Close();

        EditorGUILayout.LabelField("Register newly created VREO_AD_CANVAS object by setting its unique Spot Id:",
            EditorStyles.wordWrappedLabel);

        GUILayout.Space(5);

        SpotId = EditorGUILayout.TextField("Spot Id: ", SpotId);

        GUILayout.Space(5);

        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(SpotId));

        if (GUILayout.Button("Register Ad Spot"))
        {
            VreoCommunicate.RequestRegisterAd(SpotId, () =>
            {
                Debug.Log($"Ad spot with ID {SpotId} was registered.");
                Registration = RegistrationState.eDone;
            }, error =>
            {
                Debug.LogError($"An error occured while registering ad spot with ID {SpotId}. Error: {error}");
                Registration = RegistrationState.eError;
                Error = error;
                Repaint();
            });
        }

        EditorGUI.EndDisabledGroup();

        if (Registration == RegistrationState.eError)
        {
            GUILayout.Space(5);

            EditorGUILayout.LabelField($"An error occured while registering ad spot with ID {SpotId}. Error: {Error}", EditorStyles.wordWrappedLabel);
        }

        if (Registration == RegistrationState.eDone)
        {
            var spotId = serializedObject.FindProperty("spotId");
            var isRegistered = serializedObject.FindProperty("isRegistered");

            spotId.stringValue = SpotId;
            isRegistered.boolValue = true;

            serializedObject.ApplyModifiedProperties();

            Close();
        }
    }

    void Update()
    {
        // in order to keep editor "coroutines" alive EditorWebRequestHelper should be manually updated every frame
        EditorWebRequestHelper.Instance.UpdateExternal();
    }
}