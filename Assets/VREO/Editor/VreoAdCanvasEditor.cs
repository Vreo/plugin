using UnityEditor;
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
		
		SerializedProperty _isRegistered;
		
		void OnEnable()
        {
			_mediaType = serializedObject.FindProperty("mediaType");
			_playOnAwake = serializedObject.FindProperty("playOnAwake");
			_autoPlayNew = serializedObject.FindProperty("autoPlayNew");
			_initialRandomDelay = serializedObject.FindProperty("initialRandomDelay");
			_imageDuration = serializedObject.FindProperty("imageDuration");
			_spotId = serializedObject.FindProperty("spotId");

			_isRegistered = serializedObject.FindProperty("isRegistered");

			if (!Application.isPlaying && !_isRegistered.boolValue)
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

            EditorGUILayout.PropertyField(_mediaType);
            EditorGUILayout.PropertyField(_spotId);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_playOnAwake);
            EditorGUILayout.PropertyField(_autoPlayNew);


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

			serializedObject.ApplyModifiedProperties();
		}
	}
}