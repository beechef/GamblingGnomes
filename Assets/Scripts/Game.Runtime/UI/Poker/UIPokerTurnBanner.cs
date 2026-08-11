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

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnUnbind()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnTick()
		{
			var hasTurn = Data.HasTurn;
			if (_panel && _panel.activeSelf != hasTurn) _panel.SetActive(hasTurn);
			if (!hasTurn) return;

			var turnClientId = Data.CurrentTurnClientId.Value;
			var isLocal = turnClientId == LocalClientId;
			var remaining = Data.TurnRemaining;

			if (_turnLabel)
			{
				var turnPlayer = GameMode.FindSeatedPlayer(turnClientId);
				var seatSuffix = turnPlayer ? $"Seat {turnPlayer.Data.SeatIndex.Value + 1}" : "Player";
				_turnLabel.text = isLocal ? "Your turn" : $"{seatSuffix}'s turn";
			}

			if (_timerLabel) _timerLabel.text = Mathf.CeilToInt(remaining).ToString();

			if (_timerFill)
			{
				_timerFill.fillAmount = Data.TurnNormalized;
				_timerFill.color = remaining <= _warningThreshold
					? _warningColor
					: isLocal ? _localTurnColor : _remoteTurnColor;
			}
		}
	}
}
