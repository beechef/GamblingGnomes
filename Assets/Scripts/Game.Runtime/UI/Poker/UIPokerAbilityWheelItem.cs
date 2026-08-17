using DG.Tweening;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.UI.Wheel;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// One ability on the wheel: its icon always, its name only while it is the one in the middle. A cheat
	// and its honest twin share both, which is the whole point — the wheel must never be where the table
	// could tell them apart.
	public class UIPokerAbilityWheelItem : UIWheelItemView<PokerAbility>
	{
		[Header("References")]
		[SerializeField] private Image _icon;
		[SerializeField] private TextMeshProUGUI _nameLabel;

		[Header("Fade")]
		[Tooltip("How the name arrives and leaves as the wheel turns. Faded rather than switched so a fast spin does not flicker.")]
		[SerializeField] private CanvasGroup _nameGroup;

		[MinValue(0f)]
		[SerializeField] private float _fadeDuration = 0.15f;

		private Tween _fade;

		private void OnDestroy()
		{
			_fade?.Kill();
		}

		protected override void OnBind(PokerAbility ability)
		{
			if (_icon)
			{
				_icon.sprite = ability ? ability.Icon : null;
				_icon.enabled = _icon.sprite;
			}

			if (!_nameLabel) return;

			// Only the holder ever sees this wheel, and knowing your own card is a cheat is what makes
			// playing it a decision. A stand-in until the art tells the two apart on the icon — nothing
			// outside this client is told, so the guessing game is untouched.
			_nameLabel.text = ability
				? ability.Kind == PokerAbilityKind.Cheat ? $"{ability.DisplayName} (cheat)" : ability.DisplayName
				: string.Empty;
		}

		protected override void OnSelect() => FadeName(1f);
		protected override void OnUnSelect() => FadeName(0f);

		private void FadeName(float alpha)
		{
			if (!_nameGroup) return;

			_fade?.Kill();

			if (_fadeDuration <= 0f || !isActiveAndEnabled)
			{
				_nameGroup.alpha = alpha;
				return;
			}

			_fade = DOTween.To(() => _nameGroup.alpha, value => _nameGroup.alpha = value, alpha, _fadeDuration)
				.SetUpdate(true)
				.SetLink(gameObject);
		}
	}
}
