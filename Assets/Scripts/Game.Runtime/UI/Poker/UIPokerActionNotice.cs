using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// One announcement: who did what and what it cost, fading in over the table and taking itself out.
	// Visual only — the feed decides when one appears; this only knows how to show and how to leave.
	[RequireComponent(typeof(CanvasGroup))]
	public class UIPokerActionNotice : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private CanvasGroup _group;

		[SerializeField] private TextMeshProUGUI _nameLabel;
		[SerializeField] private TextMeshProUGUI _actionLabel;

		[Tooltip("The cost row. Hidden outright for an action that moved no money — FOLD x0 reads as a bug.")]
		[SerializeField] private GameObject _amountRoot;

		[SerializeField] private TextMeshProUGUI _amountLabel;

		[Header("Motion")]
		[MinValue(0f)]
		[SerializeField] private float _fadeDuration = 0.2f;

		private void Reset()
		{
			_group = GetComponent<CanvasGroup>();
		}

		public void Show(string playerName, string action, int amount, float lifetime)
		{
			if (_nameLabel) _nameLabel.text = playerName;
			if (_actionLabel) _actionLabel.text = action;

			var showAmount = amount > 0;
			if (_amountRoot && _amountRoot.activeSelf != showAmount) _amountRoot.SetActive(showAmount);
			if (_amountLabel) _amountLabel.text = $"x{amount}";

			// Unscaled and linked, like every HUD tween: the table can be paused under it.
			_group.alpha = 0f;
			DOTween.Sequence()
				.Append(DOTween.To(() => _group.alpha, alpha => _group.alpha = alpha, 1f, _fadeDuration))
				.AppendInterval(Mathf.Max(0f, lifetime))
				.Append(DOTween.To(() => _group.alpha, alpha => _group.alpha = alpha, 0f, _fadeDuration))
				.AppendCallback(() => Destroy(gameObject))
				.SetUpdate(true)
				.SetLink(gameObject);
		}
	}
}
