using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	public class UIPokerPlayerSlot : MonoBehaviour
	{
		[Header("Labels")]
		[SerializeField] private TextMeshProUGUI _nameLabel;
		[SerializeField] private TextMeshProUGUI _chipsLabel;
		[SerializeField] private TextMeshProUGUI _betLabel;
		[SerializeField] private TextMeshProUGUI _statusLabel;

		[Header("Turn")]
		[SerializeField] private GameObject _turnHighlight;
		[SerializeField] private Image _turnTimerFill;
		[SerializeField] private GameObject _dealerBadge;

		[Header("Cards")]
		[SerializeField] private GameObject _cardBackRoot;

		[Header("Folded")]
		[SerializeField] private CanvasGroup _canvasGroup;
		[SerializeField] private float _foldedAlpha = 0.35f;

		public void SetData(PokerPlayer player, bool isLocal, bool isTurn, bool isDealer, float turnNormalized)
		{
			if (!player || !player.Data) return;

			var data = player.Data;

			if (_nameLabel) _nameLabel.text = isLocal ? "You" : $"Seat {data.SeatIndex.Value + 1}";
			if (_chipsLabel) _chipsLabel.text = data.Chips.ToString();
			if (_betLabel) _betLabel.text = data.Bet.Value > 0 ? data.Bet.Value.ToString() : string.Empty;
			if (_statusLabel) _statusLabel.text = data.Status.Value.ToString();

			if (_turnHighlight) _turnHighlight.SetActive(isTurn);
			if (_turnTimerFill) _turnTimerFill.fillAmount = isTurn ? turnNormalized : 0f;
			if (_dealerBadge) _dealerBadge.SetActive(isDealer);
			if (_cardBackRoot) _cardBackRoot.SetActive(data.CardCount > 0);

			if (_canvasGroup) _canvasGroup.alpha = data.Status.Value == PokerPlayerStatus.Folded ? _foldedAlpha : 1f;
		}

		// Split out so the countdown can move without redrawing everything else on the row.
		public void SetTurnProgress(float normalized)
		{
			if (_turnTimerFill) _turnTimerFill.fillAmount = normalized;
		}
	}
}
