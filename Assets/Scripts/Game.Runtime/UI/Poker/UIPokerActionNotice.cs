using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// One announcement over the table, fading in and taking itself out. Visual only — the feed decides
	// when one appears; this only knows the shapes an announcement can take and how to leave.
	//
	// Three shapes, because an announcement has three things it can say underneath the verb, and they are
	// not interchangeable: nothing at all, a number in some currency, or another player. The last two look
	// different on purpose — a name is drawn like the name on top, a count is drawn beside its icon — and
	// that is exactly what one shared "detail" string could not express.
	[RequireComponent(typeof(CanvasGroup))]
	public class UIPokerActionNotice : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private CanvasGroup _group;

		[SerializeField] private TextMeshProUGUI _nameLabel;
		[SerializeField] private TextMeshProUGUI _actionLabel;

		[Header("Amount Row")]
		[Tooltip("A count and what it is counted in. Hidden outright when nothing was moved — FOLD x0 reads as a bug.")]
		[SerializeField] private GameObject _amountRoot;

		[SerializeField] private TextMeshProUGUI _amountLabel;
		[SerializeField] private Image _amountIcon;

		[Header("Target Row")]
		[Tooltip("Another player, drawn the way the name on top is drawn — both rows are somebody, so they read as somebody.")]
		[SerializeField] private GameObject _targetRoot;

		[SerializeField] private TextMeshProUGUI _targetLabel;

		[Header("Motion")]
		[MinValue(0f)]
		[SerializeField] private float _fadeDuration = 0.2f;

		private void Reset()
		{
			_group = GetComponent<CanvasGroup>();
		}

		// name / ACTION — an act with nothing to price and nobody at the other end of it.
		public void Show(string playerName, string action, float lifetime)
		{
			Fill(playerName, action);
			SetActive(_amountRoot, false);
			SetActive(_targetRoot, false);

			Play(lifetime);
		}

		// name / ACTION / xN — what an act cost, in whatever it was counted in. A missing icon leaves the
		// authored sprite; zero hides the row rather than announcing that nothing moved.
		public void ShowAmount(string playerName, string action, int amount, Sprite icon, float lifetime)
		{
			Fill(playerName, action);
			SetActive(_targetRoot, false);

			var counted = amount > 0;
			SetActive(_amountRoot, counted);

			if (counted)
			{
				if (_amountLabel) _amountLabel.text = $"x{amount}";
				if (_amountIcon && icon) _amountIcon.sprite = icon;
			}

			Play(lifetime);
		}

		// name / ACTION / name — an act aimed at somebody.
		public void ShowTarget(string playerName, string action, string targetName, float lifetime)
		{
			Fill(playerName, action);
			SetActive(_amountRoot, false);
			SetActive(_targetRoot, true);

			if (_targetLabel) _targetLabel.text = targetName;

			Play(lifetime);
		}

		private void Fill(string playerName, string action)
		{
			if (_nameLabel) _nameLabel.text = playerName;
			if (_actionLabel) _actionLabel.text = action;
		}

		private static void SetActive(GameObject root, bool active)
		{
			if (root && root.activeSelf != active) root.SetActive(active);
		}

		// lifetime is the whole time on screen, fades included, so a caller handing over a stage's own
		// duration gets a notice that is gone when the stage is — rather than one that starts fading as
		// the table moves on.
		private void Play(float lifetime)
		{
			var hold = Mathf.Max(0f, lifetime - _fadeDuration * 2f);

			// Unscaled and linked, like every HUD tween: the table can be paused under it.
			_group.alpha = 0f;
			DOTween.Sequence()
				.Append(DOTween.To(() => _group.alpha, alpha => _group.alpha = alpha, 1f, _fadeDuration))
				.AppendInterval(hold)
				.Append(DOTween.To(() => _group.alpha, alpha => _group.alpha = alpha, 0f, _fadeDuration))
				.AppendCallback(() => Destroy(gameObject))
				.SetUpdate(true)
				.SetLink(gameObject);
		}
	}
}
