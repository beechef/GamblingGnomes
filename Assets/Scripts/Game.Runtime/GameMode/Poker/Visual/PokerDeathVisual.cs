using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The head goes when the death animation's shaking ends, in a puff: its mesh is switched off through
	// PlayerVisual, the one writer of what a body draws, and switched back on when the rate comes down.
	// Only the mesh — the bones stay as they are, so the owner's camera and everything else hung on the
	// head keep their place.
	public class PokerDeathVisual : NetworkBehaviour
	{
		[Header("Head")]
		[Tooltip("Seconds after the death pose starts before the head is gone — the frame the death animation's shaking ends. The pose's own delay is added on top.")]
		[Min(0f)]
		[SerializeField] private float _headVanishDelay = 2.5f;

		[Tooltip("Meshes switched off when the head goes — the head and the hat on it.")]
		[SerializeField] private List<PlayerSlot> _hiddenSlots = new() { PlayerSlot.Head, PlayerSlot.Hat };

		[Tooltip("Spawned where the head was as it goes. Empty until the art lands.")]
		[SerializeField] private GameObject _vanishEffect;

		[Min(0f)]
		[SerializeField] private float _vanishEffectLifetime = 3f;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PlayerRigController _rig;
		[SerializeField] private PlayerVisual _visual;

		[Tooltip("The pose this waits on: the head goes Head Vanish Delay after the pose starts, and the pose starts its own beat after going under. Empty resolves from this object.")]
		[SerializeField] private PokerDeathPoseController _pose;

		private bool _hidden;
		private Tween _pending;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_rig) _rig = GetComponentInParent<PlayerRigController>();
			if (!_visual) _visual = GetComponentInParent<PlayerVisual>();
			if (!_pose) _pose = GetComponent<PokerDeathPoseController>();
			if (!_data) return;

			_data.OnHallucinationChanged += HandleHallucinationChanged;

			// Late join: somebody already under is already headless, with no death to replay.
			if (!_data.IsAlive) HideHead(false);
		}

		public override void OnNetworkDespawn()
		{
			if (_data) _data.OnHallucinationChanged -= HandleHallucinationChanged;

			Restore();
		}

		private void HandleHallucinationChanged(int previous, int current)
		{
			var wasAlive = previous < PokerPlayerData.MaxHallucination;
			var isAlive = current < PokerPlayerData.MaxHallucination;

			if (wasAlive && !isAlive)
			{
				_pending?.Kill();
				_pending = DOVirtual.DelayedCall((_pose ? _pose.PoseDelay : 0f) + _headVanishDelay, () => HideHead(true)).SetLink(gameObject);
			}
			else if (!wasAlive && isAlive)
			{
				Restore();
			}
		}

		private void HideHead(bool withEffect)
		{
			_pending = null;
			if (_hidden || !_visual) return;

			_hidden = true;

			var head = _rig && _rig.FullBodyRig ? _rig.FullBodyRig.Get(PlayerBone.Head) : null;

			if (withEffect && _vanishEffect && head)
			{
				var effect = Instantiate(_vanishEffect, head.position, Quaternion.identity);
				Destroy(effect, _vanishEffectLifetime);
			}

			foreach (var slot in _hiddenSlots) _visual.SetSlotHidden(slot, true);
		}

		private void Restore()
		{
			_pending?.Kill();
			_pending = null;

			if (!_hidden) return;

			_hidden = false;

			if (!_visual) return;

			foreach (var slot in _hiddenSlots) _visual.SetSlotHidden(slot, false);
		}
	}
}
