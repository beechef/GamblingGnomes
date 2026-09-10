using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.AnimationVfx
{
	// Spawns a clip's effects when its animation events say so. The events are not authored on the clip:
	// AnimationVfxCueDatabase installs them from its own rows when a player wakes, so the timing is the
	// animator's own — loops, crossfades and speed changes included — and the model's files are never
	// touched. Unity delivers an animation event to components on the animator's own object, which is why
	// this sits there, one per animator.
	//
	// A rig this machine is not drawing ignores its events: the owner renders the hand-only rig and
	// everybody else the full body, both play the same clips, and firing from both would put every effect
	// in twice.
	[RequireComponent(typeof(Animator))]
	public class AnimationVfxPlayer : MonoBehaviour
	{
		public const string EventFunction = nameof(OnAnimationVfxCue);

		[Required]
		[SerializeField] private AnimationVfxCueDatabase _database;

		private readonly Dictionary<string, Transform> _bones = new();

		private Renderer[] _renderers;

		private void Awake()
		{
			_renderers = GetComponentsInChildren<Renderer>(true);

			if (_database) _database.InstallEvents();
		}

		// Raised by the animator from the event AnimationVfxCueDatabase put on the clip. The event carries
		// the database and the cue's index, so an event some other database installed is not mistaken for
		// one of ours.
		private void OnAnimationVfxCue(AnimationEvent animationEvent)
		{
			if (!_database || animationEvent.objectReferenceParameter != _database) return;
			if (!IsDrawn()) return;

			var cue = _database.CueAt(animationEvent.animatorClipInfo.clip, animationEvent.intParameter);
			if (cue == null) return;

			var instance = cue.Spawn(ResolveBone(cue.Bone));
			if (instance) Destroy(instance, cue.Lifetime);
		}

		private Transform ResolveBone(string name)
		{
			if (string.IsNullOrEmpty(name)) return transform;
			if (_bones.TryGetValue(name, out var bone) && bone) return bone;

			bone = AnimationVfxCue.FindBone(transform, name);
			if (!bone)
			{
				Debug.LogWarning($"{nameof(AnimationVfxPlayer)}: no bone '{name}' under {transform.name}; the effect is spawned at the rig's root.", this);
				bone = transform;
			}

			_bones[name] = bone;
			return bone;
		}

		private bool IsDrawn()
		{
			foreach (var renderer in _renderers)
			{
				if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy) return true;
			}

			return false;
		}
	}
}
