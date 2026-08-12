using Game.Runtime.GameMode.Poker;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// The "whose turn is it" readout — the same countdown the action bar shows, but for the times the
	// table is waiting on somebody else.
	public class UIPokerTurnBanner : UIPokerView
	{
		[Header("References")]
		[SerializeField] private GameObject _panel;
		[SerializeField] private TextMeshProUGUI _turnLabel;
		[SerializeField] private TextMeshProUGUI _timerLabel;
		[SerializeField] private Image _timerFill;

		[Header("Colors")]
		[SerializeField] private Color _localTurnColor = new(0.35f, 0.85f, 0.4f);
		[SerializeField] private Color _remoteTurnColor = new(0.9f, 0.75f, 0.3f);
		[SerializeField] private float _warningThreshold = 5f;
		[SerializeField] private Color _warningColor = new(0.9f, 0.3f, 0.3f);

		protected override bool WantsTick => Data && Data.HasTurn;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			if (_panel) _panel.SetActive(false);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();

		private void Refresh()
		{
			var hasTurn = Data.HasTurn;
			if (_panel && _panel.activeSelf != hasTurn) _panel.SetActive(hasTurn);
			if (!hasTurn) return;

			if (_turnLabel)
			{
				var turnClientId = Data.CurrentTurnClientId.Value;
				var turnPlayer = GameMode.FindSeatedPlayer(turnClientId);
				var seatSuffix = turnPlayer ? $"Seat {turnPlayer.Data.SeatIndex.Value + 1}" : "Player";

				_turnLabel.text = turnClientId == LocalClientId ? "Your turn" : $"{seatSuffix}'s turn";
			}

			OnTick();
		}

		protected override void OnTick()
		{
			var remaining = Data.TurnRemaining;

			if (_timerLabel) _timerLabel.text = Mathf.CeilToInt(remaining).ToString();
			if (!_timerFill) return;

			_timerFill.fillAmount = Data.TurnNormalized;
			_timerFill.color = remaining <= _warningThreshold
				? _warningColor
				: IsLocalTurn ? _localTurnColor : _remoteTurnColor;
		}
	}
}
