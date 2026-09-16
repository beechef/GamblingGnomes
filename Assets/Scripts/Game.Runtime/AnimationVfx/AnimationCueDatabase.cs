using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.AnimationVfx
{
	// Puts a clip's cues on it as real animation events, and takes them off again. Kept apart from the
	// model on purpose: the FBX, its import settings and the animator controller are never touched, so
	// re-exporting the animation cannot take a cue with it and a cue can be retuned without reimporting
	// anything. That is the whole reason this exists rather than authoring events in the Animation window
	// — which is fine for a clip the project owns (`Animation_Bet.anim`) and lossy for one inside an FBX.
	//
	// Only the clip in memory changes; an FBX's clips are imported and never written back. Every event
	// this database put there before is taken off first, so installing again — another player waking, a
	// new Play session with domain reload off, a cue moved in between — never stacks a second copy.
	//
	// A subclass says what its events are called and builds them; everything about *when* they are written
	// and cleaned up lives here, so a second kind of cue cannot get that half subtly wrong.
	public abstract class AnimationCueDatabase : ScriptableObject
	{
		private readonly List<AnimationEvent> _eventBuffer = new();

		private bool _uninstallQueued;

		// What the events this database owns are called. Used to tell ours from anybody else's when
		// stripping, so two databases can write to one clip without erasing each other.
		public abstract string EventFunction { get; }

		protected abstract IEnumerable<AnimationClip> TargetClips { get; }

		// Appends this database's events for one clip. The buffer already holds every foreign event on it.
		protected abstract void BuildEvents(AnimationClip clip, List<AnimationEvent> into);

		public void InstallEvents()
		{
			foreach (var clip in TargetClips)
			{
				if (!clip) continue;

				CollectForeignEvents(clip);
				BuildEvents(clip, _eventBuffer);
				WriteEvents(clip);
			}

			_eventBuffer.Clear();

			if (_uninstallQueued) return;

			// Raised on quitting a build and, in the editor, on leaving Play mode. A clip keeps what it was
			// given in memory after Play mode stops, and an .anim edited in the Animation window afterwards
			// would save those events into the file — the coupling this exists to avoid.
			Application.quitting += UninstallEvents;
			_uninstallQueued = true;
		}

		public void UninstallEvents()
		{
			Application.quitting -= UninstallEvents;
			_uninstallQueued = false;

			foreach (var clip in TargetClips)
			{
				if (!clip) continue;

				CollectForeignEvents(clip);
				WriteEvents(clip);
			}

			_eventBuffer.Clear();
		}

		// Anything else playing the same clip without a receiver must not log a missing one every time the
		// frame goes by, so every event a database writes says so.
		protected AnimationEvent CueEvent(AnimationClip clip, float time) => new()
		{
			functionName = EventFunction,
			time = Mathf.Clamp(time, 0f, clip.length),
			messageOptions = SendMessageOptions.DontRequireReceiver
		};

		private void CollectForeignEvents(AnimationClip clip)
		{
			_eventBuffer.Clear();

			foreach (var existing in clip.events)
			{
				if (existing.functionName != EventFunction) _eventBuffer.Add(existing);
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
