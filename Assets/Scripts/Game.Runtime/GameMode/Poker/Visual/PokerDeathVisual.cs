using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.AnimationVfx;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The head goes on the frame it bursts: when the death clip's cue spawns the burst effect, its mesh is
	// switched off through PlayerVisual, the one writer of what a body draws, and switched back on when the
	// rate comes down. Only the mesh — the bones stay as they are, so the owner's camera and everything else
	// hung on the head keep their place.
	//
	// The burst is the one clock; the pacing's head delay plus a margin is only the backstop for a rig that
	// never fires it (not drawn, culled, or a clip with no cue).
	public class PokerDeathVisual : NetworkBehaviour
	{
		[Header("Head")]
		[Tooltip("Meshes switched off when the head goes — the head and the hat on it.")]
		[SerializeField] private List<PlayerSlot> _hiddenSlots = new() { PlayerSlot.Head, PlayerSlot.Hat };

		[Tooltip("The effect AnimationVfxCueDatabase spawns on the death clip as the head bursts. Its cue firing is when the head goes.")]
		[SerializeField] private GameObject _burstEffect;

		[Tooltip("Seconds past the pacing's head delay to wait for the burst before taking the head anyway.")]
		[Min(0f)]
		[SerializeField] private float _burstTimeout = 0.5f;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PlayerRigController _rig;
		[SerializeField] private PlayerVisual _visual;

		[Tooltip("The pose this waits on, which owns when the head goes. Empty resolves from this object.")]
		[SerializeField] private PokerDeathPoseController _pose;

		private readonly List<AnimationVfxPlayer> _vfxPlayers = new();

		private bool _hidden;
		private bool _awaitingBurst;
		private Tween _backstop;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_rig) _rig = GetComponentInParent<PlayerRigController>();
			if (!_visual) _visual = GetComponentInParent<PlayerVisual>();
			if (!_pose) _pose = GetComponent<PokerDeathPoseController>();
			if (!_data) return;

			if (_rig) _rig.GetComponentsInChildren(true, _vfxPlayers);
			foreach (var player in _vfxPlayers) player.OnCueFired += HandleCueFired;

			_data.OnHallucinationChanged += HandleHallucinationChanged;

			// Late join: somebody already under is already headless, with no death to replay.
			if (!_data.IsAlive) HideHead();
		}

		public override void OnNetworkDespawn()
		{
			if (_data) _data.OnHallucinationChanged -= HandleHallucinationChanged;

			foreach (var player in _vfxPlayers)
			{
				if (player) player.OnCueFired -= HandleCueFired;
			}

			_vfxPlayers.Clear();

			Restore();
		}

		private void HandleHallucinationChanged(int previous, int current)
		{
			var wasAlive = previous < PokerPlayerData.MaxHallucination;
			var isAlive = current < PokerPlayerData.MaxHallucination;

			if (wasAlive && !isAlive)
			{
				_awaitingBurst = true;

				var delay = (_pose ? _pose.HeadVanishDelay(previous, current) : 0f) + _burstTimeout;

				_backstop?.Kill();
				_backstop = DOVirtual.DelayedCall(delay, HideHead).SetLink(gameObject);
			}
			else if (!wasAlive && isAlive)
			{
				Restore();
			}
		}

		private void HandleCueFired(AnimationVfxCue cue)
		{
			if (_awaitingBurst && _burstEffect && cue.Prefab == _burstEffect) HideHead();
		}

		private void HideHead()
		{
			_awaitingBurst = false;
			_backstop?.Kill();
			_backstop = null;

			if (_hidden || !_visual) return;

			_hidden = true;

			foreach (var slot in _hiddenSlots) _visual.SetSlotHidden(this, slot, true);
		}

		private void Restore()
		{
			_awaitingBurst = false;
			_backstop?.Kill();
			_backstop = null;

			if (!_hidden) return;

			_hidden = false;

			if (!_visual) return;

			foreach (var slot in _hiddenSlots) _visual.SetSlotHidden(this, slot, false);
		}
	}
}
