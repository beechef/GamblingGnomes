using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The pointer that says whose turn it is out on the table itself, so a player reading the board
	// never has to look up at the HUD to find out who everyone is waiting on. It stays put in the
	// middle and swings to face the seat on the clock, the way a compass needle turns to north.
	// Presentation only: it follows replicated turn state and is never the thing that decides it.
	public class PokerTurnArrowVisual : PokerVisual
	{
		[Header("References")]
		[Tooltip("The arrow body. Hidden whenever no one is on the clock — leave the root itself enabled.")]
		[SerializeField] private GameObject _arrow;

		[Header("Placement")]
		[Tooltip("Height above this root the arrow turns at. The root sits at the middle of the table.")]
		[SerializeField] private float _height = 0.62f;

		[Header("Turning")]
		[SerializeField] private float _turnDuration = 0.4f;
		[SerializeField] private Ease _turnEase = Ease.OutBack;

		private Tween _turnTween;
		private bool _aimed;

		private void Awake()
		{
			// The arrow body is the child this hangs off, so an unset reference resolves rather than
			// leaving the pointer silently missing.
			if (!_arrow && transform.childCount > 0) _arrow = transform.GetChild(0).gameObject;

			Hide();
		}

		private void OnDestroy() => KillTween();

		protected override void OnBind()
		{
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;

			// The table announces itself before its seats have all registered and before a seat index has
			// replicated, so the first look can come up empty. Seating changes bring it back rather than
			// leaving the arrow hidden until the turn moves on.
			GameMode.OnSeatedPlayersChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			GameMode.OnSeatedPlayersChanged -= Refresh;

			if (Data) Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			KillTween();
			Hide();
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();

		private void Refresh()
		{
			var seat = ResolveTurnSeat();

			if (_arrow && _arrow.activeSelf != seat) _arrow.SetActive(seat);

			if (!seat)
			{
				// A hand that opens on a seat should point there straight away rather than sweeping
				// round from wherever the last one ended.
				_aimed = false;
				return;
			}

			AimAt(seat);
		}

		private PokerSeat ResolveTurnSeat()
		{
			if (!Data || !Data.HasTurn) return null;

			var player = GameMode.FindSeatedPlayer(Data.CurrentTurnClientId.Value);
			if (!player || !player.Data) return null;

			var seatIndex = player.Data.SeatIndex.Value;

			foreach (var seat in GameMode.Seats)
			{
				if (seat && seat.SeatIndex == seatIndex) return seat;
			}

			return null;
		}

		private void AimAt(PokerSeat seat)
		{
			var anchor = seat.CardAnchor;
			if (!_arrow || !anchor) return;

			var arrowTransform = _arrow.transform;
			arrowTransform.position = transform.position + Vector3.up * _height;

			// Flattened before it becomes a rotation: a seat sits lower than the arrow, and following
			// that drop would tip the pointer down into the table.
			var toSeat = anchor.position - arrowTransform.position;
			toSeat.y = 0f;
			if (toSeat.sqrMagnitude < 0.0001f) return;

			var target = Quaternion.LookRotation(toSeat.normalized, Vector3.up);

			KillTween();

			// The first aim of a hand snaps; after that it swings, so the handover reads as the turn
			// travelling round the table.
			if (!_aimed || _turnDuration <= 0f)
			{
				arrowTransform.rotation = target;
				_aimed = true;
				return;
			}

			if (Quaternion.Angle(arrowTransform.rotation, target) < 0.01f) return;

			// Slerped through a virtual tween rather than DORotate: the ease is allowed to overshoot,
			// and a quaternion tween would resolve that overshoot as a wobble off the turning plane
			// instead of the arrow swinging past the seat and settling back onto it.
			var from = arrowTransform.rotation;

			_turnTween = DOVirtual.Float(0f, 1f, _turnDuration, t =>
				{
					if (arrowTransform) arrowTransform.rotation = Quaternion.SlerpUnclamped(from, target, t);
				})
				.SetEase(_turnEase)
				.OnComplete(() =>
				{
					if (arrowTransform) arrowTransform.rotation = target;
				});
		}

		private void KillTween()
		{
			_turnTween?.Kill();
			_turnTween = null;
		}

		private void Hide()
		{
			_aimed = false;

			if (_arrow) _arrow.SetActive(false);
		}
	}
}
