using DG.Tweening;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// One player in the round's turn order: their gnome icon, swelling out with their name beside it while
	// it is their turn.
	public class UIPokerTurnOrderEntry : MonoBehaviour
	{
		[Tooltip("Optional. Keeps its authored sprite while the player has no colour yet.")]
		[SerializeField] private Image _icon;

		[Tooltip("Scaled up on the player's turn. Art inside the entry, so the column's layout never moves.")]
		[Required]
		[SerializeField] private RectTransform _body;

		[Tooltip("Shown only on the player's turn.")]
		[SerializeField] private CanvasGroup _nameGroup;

		[SerializeField] private TMP_Text _nameLabel;

		[Header("Turn")]
		[MinValue(1f)]
		[SerializeField] private float _currentScale = 1.5f;

		[MinValue(0f)]
		[SerializeField] private float _scaleDuration = 0.35f;

		[SerializeField] private Ease _growEase = Ease.OutBack;
		[SerializeField] private Ease _shrinkEase = Ease.OutQuad;

		[MinValue(0f)]
		[SerializeField] private float _nameFadeDuration = 0.2f;

		private Tween _scaleTween;
		private Tween _nameTween;
		private bool? _isCurrent;

		public PokerPlayer Player { get; private set; }

		private void OnDisable()
		{
			_scaleTween?.Kill();
			_nameTween?.Kill();
			_isCurrent = null;
		}

		public void Bind(PokerPlayer player)
		{
			Player = player;
			name = $"Entry_{player.ClientId}";

			var icon = player.Icon;
			if (_icon && icon) _icon.sprite = icon;
			if (_nameLabel) _nameLabel.text = player.PlainName;
		}

		// Snaps the first time, so a screen joining mid-round does not watch every icon settle. A delay holds a
		// turn's pop back behind something that comes first, such as the street's deal.
		public void SetCurrent(bool current, float delay = 0f)
		{
			if (_isCurrent == current) return;

			var animate = _isCurrent.HasValue;
			_isCurrent = current;
			Animate(animate, delay);
		}

		// Plays the turn's pop again from the start, for a delay learned after the turn already began.
		public void ReplayCurrent(float delay)
		{
			if (_isCurrent != true) return;

			_body.localScale = Vector3.one;
			if (_nameGroup) _nameGroup.alpha = 0f;
			Animate(true, delay);
		}

		private void Animate(bool animate, float delay)
		{
			var current = _isCurrent == true;

			var scale = current ? _currentScale : 1f;
			var alpha = current ? 1f : 0f;

			_scaleTween?.Kill();
			_nameTween?.Kill();

			if (!animate)
			{
				_body.localScale = Vector3.one * scale;
				if (_nameGroup) _nameGroup.alpha = alpha;
				return;
			}

			_scaleTween = _body.DOScale(scale, _scaleDuration).SetEase(current ? _growEase : _shrinkEase).SetDelay(delay).SetUpdate(true).SetLink(gameObject);

			if (_nameGroup)
			{
				_nameTween = DOTween.To(() => _nameGroup.alpha, value => _nameGroup.alpha = value, alpha, _nameFadeDuration)
					.SetDelay(delay).SetUpdate(true).SetLink(gameObject);
			}
		}
	}
}
