using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Runtime.GameMode.Poker.Hallucination;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// One player's hallucination, drawn the same way wherever it is drawn: on the owner's HUD and over
	// everybody else's head. It is handed a player rather than finding one, so the HUD binds the local
	// player and a name tag binds the body it hangs on, and neither copy knows which it is.
	//
	// Four things, all read off replicated state:
	// - the fill, eased toward the rate rather than snapped, so a mouthful is seen going down;
	// - a mark at every rung of the ladder, spawned from the asset so a retuned ladder redraws itself;
	// - the skull, resting at the end and swept along the bar when a Colorful roll is shown, stopping on
	//   the number — inside the fill is under, past it is spared;
	// - the kinds already eaten, in the order they were first met.
	public class UIPokerHallucinationMeter : MonoBehaviour
	{
		[Header("Fill")]
		[Tooltip("Filled Horizontal image over the gradient: the colours stay where they are on the bar and only how much of it shows changes.")]
		[SerializeField] private Image _fill;

		[Min(0f)]
		[SerializeField] private float _fillDuration = 0.6f;

		[SerializeField] private Ease _fillEase = Ease.OutCubic;

		[Header("Rungs")]
		[Tooltip("Stretched over the fill. Each mark is anchored at its rung's threshold along it.")]
		[SerializeField] private RectTransform _rungContainer;

		[Tooltip("One mark, instantiated per rung under the container (UI_HallucinationRung).")]
		[SerializeField] private RectTransform _rungPrefab;

		[Header("Skull")]
		[Tooltip("Anchored along the fill's width; its horizontal anchor is the value it points at.")]
		[SerializeField] private RectTransform _knot;

		[Tooltip("Full passes end to end before the last one settles on the number.")]
		[Min(0)]
		[SerializeField] private int _sweepLoops = 3;

		[SerializeField] private Ease _sweepEase = Ease.InOutSine;
		[SerializeField] private Ease _settleEase = Ease.OutCubic;

		[Tooltip("Seconds the skull takes to go back to the end once the result has been held. How long it holds arrives with the roll, from the beat's pacing, so the skull leaves exactly when the table moves on.")]
		[Min(0f)]
		[SerializeField] private float _returnDuration = 0.35f;

		[SerializeField] private Ease _returnEase = Ease.InOutSine;

		[Header("Eaten")]
		[Tooltip("Auto-layout row the icons are laid out in.")]
		[SerializeField] private RectTransform _eatenRow;

		[Tooltip("One icon, instantiated per kind eaten under the row (UI_ConsumedItemIcon).")]
		[SerializeField] private Image _eatenPrefab;

		[Tooltip("Where each kind's icon comes from.")]
		[SerializeField] private PokerItemDatabase _database;

		[Min(0f)]
		[SerializeField] private float _popDuration = 0.3f;

		[SerializeField] private Ease _popEase = Ease.OutBack;

		private readonly List<RectTransform> _rungs = new();
		private readonly List<Image> _eaten = new();

		private PokerPlayer _player;
		private PokerPlayerData _data;
		private PokerItemConsumeController _consume;
		private PokerHallucinationRollController _roll;

		private Tween _fillTween;
		private Sequence _knotSequence;
		private float _knotValue = 1f;
		private PokerHallucinationTiers _tiers;

		// A rung reached or lost: its index, and whether it was climbed. Raised as the fill starts to move, so
		// feedback lands with the change rather than after the ease.
		public event Action<int, bool> OnRungCrossed;

		// The skull has stopped on a roll: the number, and whether it means going under.
		public event Action<int, bool> OnRollSettled;

		private void OnDestroy() => Unbind();

		public void Bind(PokerPlayer player)
		{
			if (player == _player) return;

			Unbind();

			_player = player;
			if (!_player) return;

			_data = _player.Data;
			_consume = _player.ItemConsume;
			_roll = _player.HallucinationRoll;

			if (_data) _data.OnHallucinationChanged += HandleHallucinationChanged;
			if (_consume) _consume.Consumed.OnListChanged += HandleConsumedChanged;
			if (_roll) _roll.OnRollStarted += HandleRollStarted;
			if (_roll) _roll.OnRollSettled += HandleRollSettled;

			// Drawn as it stands: a player arriving at a table already halfway under sees how far gone
			// everybody is, not a row of empty bars waiting for the next mouthful.
			BuildRungs(_player.GetComponentInChildren<PokerHallucinationController>(true));
			SnapFill();
			RefreshEaten(false);
			PlaceKnot(1f);
		}

		public void Unbind()
		{
			if (_roll) _roll.OnRollSettled -= HandleRollSettled;
			if (_roll) _roll.OnRollStarted -= HandleRollStarted;
			if (_consume) _consume.Consumed.OnListChanged -= HandleConsumedChanged;
			if (_data) _data.OnHallucinationChanged -= HandleHallucinationChanged;

			_fillTween?.Kill();
			_fillTween = null;

			_knotSequence?.Kill();
			_knotSequence = null;

			_roll = null;
			_consume = null;
			_data = null;
			_player = null;
		}

		private float Rate => _data ? _data.HallucinationRate.Value / (float)PokerPlayerData.MaxHallucination : 0f;

		private void HandleHallucinationChanged(int previous, int current)
		{
			RaiseRungCrossings(previous, current);

			if (!_fill) return;

			_fillTween?.Kill();

			if (_fillDuration <= 0f)
			{
				_fill.fillAmount = Rate;
				return;
			}

			_fillTween = DOTween.To(() => _fill.fillAmount, value => _fill.fillAmount = value, Rate, _fillDuration)
				.SetEase(_fillEase)
				.SetLink(gameObject);
		}

		// Every rung passed between the two rates, in the order they were passed. Drawn off the same ladder
		// the marks are, so a feedback fires on exactly the mark the fill runs over.
		private void RaiseRungCrossings(int previous, int current)
		{
			if (!_tiers || OnRungCrossed == null || previous == current) return;

			var rungs = _tiers.Rungs;
			var climbed = current > previous;

			for (var i = 0; i < rungs.Count; i++)
			{
				var index = climbed ? i : rungs.Count - 1 - i;
				var threshold = rungs[index].Threshold;

				var crossed = climbed
					? previous < threshold && current >= threshold
					: previous >= threshold && current < threshold;

				if (crossed) OnRungCrossed?.Invoke(index, climbed);
			}
		}

		private void HandleRollSettled(int roll, bool fatal) => OnRollSettled?.Invoke(roll, fatal);

		// The mark drawn for a rung, for feedback that wants to point at the one just crossed.
		public RectTransform RungMark(int index) => index >= 0 && index < _rungs.Count ? _rungs[index] : null;

		public RectTransform Knot => _knot;

		private void SnapFill()
		{
			_fillTween?.Kill();
			if (_fill) _fill.fillAmount = Rate;
		}

		private void BuildRungs(PokerHallucinationController hallucination)
		{
			if (!_rungContainer || !_rungPrefab) return;

			var tiers = hallucination ? hallucination.Tiers : null;
			_tiers = tiers;
			var count = tiers ? tiers.Rungs.Count : 0;

			// Marks already made are re-placed rather than rebuilt, so a rebind costs nothing new.
			while (_rungs.Count < count)
			{
				_rungs.Add(Instantiate(_rungPrefab, _rungContainer));
			}

			for (var i = 0; i < _rungs.Count; i++)
			{
				var mark = _rungs[i];
				var used = i < count;

				mark.gameObject.SetActive(used);
				if (!used) continue;

				var at = tiers.Rungs[i].Threshold / (float)PokerPlayerData.MaxHallucination;
				SetHorizontalAnchor(mark, at);
			}
		}

		private void HandleConsumedChanged(NetworkListEvent<PokerItemUnit> change) =>
			RefreshEaten(change.Type == NetworkListEvent<PokerItemUnit>.EventType.Add);

		// Views already made are re-bound in order rather than destroyed and made again, so the row never
		// draws the old set under the new one for a frame.
		private void RefreshEaten(bool popLast)
		{
			if (!_eatenRow || !_eatenPrefab) return;

			var count = _consume ? _consume.Consumed.Count : 0;

			while (_eaten.Count < count)
			{
				_eaten.Add(Instantiate(_eatenPrefab, _eatenRow));
			}

			for (var i = 0; i < _eaten.Count; i++)
			{
				var icon = _eaten[i];
				var used = i < count;

				icon.gameObject.SetActive(used);
				if (!used) continue;

				icon.sprite = IconFor(_consume.Consumed[i]);
				icon.transform.DOKill();
				icon.transform.localScale = Vector3.one;
			}

			if (!popLast || count == 0 || _popDuration <= 0f) return;

			var popped = _eaten[count - 1].transform;
			popped.localScale = Vector3.zero;
			popped.DOScale(Vector3.one, _popDuration).SetEase(_popEase).SetLink(popped.gameObject);
		}

		private Sprite IconFor(PokerItemType itemType) =>
			_database && _database.TryGetEntry(itemType, out var entry) ? entry.Icon : null;

		// Back and forth, then onto the number, then home. Every duration arrives with the roll, paced by the beat
		// that started it, so the skull stops and leaves exactly when the server pays the result and moves on —
		// this meter keeps no copy of any of them.
		private void HandleRollStarted(float leadIn, float sweep, float hold, int roll)
		{
			if (!_knot) return;

			_knotSequence?.Kill();

			var target = Mathf.Clamp01(roll / (float)PokerPlayerData.MaxHallucination);
			var legs = _sweepLoops * 2 + 1;
			var leg = sweep / legs;

			var sequence = DOTween.Sequence().SetLink(gameObject);
			sequence.AppendInterval(leadIn);

			for (var i = 0; i < _sweepLoops; i++)
			{
				sequence.Append(KnotTween(0f, leg, _sweepEase));
				sequence.Append(KnotTween(1f, leg, _sweepEase));
			}

			sequence.Append(KnotTween(target, leg, _settleEase));
			sequence.AppendInterval(hold);
			sequence.Append(KnotTween(1f, _returnDuration, _returnEase));

			_knotSequence = sequence;
		}

		private Tween KnotTween(float to, float duration, Ease ease) =>
			DOTween.To(() => _knotValue, PlaceKnot, to, duration).SetEase(ease);

		private void PlaceKnot(float value)
		{
			_knotValue = value;
			if (_knot) SetHorizontalAnchor(_knot, value);
		}

		private static void SetHorizontalAnchor(RectTransform rect, float at)
		{
			rect.anchorMin = new Vector2(at, rect.anchorMin.y);
			rect.anchorMax = new Vector2(at, rect.anchorMax.y);
			rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
		}
	}
}
