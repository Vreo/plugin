using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;

namespace VREO
{
	[CustomEditor(typeof(VreoAdCanvas))]
	[CanEditMultipleObjects]
	public class VreoAdCanvasEditor : Editor
	{
		SerializedProperty _mediaType;
		SerializedProperty _playOnAwake;
		SerializedProperty _autoPlayNew;
		SerializedProperty _initialRandomDelay;
		SerializedProperty _imageDuration;
		SerializedProperty _spotId;

		SerializedProperty _proximity;

		SerializedProperty _isClickable;

		SerializedProperty _isRegistered;
		
		void OnEnable()
        {
			_mediaType = serializedObject.FindProperty("mediaType");
			_playOnAwake = serializedObject.FindProperty("playOnAwake");
			_autoPlayNew = serializedObject.FindProperty("autoPlayNew");
			_initialRandomDelay = serializedObject.FindProperty("initialRandomDelay");
			_imageDuration = serializedObject.FindProperty("imageDuration");
			_spotId = serializedObject.FindProperty("spotId");
			_proximity = serializedObject.FindProperty("proximity");
			_isClickable = serializedObject.FindProperty("isClickable");
			_isRegistered = serializedObject.FindProperty("isRegistered");
			

			if (!Application.isPlaying && !_isRegistered.boolValue && !IsPrefabMode())
			{
				var registerPopup = EditorWindow.GetWindow<VreoAdCanvasSettingsPopup>("Register Ad spot");
				registerPopup.maxSize = new Vector2(400, 115);
				registerPopup.minSize = registerPopup.maxSize;
				registerPopup.serializedObject = serializedObject;
				registerPopup.ShowPopup();
			}
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_mediaType);
            if (EditorGUI.EndChangeCheck())
            {
	            var adCanvas = GetSelectedAdCanvas();
	            if (adCanvas != null)
	            {
		            adCanvas.OnMediaTypeChanged((VreoAdCanvas.MediaType)_mediaType.enumValueIndex);
	            }
            }

            if (!IsPrefabMode())
            {
	            EditorGUILayout.PropertyField(_spotId);
            
	            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_spotId.stringValue) || !_isRegistered.boolValue);
            
	            if(GUILayout.Button("Unregister Ad Spot"))
	            {
		            VreoCommunicate.RequestUnregisterAd(_spotId.stringValue, () =>
		            {
			            Debug.Log($"Ad spot with ID {_spotId.stringValue} was unregistered.");
		            }, error =>
		            {
			            Debug.LogError($"An error occured while registering ad spot with ID {_spotId.stringValue}. Error: {error}");
		            });
	            }
            }
            
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_playOnAwake);
            EditorGUILayout.PropertyField(_autoPlayNew);
            EditorGUILayout.PropertyField(_isClickable);

            switch ((VreoAdCanvas.MediaType)_mediaType.enumValueIndex)
            {
	            case VreoAdCanvas.MediaType.MediumRectangle:
	            case VreoAdCanvas.MediaType.LargeRectangle:
	            case VreoAdCanvas.MediaType.WideSkyscraper:
	            case VreoAdCanvas.MediaType.Leaderboard:
                    EditorGUILayout.PropertyField(_imageDuration);
                    break;

                case VreoAdCanvas.MediaType.PortraitVideo:
                case VreoAdCanvas.MediaType.LandscapeVideo:
                    EditorGUILayout.PropertyField(_initialRandomDelay);
                    break;
            }

            EditorGUILayout.Slider(_proximity, 0.0f, 100.0f);

			serializedObject.ApplyModifiedProperties();
		}
        
        VreoAdCanvas GetSelectedAdCanvas()
        {
	        var adCanvas = Selection.activeGameObject;
	        if (!adCanvas)
		        return null;

	        var adCanvasComp = adCanvas.GetComponent<VreoAdCanvas>();
	        if (!adCanvasComp)
				return null;

	        return adCanvasComp;
        }

        bool IsPrefabMode()
        {
	        var adCanvas = GetSelectedAdCanvas();
	        if (adCanvas != null)
	        {
		        var prefabStage = PrefabStageUtility.GetPrefabStage(adCanvas.gameObject);
		        if (prefabStage != null)
		        {
			        return true;
		        }
	        }

	        return false;
        }
	}
}