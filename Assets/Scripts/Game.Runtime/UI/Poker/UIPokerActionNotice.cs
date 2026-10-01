using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// One announcement over the table: a plank with one line on it, fading in and taking itself out. Visual
	// only — the feed decides when one appears; this only knows the shapes an announcement can take and how to leave.
	//
	// Three shapes, because an announcement has three things it can say after the verb, and they are not
	// interchangeable: nothing at all, a number in some currency, or another player. A name is written into the
	// line; a count sits beside its icon, which is what one shared "detail" string could not express.
	[RequireComponent(typeof(CanvasGroup))]
	public class UIPokerActionNotice : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private CanvasGroup _group;

		[Tooltip("The one line on the plank: who (when named), what, and at whom.")]
		[SerializeField] private TextMeshProUGUI _label;

		[Header("Amount Row")]
		[Tooltip("A count and what it is counted in. Hidden outright when nothing was moved — FOLD x0 reads as a bug.")]
		[SerializeField] private GameObject _amountRoot;

		[SerializeField] private TextMeshProUGUI _amountLabel;

		[Tooltip("On the amount label. Its minimum is pinned to the count's width, so a long line shrinks the words and never the count.")]
		[SerializeField] private LayoutElement _amountLayout;

		[SerializeField] private Image _amountIcon;

		[Header("Motion")]
		[MinValue(0f)]
		[SerializeField] private float _fadeDuration = 0.2f;

		private void Reset()
		{
			_group = GetComponent<CanvasGroup>();
		}

		// ACTION — an act with nothing to price and nobody at the other end of it.
		public void Show(string playerName, string action, float lifetime)
		{
			Fill(playerName, action, null);
			SetActive(_amountRoot, false);

			Play(lifetime);
		}

		// ACTION xN — what an act cost, in whatever it was counted in. A missing icon leaves the authored
		// sprite; zero hides the row rather than announcing that nothing moved.
		public void ShowAmount(string playerName, string action, int amount, Sprite icon, float lifetime)
		{
			Fill(playerName, action, null);

			var counted = amount > 0;
			SetActive(_amountRoot, counted);

			if (counted)
			{
				if (_amountLabel) _amountLabel.text = $"x{amount}";
				if (_amountLabel && _amountLayout) _amountLayout.minWidth = _amountLabel.GetPreferredValues(_amountLabel.text).x;
				if (_amountIcon && icon) _amountIcon.sprite = icon;
			}

			Play(lifetime);
		}

		// ACTION name — an act aimed at somebody.
		public void ShowTarget(string playerName, string action, string targetName, float lifetime)
		{
			Fill(playerName, action, targetName);
			SetActive(_amountRoot, false);

			Play(lifetime);
		}

		private void Fill(string playerName, string action, string targetName)
		{
			if (!_label) return;

			var verb = action != null ? action.ToLowerInvariant() : string.Empty;
			var line = string.IsNullOrEmpty(playerName) ? verb : $"{playerName} {verb}";
			_label.text = Capitalize(string.IsNullOrEmpty(targetName) ? line : $"{line} {targetName}");
		}

		// A notice reads as a sentence: the words lower whatever case they arrive in, names as the player wrote
		// them, and the first letter capital.
		public static string Capitalize(string text) =>
			string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

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
