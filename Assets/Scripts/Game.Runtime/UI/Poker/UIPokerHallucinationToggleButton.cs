using DG.Tweening;
using Game.Runtime.GameMode.Poker.Hallucination;
using Game.Runtime.UI.Button;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// Lets a player who has gone under switch their hallucination off and watch the rest of the match sober,
	// and back on. Shown only while the local player is out; reviving drops the choice in the controller.
	// Each press blinks (the controller's own transition) and then rests for a cooldown drawn as a dark bar
	// running out across the button. The button stays lit through it, like every other HUD button; a press
	// while it runs is ignored rather than drawn as disabled.
	public class UIPokerHallucinationToggleButton : UIPokerView
	{
		[SerializeField] private GameObject _content;
		[SerializeField] private UIButton _button;

		[Header("Icon")]
		[SerializeField] private Image _icon;
		[SerializeField] private Sprite _shownSprite;
		[SerializeField] private Sprite _hiddenSprite;

		[Header("Cooldown")]
		[Tooltip("A Filled Horizontal image over the button: full when pressed, emptied over the cooldown.")]
		[SerializeField] private Image _cooldownOverlay;

		[Min(0f)]
		[SerializeField] private float _cooldown = 5f;

		private PokerHallucinationController _controller;
		private Tween _cooldownTween;

		private bool IsCoolingDown => _cooldownTween != null && _cooldownTween.IsActive();

		private void Awake()
		{
			if (_content) _content.SetActive(false);
			if (_cooldownOverlay) _cooldownOverlay.fillAmount = 0f;
		}

		private void OnDestroy() => _cooldownTween?.Kill();

		protected override void OnBind()
		{
			// Reached from the local player, as the blink does: the player is spawned, this HUD is not.
			_controller = LocalPlayer.GetComponentInChildren<PokerHallucinationController>(true);

			LocalData.OnHallucinationChanged += HandleHallucinationChanged;
			if (_controller) _controller.OnEffectsHiddenChanged += Refresh;
			if (_button) _button.OnClick += HandleClick;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_button) _button.OnClick -= HandleClick;
			if (_controller) _controller.OnEffectsHiddenChanged -= Refresh;
			if (LocalData) LocalData.OnHallucinationChanged -= HandleHallucinationChanged;

			_controller = null;
			StopCooldown();

			if (_content) _content.SetActive(false);
		}

		private void HandleHallucinationChanged(int previous, int current) => Refresh();

		private void HandleClick()
		{
			if (!_controller || IsCoolingDown || !_controller.CanHideEffects) return;

			_controller.SetEffectsHidden(!_controller.EffectsHidden);
			StartCooldown();
		}

		private void StartCooldown()
		{
			StopCooldown();
			if (_cooldown <= 0f) return;

			if (_cooldownOverlay) _cooldownOverlay.fillAmount = 1f;

			_cooldownTween = DOTween.To(() => _cooldownOverlay ? _cooldownOverlay.fillAmount : 0f,
					value => { if (_cooldownOverlay) _cooldownOverlay.fillAmount = value; }, 0f, _cooldown)
				.SetEase(Ease.Linear)
				.SetUpdate(true)
				.SetTarget(this)
				.OnComplete(StopCooldown);
		}

		private void StopCooldown()
		{
			_cooldownTween?.Kill();
			_cooldownTween = null;

			if (_cooldownOverlay) _cooldownOverlay.fillAmount = 0f;
		}

		private void Refresh()
		{
			var show = _controller && LocalData && !LocalData.IsAlive && PokerHallucinationController.EffectsAllowed;

			if (!show) StopCooldown();
			if (_content && _content.activeSelf != show) _content.SetActive(show);
			if (!show) return;

			if (_icon) _icon.sprite = _controller.EffectsHidden ? _hiddenSprite : _shownSprite;
		}
	}
}
