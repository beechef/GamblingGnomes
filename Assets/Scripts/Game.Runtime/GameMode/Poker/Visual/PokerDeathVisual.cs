using DG.Tweening;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The head goes when the death animation's shaking ends, in a puff. Collapsed at its bone through
	// PlayerBoneScaleController — the same way a lost finger goes — so the hat, the glasses and anything
	// else hung on the head go with it, and it comes back by dropping one modifier when the rate does.
	//
	// The full body rig only: that is what the rest of the table sees, and the owner's own view hangs off
	// the hand-only rig's head, which a zero scale would take with it.
	public class PokerDeathVisual : NetworkBehaviour
	{
		[Header("Head")]
		[Tooltip("Seconds after going under before the head is gone — the frame the death animation's shaking ends. Counted from the change that starts the pose.")]
		[Min(0f)]
		[SerializeField] private float _headVanishDelay = 2.5f;

		[Tooltip("Spawned where the head was as it goes. Empty until the art lands.")]
		[SerializeField] private GameObject _vanishEffect;

		[Min(0f)]
		[SerializeField] private float _vanishEffectLifetime = 3f;

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PlayerRigController _rig;
		[SerializeField] private PlayerBoneScaleController _boneScale;

		private PlayerBoneScaleModifier _collapsed;
		private Tween _pending;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_rig) _rig = GetComponentInParent<PlayerRigController>();
			if (!_boneScale && _rig) _boneScale = _rig.GetComponentInChildren<PlayerBoneScaleController>(true);
			if (!_data) return;

			_data.OnHallucinationChanged += HandleHallucinationChanged;

			// Late join: somebody already under is already headless, with no death to replay.
			if (!_data.IsAlive) Collapse(false);
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
				_pending = DOVirtual.DelayedCall(_headVanishDelay, () => Collapse(true)).SetLink(gameObject);
			}
			else if (!wasAlive && isAlive)
			{
				Restore();
			}
		}

		private void Collapse(bool withEffect)
		{
			_pending = null;
			if (_collapsed != null || !_boneScale || !_rig || !_rig.FullBodyRig) return;

			var head = _rig.FullBodyRig.Get(PlayerBone.Head);
			if (!head) return;

			if (withEffect && _vanishEffect)
			{
				var effect = Instantiate(_vanishEffect, head.position, Quaternion.identity);
				Destroy(effect, _vanishEffectLifetime);
			}

			_collapsed = _boneScale.Add(head, Vector3.zero);
		}

		private void Restore()
		{
			_pending?.Kill();
			_pending = null;

			_collapsed?.Remove();
			_collapsed = null;
		}
	}
}
