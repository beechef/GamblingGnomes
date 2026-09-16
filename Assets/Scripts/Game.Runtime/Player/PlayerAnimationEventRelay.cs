using System;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Turns an animation event authored on a clip into a C# event anything on the player can subscribe to.
	// Unity delivers an animation event only to components on the animator's own GameObject, which is why
	// this sits on each rig rather than on the feature child that cares — a listener over on Items or Cards
	// could never be called directly.
	//
	// A rig this machine is not drawing stays quiet: the owner renders the hand-only rig and everybody else
	// the full body, both play the same clips, and relaying from both would raise every cue twice. The same
	// gate AnimationVfxPlayer keeps, for the same reason.
	//
	// The cue is named by the event's string parameter rather than by a function per cue, so adding one is
	// typing a name on the clip and never a method here. The events live on the clips themselves — in an
	// FBX's import settings or in a project-owned .anim — so what a clip raises is visible on the clip.
	public class PlayerAnimationEventRelay : MonoBehaviour
	{
		public const string EventFunction = nameof(OnAnimationCueEvent);

		public event Action<string> OnAnimationCue;

		private Renderer[] _renderers;

		private void Awake() => _renderers = GetComponentsInChildren<Renderer>(true);

		// Named by the clip, so renaming it breaks every event authored against it — grep before touching.
		private void OnAnimationCueEvent(string cue)
		{
			if (string.IsNullOrEmpty(cue) || !IsDrawn()) return;

			OnAnimationCue?.Invoke(cue);
		}

		private bool IsDrawn()
		{
			if (_renderers == null) return false;

			foreach (var renderer in _renderers)
			{
				if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy) return true;
			}

			return false;
		}
	}
}
