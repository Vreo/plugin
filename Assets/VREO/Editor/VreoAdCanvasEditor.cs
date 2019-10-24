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
	            case VreoAdCanvas.MediaType.MediumRectangle:
	            case VreoAdCanvas.MediaType.LargeRectangle:
	            case VreoAdCanvas.MediaType.WideSkyscraper:
	            case VreoAdCanvas.MediaType.Leaderboard:
                    EditorGUILayout.PropertyField(imageDuration);
                    break;

                case VreoAdCanvas.MediaType.PortraitVideo:
                case VreoAdCanvas.MediaType.LandscapeVideo:
                    EditorGUILayout.PropertyField(initialRandomDelay);
                    break;

            }

			serializedObject.ApplyModifiedProperties();
		}
	}
}