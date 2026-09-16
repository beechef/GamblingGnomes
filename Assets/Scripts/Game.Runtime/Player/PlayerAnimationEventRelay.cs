using System;
using Game.Runtime.AnimationVfx;
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
	// typing a name in the Animation window and never a method here. A clip inside an FBX cannot keep an
	// event through a re-export, so its markers come from _markers instead and arrive indistinguishably.
	public class PlayerAnimationEventRelay : MonoBehaviour
	{
		public const string EventFunction = nameof(OnAnimationCueEvent);

		[Tooltip("Markers installed onto clips this rig plays. For clips inside an FBX, where an event authored in the Animation window would be lost on the next export. A clip the project owns can carry its own instead.")]
		[SerializeField] private AnimationMarkerDatabase _markers;

		public event Action<string> OnAnimationCue;

		private Renderer[] _renderers;

		private void Awake()
		{
			_renderers = GetComponentsInChildren<Renderer>(true);

			// Idempotent: every event this database wrote before is stripped first, so both rigs installing
			// the same rows never stacks a second copy.
			if (_markers) _markers.InstallEvents();
		}

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
