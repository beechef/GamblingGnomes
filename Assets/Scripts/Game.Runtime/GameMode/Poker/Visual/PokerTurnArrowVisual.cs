using DG.Tweening;
using Unity.Collections;
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
		[Tooltip("The arrow body. Hidden whenever no one is on the clock — leave the root itself enabled. Where it sits is its own transform in the prefab, dragged onto the table top in the scene view; this only turns it.")]
		[SerializeField] private GameObject _arrow;

		[Header("Turning")]
		[SerializeField] private float _turnDuration = 0.4f;
		[SerializeField] private Ease _turnEase = Ease.OutBack;

		[Header("Signal")]
		[Tooltip("The arrow's light: its alpha is flashed once the arrow lands on a seat, then blinked like an indicator for as long as the turn lasts. Resolved from the arrow body when empty.")]
		[SerializeField] private SpriteRenderer _light;

		[Tooltip("Scale the arrow pops up to as the turn passes to somebody else, settling back while it turns.")]
		[Min(1f)]
		[SerializeField] private float _popScale = 1.3f;

		[Min(0f)]
		[SerializeField] private float _popDuration = 0.3f;

		[Range(0f, 1f)]
		[SerializeField] private float _flashAlpha = 1f;

		[Tooltip("Seconds for the one flash, up and back down.")]
		[Min(0f)]
		[SerializeField] private float _flashDuration = 0.25f;

		[Range(0f, 1f)]
		[SerializeField] private float _blinkAlpha = 0.7f;

		[Tooltip("Seconds from dim to lit; the blink goes back the same way.")]
		[Min(0.01f)]
		[SerializeField] private float _blinkHalfPeriod = 0.4f;

		[SerializeField] private Ease _blinkEase = Ease.InOutSine;

		private Tween _turnTween;
		private Sequence _signalTween;
		private Tween _blinkTween;
		private bool _aimed;
		private ulong _signalledClientId = PokerGameData.NoTurn;
		private Vector3 _restScale;
		private float _restAlpha;

		private void Awake()
		{
			// The arrow body is the child this hangs off, so an unset reference resolves rather than
			// leaving the pointer silently missing.
			if (!_arrow && transform.childCount > 0) _arrow = transform.GetChild(0).gameObject;
			if (!_light && _arrow) _light = _arrow.GetComponentInChildren<SpriteRenderer>(true);

			if (_arrow) _restScale = _arrow.transform.localScale;
			if (_light) _restAlpha = _light.color.a;

			Hide();
		}

		private void OnDestroy()
		{
			KillTween();
			KillSignal();
		}

		protected override void OnBind()
		{
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;

			// The table announces itself before its seats have all registered and before a seat index has
			// replicated, so the first look can come up empty. Seating changes bring it back rather than
			// leaving the arrow hidden until the turn moves on.
			GameMode.OnSeatedPlayersChanged += Refresh;
			Data.StageId.OnValueChanged += HandleStageChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (Data) Data.StageId.OnValueChanged -= HandleStageChanged;
			GameMode.OnSeatedPlayersChanged -= Refresh;

			if (Data) Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			KillTween();
			Hide();
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();

		// A turn cleared inside a stage is only the beat between two (the street clears it while a bet
		// lands), so the arrow stays on the last seat and swings on from there. Only a stage that moves on
		// with nobody on the clock takes it away.
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current)
		{
			// Hidden but still aimed, so the next street's first turn swings round from the last seat.
			if (ResolveTurnSeat() || !_arrow) return;

			_arrow.SetActive(false);
			_signalledClientId = PokerGameData.NoTurn;
			KillSignal();
		}

		private void Refresh()
		{
			var seat = ResolveTurnSeat();
			if (!seat) return;

			if (_arrow && !_arrow.activeSelf) _arrow.SetActive(true);

			var swing = AimAt(seat);

			// Every new turn holder gets the signal; a refresh that finds the same holder leaves it blinking.
			var clientId = Data.CurrentTurnClientId.Value;
			if (clientId == _signalledClientId) return;

			PlaySignal(_signalledClientId != PokerGameData.NoTurn, swing);
			_signalledClientId = clientId;
		}

		// Turned onto the seat (popping first when the turn left somebody else), one flash, then blinking.
		private void PlaySignal(bool pop, float swing)
		{
			KillSignal();
			if (!_arrow) return;

			var arrowTransform = _arrow.transform;
			_signalTween = DOTween.Sequence().SetLink(gameObject);

			if (pop && _popDuration > 0f)
			{
				var half = _popDuration * 0.5f;
				_signalTween.Insert(0f, arrowTransform.DOScale(_restScale * _popScale, half).SetEase(Ease.OutQuad));
				_signalTween.Insert(half, arrowTransform.DOScale(_restScale, half).SetEase(Ease.InQuad));
			}

			_signalTween.AppendInterval(Mathf.Max(0f, swing - _signalTween.Duration()));

			if (_light && _flashDuration > 0f)
			{
				var half = _flashDuration * 0.5f;
				_signalTween.Append(FadeLight(_flashAlpha, half).SetEase(Ease.OutQuad));
				_signalTween.Append(FadeLight(_restAlpha, half).SetEase(Ease.InQuad));
			}

			_signalTween.OnComplete(StartBlink);
		}

		private void StartBlink()
		{
			if (!_light) return;

			_blinkTween = FadeLight(_blinkAlpha, _blinkHalfPeriod)
				.SetEase(_blinkEase)
				.SetLoops(-1, LoopType.Yoyo)
				.SetLink(gameObject);
		}

		private Tween FadeLight(float alpha, float duration) =>
			DOTween.To(() => _light.color.a, SetLightAlpha, alpha, duration);

		private void SetLightAlpha(float alpha)
		{
			if (!_light) return;

			var color = _light.color;
			color.a = alpha;
			_light.color = color;
		}

		private void KillSignal()
		{
			_signalTween?.Kill();
			_signalTween = null;
			_blinkTween?.Kill();
			_blinkTween = null;

			if (_arrow) _arrow.transform.localScale = _restScale;
			SetLightAlpha(_restAlpha);
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

		// Seconds the arrow takes to reach the seat: zero when it snaps or is already there.
		private float AimAt(PokerSeat seat)
		{
			var anchor = seat.CardAnchor;
			if (!_arrow || !anchor) return 0f;

			// Left wherever the prefab put it — on the table top — and only turned. A height worked out here
			// is a guess about the table that the scene view can show the answer to.
			var arrowTransform = _arrow.transform;

			// Flattened before it becomes a rotation: the seat's anchor need not be level with the arrow,
			// and following that difference would tip the pointer into the table.
			var toSeat = anchor.position - arrowTransform.position;
			toSeat.y = 0f;
			if (toSeat.sqrMagnitude < 0.0001f) return 0f;

			var target = Quaternion.LookRotation(toSeat.normalized, Vector3.up);

			KillTween();

			// The first aim of a hand snaps; after that it swings, so the handover reads as the turn
			// travelling round the table.
			if (!_aimed || _turnDuration <= 0f)
			{
				arrowTransform.rotation = target;
				_aimed = true;
				return 0f;
			}

			if (Quaternion.Angle(arrowTransform.rotation, target) < 0.01f) return 0f;

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

			return _turnDuration;
		}

		private void KillTween()
		{
			_turnTween?.Kill();
			_turnTween = null;
		}

		private void Hide()
		{
			_aimed = false;
			_signalledClientId = PokerGameData.NoTurn;
			KillSignal();

			if (_arrow) _arrow.SetActive(false);
		}
	}
}
