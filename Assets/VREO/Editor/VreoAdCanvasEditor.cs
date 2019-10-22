using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VREO
{
	[CustomEditor(typeof(VreoAdCanvas))]
	[CanEditMultipleObjects]
	public class VreoAdCanvasEditor : Editor
	{
		SerializedProperty mediaType;
		SerializedProperty playOnAwake;
		SerializedProperty autoPlayNew;
		SerializedProperty initialRandomDelay;
		SerializedProperty imageDuration;

		private void OnEnable()
        {
			mediaType = serializedObject.FindProperty("mediaType");
			playOnAwake = serializedObject.FindProperty("playOnAwake");
			autoPlayNew = serializedObject.FindProperty("autoPlayNew");
			initialRandomDelay = serializedObject.FindProperty("initialRandomDelay");
			imageDuration = serializedObject.FindProperty("imageDuration");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(mediaType);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(playOnAwake);
            EditorGUILayout.PropertyField(autoPlayNew);


            switch ((VreoAdCanvas.MediaType)mediaType.enumValueIndex)
            {
                case VreoAdCanvas.MediaType.Image:
                case VreoAdCanvas.MediaType.Banner:
                case VreoAdCanvas.MediaType.LogoSquare:
                case VreoAdCanvas.MediaType.LogoWide:
                    EditorGUILayout.PropertyField(imageDuration);
                    break;

                case VreoAdCanvas.MediaType.Movie:
                    EditorGUILayout.PropertyField(initialRandomDelay);
                    break;

            }

			serializedObject.ApplyModifiedProperties();
		}
	}
}