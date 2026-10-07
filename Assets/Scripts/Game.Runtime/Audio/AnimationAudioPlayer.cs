using System.Collections.Generic;
using Game.Runtime.AnimationVfx;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Audio
{
	// Plays a clip's sounds when the events AnimationAudioCueDatabase installed say so, through AudioManager
	// so the backend is whichever the game runs. Sits on the animator's own object, one per animator; a rig
	// this machine is not drawing ignores its events, or every sound would play twice.
	[RequireComponent(typeof(Animator))]
	public class AnimationAudioPlayer : MonoBehaviour
	{
		public const string EventFunction = nameof(OnAnimationAudioCue);

		[Required]
		[SerializeField] private AnimationAudioCueDatabase _database;

		private readonly Dictionary<string, Transform> _bones = new();

		private Renderer[] _renderers;

		private void Awake()
		{
			_renderers = GetComponentsInChildren<Renderer>(true);

			if (_database) _database.InstallEvents();
		}

		private void OnAnimationAudioCue(AnimationEvent animationEvent)
		{
			if (!_database || animationEvent.objectReferenceParameter != _database) return;
			if (!IsDrawn() || !AudioManager.Instance) return;

			var cue = _database.CueAt(animationEvent.animatorClipInfo.clip, animationEvent.intParameter);
			if (cue == null || !cue.Event) return;

			var bone = ResolveBone(cue.Bone);

			if (cue.FollowBone) AudioManager.Instance.PlayOneShotAttached(cue.Event, bone);
			else AudioManager.Instance.PlayOneShot(cue.Event, bone.position);
		}

		private Transform ResolveBone(string name)
		{
			if (string.IsNullOrEmpty(name)) return transform;
			if (_bones.TryGetValue(name, out var bone) && bone) return bone;

			bone = AnimationCueDatabase.FindBone(transform, name);
			if (!bone)
			{
				Debug.LogWarning($"{nameof(AnimationAudioPlayer)}: no bone '{name}' under {transform.name}; the sound plays from the rig's root.", this);
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
