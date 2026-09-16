using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// A knock to the view, fired from a moment in an animation. A Cinemachine impulse rather than a key on
	// a bone: the camera rides the look rather than the rig, so the only honest way to move it on purpose
	// is to tell the brain — and an impulse is tunable after the fact, where a baked shake is not.
	//
	// Owner only, and that is the whole point of it. The cue is raised by whichever rig this machine draws,
	// so a body being watched across the table raises it on every client at once; without the gate everyone
	// would feel the hit that landed on somebody else. Whoever it happened to is the only one who feels it.
	public class PlayerCameraShake : NetworkBehaviour
	{
		[Serializable]
		private struct Shake
		{
			[Tooltip("Cue a clip raises at the frame the knock lands.")]
			public string Cue;

			[Tooltip("Multiplier on the source's own velocity. 1 is the shake as authored on the source; a hit and a swallow are the same signal at different strengths.")]
			[Min(0f)]
			public float Force;
		}

		[Tooltip("What the impulse is fired from. The signal's shape is authored on the source, so retuning how a shake feels never touches this file.")]
		[SerializeField] private CinemachineImpulseSource _source;

		[Tooltip("Cues that shake the view, each at its own strength. Anything not named here is ignored.")]
		[SerializeField] private Shake[] _shakes =
		{
			new() { Cue = "ImpactShake", Force = 3f },
			new() { Cue = "EatSwallow", Force = 0.5f }
		};

		private readonly List<PlayerAnimationEventRelay> _relays = new();

		private PlayerRigController _rig;
		private bool _bound;

		private void Awake()
		{
			if (!_source) _source = GetComponent<CinemachineImpulseSource>();
			if (!_rig) _rig = GetComponentInParent<PlayerRigController>(true);
		}

		// A spawned prefab runs Awake at instantiate and only learns who owns it when it spawns, so the
		// binding waits for that rather than reading IsOwner from a hook that runs too early.
		public override void OnNetworkSpawn()
		{
			if (IsOwner) Bind();
		}

		public override void OnNetworkDespawn() => Unbind();

		// OnNetworkSpawn is raised once, so a switch off and on again afterwards is this one's to cover.
		private void OnEnable()
		{
			if (IsSpawned && IsOwner) Bind();
		}

		private void OnDisable() => Unbind();

		private void Bind()
		{
			if (_bound || !_rig) return;

			_rig.GetComponentsInChildren(true, _relays);
			if (_relays.Count == 0) return;

			foreach (var relay in _relays) relay.OnAnimationCue += HandleCue;

			_bound = true;
		}

		private void Unbind()
		{
			if (!_bound) return;

			foreach (var relay in _relays)
			{
				if (relay) relay.OnAnimationCue -= HandleCue;
			}

			_relays.Clear();
			_bound = false;
		}

		private void HandleCue(string cue)
		{
			if (!_source || string.IsNullOrEmpty(cue)) return;

			foreach (var shake in _shakes)
			{
				if (shake.Cue != cue) continue;

				if (shake.Force > 0f) _source.GenerateImpulseWithForce(shake.Force);
				return;
			}
		}
	}
}
