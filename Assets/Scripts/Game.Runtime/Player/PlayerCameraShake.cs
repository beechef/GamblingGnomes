using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// A shake of the view, started from a moment in an animation. A Cinemachine impulse rather than a key on
	// a bone: the camera rides the look rather than the rig, so the only honest way to move it on purpose is
	// to tell the brain. What each shake feels like is a preset — a 6D noise run through an envelope — so a
	// hit and a chuckle differ in data, not in code.
	//
	// Owner only, and that is the whole point of it. The cue is raised by whichever rig this machine draws,
	// so a body being watched across the table raises it on every client at once; without the gate everyone
	// would feel the hit that landed on somebody else.
	public class PlayerCameraShake : NetworkBehaviour
	{
		[Serializable]
		private struct Shake
		{
			[Tooltip("Cue a clip raises where the shake starts.")]
			public string Cue;

			public PlayerCameraShakePreset Preset;
		}

		[Tooltip("Cues that shake the view, each with its own preset. Anything not named here is ignored.")]
		[SerializeField] private Shake[] _shakes = Array.Empty<Shake>();

		private class Playing
		{
			public readonly CinemachineImpulseDefinition Definition = new();
			public CinemachineImpulseManager.ImpulseEvent Event;
			public ISignalSource6D Signal;
		}

		private readonly List<PlayerAnimationEventRelay> _relays = new();

		// One definition per preset: a running impulse reads its definition every frame, so two presets
		// sharing one would each play with whichever was set last.
		private readonly Dictionary<PlayerCameraShakePreset, Playing> _playing = new();

		private PlayerRigController _rig;
		private bool _bound;

		private void Awake()
		{
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

			foreach (var playing in _playing.Values) Stop(playing);
		}

		private void HandleCue(string cue)
		{
			if (string.IsNullOrEmpty(cue)) return;

			foreach (var shake in _shakes)
			{
				if (shake.Cue != cue) continue;

				Play(shake.Preset);
				return;
			}
		}

		public void Play(PlayerCameraShakePreset preset)
		{
			if (!preset || !preset.IsPlayable) return;

			if (!_playing.TryGetValue(preset, out var playing))
			{
				playing = new Playing();
				_playing.Add(preset, playing);
			}

			// A cue raised again while its shake is still running — a looping clip — takes over from the one
			// in progress rather than stacking a second copy on top: the old one decays out as the new one
			// comes in, so a held pose shakes evenly instead of spiking once a loop.
			Stop(playing);

			preset.ApplyTo(playing.Definition);

			var position = transform.position;
			playing.Event = playing.Definition.CreateAndReturnEvent(position, Vector3.down * preset.Strength);
			playing.Signal = playing.Event?.SignalSource;
		}

		// Events are pooled by the manager, so one is only still ours if it carries the signal we gave it.
		private static void Stop(Playing playing)
		{
			var impulse = playing.Event;
			if (impulse != null && impulse.SignalSource == playing.Signal && !impulse.Expired)
			{
				impulse.Cancel(CinemachineImpulseManager.Instance.CurrentTime, false);
			}

			playing.Event = null;
			playing.Signal = null;
		}
	}
}
