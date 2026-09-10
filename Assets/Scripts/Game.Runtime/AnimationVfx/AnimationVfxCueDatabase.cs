using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.AnimationVfx
{
	// Which effects a clip fires, and when. Kept apart from the model on purpose: the FBX, its import
	// settings and the animator controller are never touched, so re-exporting the animation cannot take an
	// effect with it and an effect can be retuned without reimporting anything. Edited through
	// Tools > Animation VFX Preview, where the frame can be seen.
	[CreateAssetMenu(fileName = "AnimationVfxCueDatabase", menuName = "Game/Animation/VFX Cue Database")]
	public class AnimationVfxCueDatabase : ScriptableObject
	{
		[Serializable]
		public class ClipCues
		{
			[Required]
			public AnimationClip Clip;

			public List<AnimationVfxCue> Cues = new();
		}

		[SerializeField] private List<ClipCues> _clips = new();

		private readonly List<AnimationEvent> _eventBuffer = new();

		private bool _uninstallQueued;

		public IReadOnlyList<ClipCues> Clips => _clips;

		public List<AnimationVfxCue> CuesFor(AnimationClip clip)
		{
			foreach (var entry in _clips)
			{
				if (entry.Clip == clip) return entry.Cues;
			}

			return null;
		}

		public AnimationVfxCue CueAt(AnimationClip clip, int index)
		{
			var cues = CuesFor(clip);
			return cues != null && index >= 0 && index < cues.Count ? cues[index] : null;
		}

		// Puts one animation event per cue on its clip, at the cue's frame. Only the clip in memory changes —
		// an FBX's clips are imported and never written back — and every event this database put there
		// before is taken off first, so installing again (another player waking, a new Play session with
		// domain reload off, a cue moved in between) never stacks a second copy.
		//
		// And taken off again when the session ends. A clip keeps what it was given in memory after Play mode
		// stops, and an .anim clip edited in the Animation window afterwards would save those events into the
		// file — which is exactly the coupling this database exists to avoid.
		public void InstallEvents()
		{
			foreach (var entry in _clips)
			{
				if (!entry.Clip) continue;

				CollectForeignEvents(entry.Clip);

				for (var i = 0; i < entry.Cues.Count; i++)
				{
					var cue = entry.Cues[i];
					if (cue == null || !cue.Prefab) continue;

					_eventBuffer.Add(new AnimationEvent
					{
						functionName = AnimationVfxPlayer.EventFunction,
						time = Mathf.Clamp(cue.TimeIn(entry.Clip), 0f, entry.Clip.length),
						intParameter = i,
						objectReferenceParameter = this,

						// Anything else playing the same clip without a player — a test scene's gnome — must not
						// log a missing receiver every time the frame goes by.
						messageOptions = SendMessageOptions.DontRequireReceiver
					});
				}

				WriteEvents(entry.Clip);
			}

			_eventBuffer.Clear();

			if (_uninstallQueued) return;

			// Raised on quitting a build and, in the editor, on leaving Play mode.
			Application.quitting += UninstallEvents;
			_uninstallQueued = true;
		}

		public void UninstallEvents()
		{
			Application.quitting -= UninstallEvents;
			_uninstallQueued = false;

			foreach (var entry in _clips)
			{
				if (!entry.Clip) continue;

				CollectForeignEvents(entry.Clip);
				WriteEvents(entry.Clip);
			}

			_eventBuffer.Clear();
		}

		private void CollectForeignEvents(AnimationClip clip)
		{
			_eventBuffer.Clear();

			foreach (var existing in clip.events)
			{
				if (existing.functionName != AnimationVfxPlayer.EventFunction) _eventBuffer.Add(existing);
			}
		}

		// The runtime setter is refused outside Play mode ("use AnimationUtility"), and quitting can land on
		// either side of that line.
		private void WriteEvents(AnimationClip clip)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				UnityEditor.AnimationUtility.SetAnimationEvents(clip, _eventBuffer.ToArray());
				return;
			}
#endif
			clip.events = _eventBuffer.ToArray();
		}
	}
}
