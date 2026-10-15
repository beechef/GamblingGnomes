using DG.Tweening;
using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.UI.Button;
using Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// How many item cards this player is holding, written into a label (the Items button's own). Which ones
	// is shown in the item picker. At a table that deals no items the optional content is switched off.
	// Cards arriving are counted in one at a time: each floats a "+1" up off the icon, pops the icon and adds
	// one to the label, however many landed at once. Losing cards (played, a match ending) shows at once.
	public class UIPokerItemCount : UIPokerView
	{
		[Tooltip("Optional. Shown only at a table that deals items. A child, so this view keeps listening while it is hidden.")]
		[SerializeField] private GameObject _content;

		[SerializeField] private TMP_Text _countLabel;

		[Header("Gain")]
		[Tooltip("The icon popped for each card gained, through its own scale visual (which owns its scale).")]
		[SerializeField] private UIButtonScaleVisual _icon;

		[Tooltip("The \"+1\" floated up for each card gained; spawned beside the icon, destroyed when it has faded.")]
		[SerializeField] private RectTransform _gainPopupPrefab;

		[Tooltip("Where a \"+1\" starts, from the icon's centre.")]
		[SerializeField] private Vector2 _gainPopupOffset = new(0f, 40f);

		[Tooltip("How far a \"+1\" rises while it fades.")]
		[SerializeField] private float _gainPopupRise = 60f;

		[MinValue(0.05f)]
		[SerializeField] private float _gainPopupDuration = 0.8f;

		[SerializeField] private Ease _gainPopupEase = Ease.OutCubic;

		[Tooltip("Seconds between two \"+1\"s when several cards land together.")]
		[MinValue(0.05f)]
		[SerializeField] private float _gainInterval = 0.35f;

		[SerializeField] private Vector3 _iconPunch = new(0.35f, 0.35f, 0f);

		[MinValue(0.05f)]
		[SerializeField] private float _iconPunchDuration = 0.35f;

		private int _shownCount;
		private Tween _gainTween;

		private void Awake()
		{
			if (_content) _content.SetActive(false);
		}

		protected override void OnBind()
		{
			if (LocalPlayer.ItemInventory) LocalPlayer.ItemInventory.OnCountChanged += HandleCountChanged;

			// Whatever is already held is shown as it stands: only cards arriving from now on are counted in.
			_shownCount = HeldCount;
			Refresh();
		}

		protected override void OnUnbind()
		{
			if (LocalPlayer.ItemInventory) LocalPlayer.ItemInventory.OnCountChanged -= HandleCountChanged;

			_gainTween?.Kill();
			_gainTween = null;

			if (_content) _content.SetActive(false);
		}

		private int HeldCount => LocalPlayer && LocalPlayer.ItemInventory ? LocalPlayer.ItemInventory.Count.Value : 0;

		private void HandleCountChanged()
		{
			if (HeldCount < _shownCount)
			{
				_gainTween?.Kill();
				_gainTween = null;
				_shownCount = HeldCount;
				Refresh();
				return;
			}

			if (_gainTween == null) CountInNextGain();
		}

		private void CountInNextGain()
		{
			if (_shownCount >= HeldCount)
			{
				_gainTween = null;
				return;
			}

			_shownCount++;
			Refresh();
			ShowGain();

			_gainTween = DOVirtual.DelayedCall(_gainInterval, CountInNextGain, true).SetLink(gameObject);
		}

		private void ShowGain()
		{
			if (!isActiveAndEnabled) return;

			if (_icon) _icon.Punch(_iconPunch, _iconPunchDuration, 6, 0.5f);
			if (!_gainPopupPrefab || !_icon) return;

			var icon = (RectTransform)_icon.transform;
			var popup = Instantiate(_gainPopupPrefab, icon.parent, false);
			var start = (Vector2)icon.localPosition + _gainPopupOffset;
			popup.localPosition = start;
			popup.localScale = Vector3.zero;

			var group = popup.GetComponent<CanvasGroup>();
			var fadeAt = _gainPopupDuration * 0.5f;

			var sequence = DOTween.Sequence()
				.Join(popup.DOScale(1f, 0.15f).SetEase(Ease.OutBack))
				.Join(popup.DOLocalMoveY(start.y + _gainPopupRise, _gainPopupDuration).SetEase(_gainPopupEase))
				.SetUpdate(true)
				.SetLink(popup.gameObject)
				.OnComplete(() => Destroy(popup.gameObject));

			if (group) sequence.Insert(fadeAt, DOTween.To(() => group.alpha, alpha => group.alpha = alpha, 0f, _gainPopupDuration - fadeAt));
		}

		private void Refresh()
		{
			var inventory = LocalPlayer ? LocalPlayer.ItemInventory : null;
			var shown = inventory && GameMode.FindModule<PokerItemModule>();

			if (_content) _content.SetActive(shown);
			if (shown && _countLabel) _countLabel.text = Localizer.Format(LocalizationKeys.Poker.Button.Items, _shownCount);
		}
	}
}
