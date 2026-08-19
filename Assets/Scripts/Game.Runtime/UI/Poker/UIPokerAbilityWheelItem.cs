using DG.Tweening;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.UI.Wheel;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// One ability on the wheel. Being the chosen one is drawn as a change of colour and nothing else: the
	// rows keep their size, so the column stays a steady list rather than something that swells and shrinks
	// under the eye every time the wheel turns. A cheat and its honest twin share icon and name, which is
	// the whole point — the wheel must never be where the table could tell them apart.
	public class UIPokerAbilityWheelItem : UIWheelItemView<PokerAbility>
	{
		[Header("References")]
		[SerializeField] private Image _icon;

		[SerializeField] private TextMeshProUGUI _nameLabel;

		[Header("Tint")]
		[Tooltip("The plate and the diamond both take these, so the whole row lights together.")]
		[SerializeField] private Image _plate;

		[SerializeField] private Image _frame;

		[SerializeField] private Color _normalColor = new(0.36f, 0.28f, 0.20f);
		[SerializeField] private Color _selectedColor = new(0.62f, 0.49f, 0.34f);

		[SerializeField] private Color _normalAbilityColor;
		[SerializeField] private Color _cheatAbilityColor;


		[Header("Name")]
		[Tooltip("Unselected rows stay readable rather than blank — the list is what the player is choosing between.")]
		[PropertyRange(0f, 1f)]
		[SerializeField] private float _unselectedNameAlpha = 0.55f;

		[SerializeField] private CanvasGroup _nameGroup;

		[Header("Press")]
		[Required]
		[Tooltip("Punched when the ability is played. A child, never this object: the wheel drives the row's own scale and the two would fight over it.")]
		[SerializeField] private RectTransform _content;

		[MinValue(1f)]
		[SerializeField] private float _pressScale = 1.12f;

		[MinValue(0f)]
		[SerializeField] private float _pressGrowDuration = 0.07f;

		[MinValue(0f)]
		[SerializeField] private float _pressSettleDuration = 0.13f;

		[Header("Timing")]
		[MinValue(0f)]
		[SerializeField] private float _fadeDuration = 0.15f;

		private Tween _pressTween;
		private Tween _plateTween;
		private Tween _frameTween;
		private Tween _nameTween;

		private void OnDestroy()
		{
			KillTweens();
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
			_nameLabel.text = ability ? ability.DisplayName : string.Empty;

			_icon.color = ability ? (ability.Kind == PokerAbilityKind.Cheat ? _cheatAbilityColor : _normalAbilityColor) : Color.white;
		}

		protected override void OnSelect(bool instant) => Draw(true, instant);
		protected override void OnUnSelect(bool instant) => Draw(false, instant);

		private void Draw(bool selected, bool instant)
		{
			var color = selected ? _selectedColor : _normalColor;

			_plateTween = Tint(_plate, color, _plateTween, instant);
			// _frameTween = Tint(_frame, color, _frameTween, instant);

			FadeName(selected ? 1f : _unselectedNameAlpha, instant);
		}

		private Tween Tint(Image image, Color color, Tween running, bool instant)
		{
			running?.Kill();

			if (!image) return null;

			// Snapped when the row is being set up rather than changed: a slot has no previous colour to
			// travel from, so animating there would show it arriving from whatever the prefab was saved with.
			if (instant || _fadeDuration <= 0f || !isActiveAndEnabled)
			{
				image.color = color;
				return null;
			}

			return DOTween.To(() => image.color, value => image.color = value, color, _fadeDuration)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		private void FadeName(float alpha, bool instant)
		{
			_nameTween?.Kill();

			if (!_nameGroup) return;

			if (instant || _fadeDuration <= 0f || !isActiveAndEnabled)
			{
				_nameGroup.alpha = alpha;
				return;
			}

			_nameTween = DOTween.To(() => _nameGroup.alpha, value => _nameGroup.alpha = value, alpha, _fadeDuration)
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		// Swells and settles back. Nothing waits on it: how long to hold before the ability actually fires is
		// the panel's decision, so a row can be punched without the caller knowing the animation's shape.
		public void PlayPress()
		{
			if (!_content) return;

			_pressTween?.Kill();
			_content.localScale = Vector3.one;

			_pressTween = DOTween.Sequence()
				.Append(_content.DOScale(_pressScale, _pressGrowDuration).SetEase(Ease.OutQuad))
				.Append(_content.DOScale(1f, _pressSettleDuration).SetEase(Ease.OutBack))
				.SetUpdate(true)
				.SetLink(gameObject);
		}

		private void KillTweens()
		{
			_pressTween?.Kill();
			_plateTween?.Kill();
			_frameTween?.Kill();
			_nameTween?.Kill();
		}
	}
}