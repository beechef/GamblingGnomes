using Game.Runtime.Audio;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Audio
{
	[CustomEditor(typeof(AudioEvent))]
	public class AudioEventEditor : OdinEditor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			EditorGUILayout.Space();

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Play")) AudioPreview.Play((AudioEvent)target);
				if (GUILayout.Button("Stop")) AudioPreview.StopAll();
			}
		}
	}
}
