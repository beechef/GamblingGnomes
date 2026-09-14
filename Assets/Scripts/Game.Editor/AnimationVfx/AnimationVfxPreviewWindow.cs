using System.Collections.Generic;
using System.Linq;
using Game.Runtime.AnimationVfx;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace Game.Editor.AnimationVfx
{
	// Scrubs a clip on a character in the open scene and plays its effects at the frames they are set to,
	// without entering Play mode. The pose goes through AnimationMode — the sampling the Animation window
	// itself uses — so closing the preview hands the character back exactly as it was and nothing in the
	// scene is dirtied. The effects are spawned by AnimationVfxCue.Spawn, the same call AnimationVfxPlayer
	// makes in game, and simulated forward to the scrubbed time, so what lines up here lines up in play.
	public class AnimationVfxPreviewWindow : EditorWindow
	{
		private const float SimulationStep = 1f / 60f;

		[SerializeField] private AnimationVfxCueDatabase _database;
		[SerializeField] private Animator _target;
		[SerializeField] private AnimationClip _clip;
		[SerializeField] private float _time;
		[SerializeField] private bool _loop = true;
		[SerializeField] private int _selectedCue = -1;

		private readonly List<Spawned> _spawned = new();

		private AnimationModeDriver _driver;
		private bool _playing;
		private double _lastTick;
		private float _simulatedTo = -1f;
		private string[] _boneNames = { "(root)" };
		private Vector2 _scroll;

		private struct Spawned
		{
			public AnimationVfxCue Cue;
			public GameObject Instance;
			public float BornAt;
		}

		[MenuItem("Tools/Animation VFX Preview")]
		private static void Open() => GetWindow<AnimationVfxPreviewWindow>("Animation VFX");

		private float FrameRate => _clip && _clip.frameRate > 0f ? _clip.frameRate : 30f;
		private int CurrentFrame => Mathf.RoundToInt(_time * FrameRate);
		private bool CanPreview => _target && _clip && !EditorApplication.isPlayingOrWillChangePlaymode;

		private void OnEnable()
		{
			_driver = CreateInstance<AnimationModeDriver>();
			_driver.hideFlags = HideFlags.HideAndDontSave;

			EditorApplication.update += Tick;
			SceneView.duringSceneGui += OnSceneGUI;
			Undo.undoRedoPerformed += Refresh;
			EditorApplication.playModeStateChanged += HandlePlayModeChanged;

			_boneNames = CollectBoneNames();
			Refresh();
		}

		private void OnDisable()
		{
			EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
			Undo.undoRedoPerformed -= Refresh;
			SceneView.duringSceneGui -= OnSceneGUI;
			EditorApplication.update -= Tick;

			StopPreview();

			if (_driver) DestroyImmediate(_driver);
		}

		private void HandlePlayModeChanged(PlayModeStateChange change) => StopPreview();

		private void OnGUI()
		{
			EditorGUI.BeginChangeCheck();

			_database = (AnimationVfxCueDatabase)EditorGUILayout.ObjectField("Cue Database", _database, typeof(AnimationVfxCueDatabase), false);
			_target = (Animator)EditorGUILayout.ObjectField(new GUIContent("Character", "An animator in the open scene. Its pose is borrowed while previewing and handed back afterwards."), _target, typeof(Animator), true);
			DrawClipPicker();

			if (EditorGUI.EndChangeCheck())
			{
				_boneNames = CollectBoneNames();
				_selectedCue = -1;
				_time = Mathf.Min(_time, _clip ? _clip.length : 0f);
				Refresh();
			}

			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				EditorGUILayout.HelpBox("The preview runs in edit mode. In Play mode the cues fire from AnimationVfxPlayer instead.", MessageType.Info);
				return;
			}

			if (!_target || !_clip)
			{
				EditorGUILayout.HelpBox("Pick a character in the open scene and one of its clips.", MessageType.Info);
				return;
			}

			DrawTransport();

			EditorGUILayout.Space();

			_scroll = EditorGUILayout.BeginScrollView(_scroll);
			DrawCues();
			EditorGUILayout.EndScrollView();
		}

		private void DrawClipPicker()
		{
			var controllerClips = _target && _target.runtimeAnimatorController
				? _target.runtimeAnimatorController.animationClips.Where(clip => clip).Distinct().OrderBy(clip => clip.name).ToArray()
				: new AnimationClip[0];

			using (new EditorGUILayout.HorizontalScope())
			{
				_clip = (AnimationClip)EditorGUILayout.ObjectField("Clip", _clip, typeof(AnimationClip), false);

				if (controllerClips.Length == 0) return;

				var index = System.Array.IndexOf(controllerClips, _clip);
				var picked = EditorGUILayout.Popup(index, controllerClips.Select(clip => clip.name).ToArray(), GUILayout.Width(160));
				if (picked != index && picked >= 0) _clip = controllerClips[picked];
			}
		}

		private void DrawTransport()
		{
			var totalFrames = Mathf.Max(1, Mathf.RoundToInt(_clip.length * FrameRate));

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button(_playing ? "Pause" : "Play", GUILayout.Width(60)))
				{
					_playing = !_playing;
					_lastTick = EditorApplication.timeSinceStartup;
					if (_playing && _time >= _clip.length) SetTime(0f);
				}

				if (GUILayout.Button("|<", GUILayout.Width(28))) SetTime(0f);
				if (GUILayout.Button("<", GUILayout.Width(24))) SetTime((CurrentFrame - 1) / FrameRate);
				if (GUILayout.Button(">", GUILayout.Width(24))) SetTime((CurrentFrame + 1) / FrameRate);

				_loop = GUILayout.Toggle(_loop, "Loop", GUILayout.Width(50));
			}

			EditorGUI.BeginChangeCheck();
			var frame = EditorGUILayout.IntSlider("Frame", CurrentFrame, 0, totalFrames);
			if (EditorGUI.EndChangeCheck()) SetTime(frame / FrameRate);

			DrawCueMarkers(totalFrames);

			EditorGUILayout.LabelField($"{_time:F3}s of {_clip.length:F3}s at {FrameRate:0.##} fps — {totalFrames} frames", EditorStyles.miniLabel);
		}

		// A strip under the slider with one tick per cue, so a clip's effects read at a glance.
		private void DrawCueMarkers(int totalFrames)
		{
			var rect = GUILayoutUtility.GetRect(0f, 8f, GUILayout.ExpandWidth(true));
			rect.xMin += EditorGUIUtility.labelWidth;
			rect.xMax -= 55f;

			EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.2f));

			var cues = _database ? _database.CuesFor(_clip) : null;
			if (cues != null)
			{
				for (var i = 0; i < cues.Count; i++)
				{
					var x = rect.x + rect.width * Mathf.Clamp01(cues[i].Frame / (float)totalFrames);
					var colour = i == _selectedCue ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.35f, 0.35f);
					EditorGUI.DrawRect(new Rect(x - 1f, rect.y, 3f, rect.height), colour);
				}
			}

			var head = rect.x + rect.width * Mathf.Clamp01(CurrentFrame / (float)totalFrames);
			EditorGUI.DrawRect(new Rect(head - 0.5f, rect.y - 2f, 1f, rect.height + 4f), Color.white);
		}

		private void DrawCues()
		{
			if (!_database)
			{
				EditorGUILayout.HelpBox("Assign a cue database to add effects to this clip.", MessageType.Info);
				return;
			}

			var serialized = new SerializedObject(_database);
			var clips = serialized.FindProperty("_clips");
			var entryIndex = FindEntry(clips);

			if (GUILayout.Button($"Add effect at frame {CurrentFrame}"))
			{
				if (entryIndex < 0)
				{
					clips.InsertArrayElementAtIndex(clips.arraySize);
					entryIndex = clips.arraySize - 1;

					var entry = clips.GetArrayElementAtIndex(entryIndex);
					entry.FindPropertyRelative(nameof(AnimationVfxCueDatabase.ClipCues.Clip)).objectReferenceValue = _clip;
					entry.FindPropertyRelative(nameof(AnimationVfxCueDatabase.ClipCues.Cues)).ClearArray();
				}

				var list = clips.GetArrayElementAtIndex(entryIndex).FindPropertyRelative(nameof(AnimationVfxCueDatabase.ClipCues.Cues));
				list.InsertArrayElementAtIndex(list.arraySize);

				var cue = list.GetArrayElementAtIndex(list.arraySize - 1);
				cue.FindPropertyRelative(nameof(AnimationVfxCue.Frame)).intValue = CurrentFrame;
				cue.FindPropertyRelative(nameof(AnimationVfxCue.Prefab)).objectReferenceValue = null;
				cue.FindPropertyRelative(nameof(AnimationVfxCue.Bone)).stringValue = string.Empty;
				cue.FindPropertyRelative(nameof(AnimationVfxCue.Position)).vector3Value = Vector3.zero;
				cue.FindPropertyRelative(nameof(AnimationVfxCue.Rotation)).vector3Value = Vector3.zero;
				cue.FindPropertyRelative(nameof(AnimationVfxCue.FollowBone)).boolValue = true;
				cue.FindPropertyRelative(nameof(AnimationVfxCue.Lifetime)).floatValue = 2f;

				_selectedCue = list.arraySize - 1;
			}

			if (entryIndex >= 0)
			{
				var list = clips.GetArrayElementAtIndex(entryIndex).FindPropertyRelative(nameof(AnimationVfxCueDatabase.ClipCues.Cues));

				for (var i = 0; i < list.arraySize; i++)
				{
					if (DrawCue(list, i)) break;
				}
			}

			if (serialized.ApplyModifiedProperties()) Refresh();
		}

		// True when the cue was deleted, so the caller stops walking a list that just got shorter.
		private bool DrawCue(SerializedProperty list, int index)
		{
			var cue = list.GetArrayElementAtIndex(index);
			var frame = cue.FindPropertyRelative(nameof(AnimationVfxCue.Frame));
			var prefab = cue.FindPropertyRelative(nameof(AnimationVfxCue.Prefab));
			var selected = index == _selectedCue;

			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					var title = $"{(prefab.objectReferenceValue ? prefab.objectReferenceValue.name : "(no prefab)")} @ {frame.intValue}";
					if (GUILayout.Toggle(selected, title, EditorStyles.foldout) != selected)
					{
						_selectedCue = selected ? -1 : index;
						SceneView.RepaintAll();
					}

					if (GUILayout.Button("Go", GUILayout.Width(36))) SetTime(frame.intValue / FrameRate);
					if (GUILayout.Button("Set here", GUILayout.Width(64))) frame.intValue = CurrentFrame;

					if (GUILayout.Button("X", GUILayout.Width(22)))
					{
						list.DeleteArrayElementAtIndex(index);
						if (_selectedCue == index) _selectedCue = -1;
						return true;
					}
				}

				if (!selected) return false;

				EditorGUILayout.PropertyField(frame);
				EditorGUILayout.PropertyField(prefab);
				DrawBonePopup(cue.FindPropertyRelative(nameof(AnimationVfxCue.Bone)));
				EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(AnimationVfxCue.Position)));
				EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(AnimationVfxCue.Rotation)));
				EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(AnimationVfxCue.FollowBone)));
				EditorGUILayout.PropertyField(cue.FindPropertyRelative(nameof(AnimationVfxCue.Lifetime)));
				EditorGUILayout.LabelField("Drag the handle in the Scene view to place it. W moves, E rotates.", EditorStyles.miniLabel);
			}

			return false;
		}

		private void DrawBonePopup(SerializedProperty bone)
		{
			var index = string.IsNullOrEmpty(bone.stringValue) ? 0 : System.Array.IndexOf(_boneNames, bone.stringValue);
			if (index < 0)
			{
				EditorGUILayout.PropertyField(bone);
				EditorGUILayout.HelpBox($"'{bone.stringValue}' is not a bone of {(_target ? _target.name : "the character")}.", MessageType.Warning);
				return;
			}

			var picked = EditorGUILayout.Popup("Bone", index, _boneNames);
			if (picked != index) bone.stringValue = picked == 0 ? string.Empty : _boneNames[picked];
		}

		private int FindEntry(SerializedProperty clips)
		{
			for (var i = 0; i < clips.arraySize; i++)
			{
				var clip = clips.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(AnimationVfxCueDatabase.ClipCues.Clip));
				if (clip.objectReferenceValue == _clip) return i;
			}

			return -1;
		}

		private AnimationVfxCue SelectedCue
		{
			get
			{
				var cues = _database && _clip ? _database.CuesFor(_clip) : null;
				return cues != null && _selectedCue >= 0 && _selectedCue < cues.Count ? cues[_selectedCue] : null;
			}
		}

		// The selected cue's offset, dragged in the scene where it can be seen against the pose it fires on.
		private void OnSceneGUI(SceneView view)
		{
			var cue = SelectedCue;
			if (cue == null || !CanPreview) return;

			var bone = ResolveBone(cue.Bone);
			if (!bone) return;

			var position = bone.TransformPoint(cue.Position);
			var rotation = bone.rotation * Quaternion.Euler(cue.Rotation);

			EditorGUI.BeginChangeCheck();

			if (Tools.current == Tool.Rotate) rotation = Handles.RotationHandle(rotation, position);
			else position = Handles.PositionHandle(position, Tools.pivotRotation == PivotRotation.Local ? rotation : Quaternion.identity);

			if (!EditorGUI.EndChangeCheck()) return;

			Undo.RecordObject(_database, "Place animation VFX");
			cue.Position = bone.InverseTransformPoint(position);
			cue.Rotation = (Quaternion.Inverse(bone.rotation) * rotation).eulerAngles;
			EditorUtility.SetDirty(_database);

			Refresh();
			Repaint();
		}

		private void Tick()
		{
			if (!_playing || !CanPreview) return;

			var now = EditorApplication.timeSinceStartup;
			var delta = (float)(now - _lastTick);
			_lastTick = now;

			var next = _time + delta;
			var wrapped = false;

			if (next > _clip.length)
			{
				if (_loop)
				{
					next = _clip.length > 0f ? next % _clip.length : 0f;
					wrapped = true;
				}
				else
				{
					next = _clip.length;
					_playing = false;
				}
			}

			_time = next;
			Sample();
			UpdateEffects(!wrapped);
			Repaint();
		}

		private void SetTime(float time)
		{
			_playing = false;
			_time = Mathf.Clamp(time, 0f, _clip ? _clip.length : 0f);
			Refresh();
			Repaint();
		}

		private void Refresh()
		{
			if (!CanPreview)
			{
				StopPreview();
				return;
			}

			Sample();
			UpdateEffects(false);
		}

		private void Sample()
		{
			if (!AnimationMode.InAnimationMode(_driver)) AnimationMode.StartAnimationMode(_driver);

			AnimationMode.BeginSampling();
			AnimationMode.SampleAnimationClip(_target.gameObject, _clip, _time);
			AnimationMode.EndSampling();

			SceneView.RepaintAll();
		}

		// Playing forward advances what is already alive; anything else — a scrub, a step back, a loop
		// wrapping — starts again from nothing and simulates each effect up to the time it would have reached,
		// because an effect cannot be run backwards.
		private void UpdateEffects(bool incremental)
		{
			if (!incremental || _simulatedTo < 0f) ClearEffects();

			var previous = _simulatedTo;
			var advance = _time - Mathf.Max(previous, 0f);

			for (var i = _spawned.Count - 1; i >= 0; i--)
			{
				var live = _spawned[i];
				if (!live.Instance || _time - live.BornAt >= live.Cue.Lifetime)
				{
					if (live.Instance) DestroyImmediate(live.Instance);
					_spawned.RemoveAt(i);
					continue;
				}

				Simulate(live.Instance, advance);
			}

			var cues = _database ? _database.CuesFor(_clip) : null;
			if (cues != null)
			{
				foreach (var cue in cues)
				{
					if (cue == null || !cue.Prefab) continue;

					var at = cue.TimeIn(_clip);
					var due = previous < 0f
						? at <= _time && _time - at < cue.Lifetime
						: previous < at && at <= _time;

					if (!due) continue;

					var instance = cue.Spawn(ResolveBone(cue.Bone));
					if (!instance) continue;

					Prepare(instance);
					Simulate(instance, _time - at);

					_spawned.Add(new Spawned { Cue = cue, Instance = instance, BornAt = at });
				}
			}

			_simulatedTo = _time;
		}

		// Hidden and never saved, so a preview cannot leave an effect behind in the scene; seeded the same
		// every time, so scrubbing back and forth shows the same particles rather than a new roll per frame.
		private static void Prepare(GameObject instance)
		{
			foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.HideAndDontSave;

			foreach (var effect in instance.GetComponentsInChildren<VisualEffect>(true))
			{
				effect.resetSeedOnPlay = false;
				effect.Reinit();
				effect.pause = true;
			}
		}

		private static void Simulate(GameObject instance, float seconds)
		{
			if (seconds <= 0f) return;

			var steps = (uint)Mathf.Max(1, Mathf.RoundToInt(seconds / SimulationStep));

			foreach (var effect in instance.GetComponentsInChildren<VisualEffect>(true)) effect.Simulate(seconds / steps, steps);
		}

		private void ClearEffects()
		{
			foreach (var live in _spawned)
			{
				if (live.Instance) DestroyImmediate(live.Instance);
			}

			_spawned.Clear();
			_simulatedTo = -1f;
		}

		private void StopPreview()
		{
			_playing = false;
			ClearEffects();

			if (_driver && AnimationMode.InAnimationMode(_driver)) AnimationMode.StopAnimationMode(_driver);

			SceneView.RepaintAll();
		}

		private Transform ResolveBone(string name)
		{
			if (!_target) return null;

			var bone = AnimationVfxCue.FindBone(_target.transform, name);
			return bone ? bone : _target.transform;
		}

		private string[] CollectBoneNames()
		{
			var names = new List<string> { "(root)" };
			if (!_target) return names.ToArray();

			foreach (var child in _target.GetComponentsInChildren<Transform>(true))
			{
				if (child != _target.transform && !names.Contains(child.name)) names.Add(child.name);
			}

			return names.ToArray();
		}
	}
}
