using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// Carrying a staked cap through the bet gesture: hidden until the hand reaches the table, held in the
	// fist, then put down on the spot the table picked. It lives on the player because everything it knows
	// is the player's — which rig is drawn, where a fist closes, where the arm is aimed, and which frame of
	// the clip the hand arrives on. The table owns the ledger and the spot, and hands a cap over.
	//
	// When the hand arrives is the clip's own business, raised as an animation event rather than counted in
	// seconds here. A second bet clip therefore needs no number in this file: it carries its own two cues.
	public class PokerItemCarryController : MonoBehaviour
	{
		// Typed in the Animation window on the other side, which is the cost of authoring cues on the clip
		// itself. Renaming one means re-authoring every clip that raises it.
		public const string GrabCue = "BetGrab";
		public const string ReleaseCue = "BetRelease";

		// Where a held cap sits, as a transform authored under the wrist on both rigs. A bone is the wrong
		// answer: Cup_R is the base of the hand, so a cap sitting on it reads as held at the joint rather
		// than in the fingers, and every bone on this skeleton carries axes nobody picked.
		private const string CapHoldName = "CapHold";

		// The palm, as a last resort for a rig with no hold point yet — still far better than the wrist
		// origin, which is inside the forearm.
		private const string PalmBoneName = "Cup_R";

		[SerializeField] private PlayerRigController _rig;
		[SerializeField] private PlayerHandIkController _handIk;

		[Tooltip("Seconds the cap takes from the hand to its spot on the table once the gesture lets go.")]
		[Min(0f)]
		[SerializeField] private float _placeDuration = 0.25f;

		[SerializeField] private Ease _placeEase = Ease.OutQuad;

		[Tooltip("How long a cap waits for a cue that never comes before it is simply put on the table. A clip missing its events must not leave a cap invisible for the rest of the hand.")]
		[Min(0f)]
		[SerializeField] private float _cueTimeout = 3f;

		private readonly List<PlayerAnimationEventRelay> _relays = new();
		private readonly List<Carried> _carrying = new();

		private class Carried
		{
			public GameObject Cap;
			public Transform ReturnParent;
			public Vector3 Resting;
			public Vector3 WorldScale;
			public Vector3 RestingScale;
			public bool Held;
			public Tween Timeout;
		}

		private void Awake()
		{
			if (!_rig) _rig = GetComponentInParent<PlayerRigController>(true);
			if (!_handIk) _handIk = _rig ? _rig.GetComponentInChildren<PlayerHandIkController>(true) : null;
		}

		// Both rigs, each gating itself on being drawn, rather than the one RenderedRig answers with today:
		// ownership is not settled when a spawned prefab wakes, and a relay picked once here would be the
		// wrong rig for the rest of the session on whichever machine lost that race.
		private void OnEnable()
		{
			if (_rig) _rig.GetComponentsInChildren(true, _relays);

			foreach (var relay in _relays) relay.OnAnimationCue += HandleCue;
		}

		private void OnDisable()
		{
			foreach (var relay in _relays) relay.OnAnimationCue -= HandleCue;

			_relays.Clear();

			for (var i = _carrying.Count - 1; i >= 0; i--) Land(_carrying[i]);

			_carrying.Clear();
		}

		// Taken from the table: hidden where it lies until the gesture's hand comes down for it.
		public void Carry(GameObject cap, Transform returnParent, Vector3 resting, Transform placementAnchor)
		{
			if (!cap) return;

			if (_handIk && placementAnchor) _handIk.Aim(placementAnchor);

			var carried = new Carried
			{
				Cap = cap,
				ReturnParent = returnParent,
				Resting = resting,
				WorldScale = cap.transform.lossyScale,
				RestingScale = cap.transform.localScale
			};

			carried.Timeout = DOVirtual.DelayedCall(_cueTimeout, () => Release(cap), false);

			cap.SetActive(false);
			_carrying.Add(carried);
		}

		// A cap on its way is one the table must leave alone, and one a clear has to be able to take back.
		public bool IsCarrying(GameObject cap)
		{
			foreach (var carried in _carrying)
			{
				if (carried.Cap == cap) return true;
			}

			return false;
		}

		public void Release(GameObject cap)
		{
			for (var i = _carrying.Count - 1; i >= 0; i--)
			{
				if (_carrying[i].Cap != cap) continue;

				Land(_carrying[i]);
				_carrying.RemoveAt(i);
				return;
			}
		}

		private void HandleCue(string cue)
		{
			// A cap destroyed under us — the pot cleared, or one eaten mid-gesture — is dropped here rather
			// than left for its timeout, so nothing is ever put back onto a corpse.
			for (var i = _carrying.Count - 1; i >= 0; i--)
			{
				if (_carrying[i].Cap) continue;

				var timeout = _carrying[i].Timeout;
				_carrying[i].Timeout = null;
				timeout?.Kill();
				_carrying.RemoveAt(i);
			}

			switch (cue)
			{
				case GrabCue:
					foreach (var carried in _carrying) Grab(carried);
					break;

				case ReleaseCue:
					for (var i = _carrying.Count - 1; i >= 0; i--)
					{
						Place(_carrying[i]);
						_carrying.RemoveAt(i);
					}

					break;
			}
		}

		// Held where the rig says a hand holds something. The cap hangs on the hold point at zero: CapHold
		// is the pose, so retuning the grip is a drag in the scene view and never a number in this file.
		private void Grab(Carried carried)
		{
			if (carried.Held || !carried.Cap) return;

			var holder = HoldPoint();
			if (!holder) return;

			carried.Held = true;
			carried.Cap.SetActive(true);
			carried.Cap.transform.SetParent(holder, false);
			carried.Cap.transform.localPosition = Vector3.zero;
			carried.Cap.transform.localRotation = Quaternion.identity;

			// Kept at the size it has on the table, whatever scale the chain down to the hold point carries.
			var scale = holder.lossyScale;
			carried.Cap.transform.localScale = new Vector3(carried.WorldScale.x / scale.x, carried.WorldScale.y / scale.y, carried.WorldScale.z / scale.z);
		}

		private void Place(Carried carried)
		{
			var timeout = carried.Timeout;
			carried.Timeout = null;
			timeout?.Kill();

			if (!carried.Cap) return;

			carried.Cap.SetActive(true);
			carried.Cap.transform.SetParent(carried.ReturnParent, true);

			if (_placeDuration <= 0f)
			{
				Land(carried);
				return;
			}

			carried.Cap.transform.DOLocalMove(carried.Resting, _placeDuration).SetEase(_placeEase);
			carried.Cap.transform.DOLocalRotateQuaternion(Quaternion.identity, _placeDuration);
			carried.Cap.transform.DOScale(carried.RestingScale, _placeDuration);
		}

		// Wherever it had got to, it is back on the table's books at its spot.
		private void Land(Carried carried)
		{
			var timeout = carried.Timeout;
			carried.Timeout = null;
			timeout?.Kill();

			if (!carried.Cap) return;

			carried.Cap.transform.DOKill();
			carried.Cap.SetActive(true);
			carried.Cap.transform.SetParent(carried.ReturnParent, false);
			carried.Cap.transform.localPosition = carried.Resting;
			carried.Cap.transform.localRotation = Quaternion.identity;
			carried.Cap.transform.localScale = carried.RestingScale;
		}

		private Transform HoldPoint()
		{
			var hand = _rig ? _rig.GetBone(PlayerBone.HandRight) : null;
			if (!hand) return null;

			var hold = hand.Find(CapHoldName);
			if (hold) return hold;

			var palm = hand.Find(PalmBoneName);

			return palm ? palm : hand;
		}
	}
}
