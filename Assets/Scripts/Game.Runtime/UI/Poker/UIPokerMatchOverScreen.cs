using DG.Tweening;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The end of a match: who outlasted it, then the whole screen blinking shut while the table is put back.
	// Both halves read replicated state — the phase and the survivor for the announcement, MatchResetting for
	// the blink — so a late joiner lands in whichever of them is up rather than waiting for a change.
	//
	// Neither half takes the pointer: nothing is being asked of anybody here.
	public class UIPokerMatchOverScreen : UIPokerView
	{
		[Header("Announcement")]
		[SerializeField] private CanvasGroup _announcement;
		[SerializeField] private TextMeshProUGUI _titleLabel;
		[SerializeField] private TextMeshProUGUI _nameLabel;
		[SerializeField] private string _survivorTitle = "LAST GNOME STANDING";
		[SerializeField] private string _nobodyTitle = "NOBODY MADE IT";
		[SerializeField] private float _announceFadeDuration = 0.4f;
		[SerializeField] private Ease _announceEase = Ease.OutQuad;

		[Header("Blink")]
		[Tooltip("Faded to opaque while the table is put back. Placeholder for whatever eyelids the art lands on.")]
		[SerializeField] private CanvasGroup _blinkScreen;
		[SerializeField] private Ease _closeEase = Ease.InQuad;
		[SerializeField] private Ease _openEase = Ease.OutQuad;

		private Tween _announceTween;
		private Tween _blinkTween;

		// Resolved when the eye starts to close: by the time it opens again the table has already moved on to
		// the idle stage, and the running stage is no longer the one that knows how long opening takes.
		private PokerMatchOverStage _stage;

		private void Awake()
		{
			SetAlpha(_announcement, 0f);
			SetAlpha(_blinkScreen, 0f);
		}

		private void OnDestroy()
		{
			_announceTween?.Kill();
			_blinkTween?.Kill();
		}

		protected override void OnBind()
		{
			Data.Phase.OnValueChanged += HandlePhaseChanged;
			Data.SurvivorClientId.OnValueChanged += HandleSurvivorChanged;
			Data.MatchResetting.OnValueChanged += HandleResettingChanged;

			ShowAnnouncement(Data.Phase.Value == PokerPhase.MatchOver, animate: false);
			SetBlink(Data.MatchResetting.Value, animate: false);
		}

		protected override void OnUnbind()
		{
			Data.Phase.OnValueChanged -= HandlePhaseChanged;
			Data.SurvivorClientId.OnValueChanged -= HandleSurvivorChanged;
			Data.MatchResetting.OnValueChanged -= HandleResettingChanged;

			_announceTween?.Kill();
			_blinkTween?.Kill();
			_stage = null;

			SetAlpha(_announcement, 0f);
			SetAlpha(_blinkScreen, 0f);
		}

		// Leaving the phase happens behind the shut eye, so the announcement goes at once rather than fading.
		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) =>
			ShowAnnouncement(current == PokerPhase.MatchOver, animate: current == PokerPhase.MatchOver);

		private void HandleSurvivorChanged(ulong previous, ulong current)
		{
			if (Data.Phase.Value == PokerPhase.MatchOver) FillAnnouncement();
		}

		private void HandleResettingChanged(bool previous, bool current) => SetBlink(current, animate: true);

		private void ShowAnnouncement(bool show, bool animate)
		{
			if (!_announcement) return;

			if (show) FillAnnouncement();

			_announceTween?.Kill();

			var target = show ? 1f : 0f;

			if (!animate || _announceFadeDuration <= 0f)
			{
				_announcement.alpha = target;
				return;
			}

			_announceTween = DOTween.To(() => _announcement.alpha, alpha => _announcement.alpha = alpha, target, _announceFadeDuration)
				.SetEase(_announceEase)
				.SetUpdate(true)
				.SetTarget(_announcement);
		}

		private void FillAnnouncement()
		{
			var survivor = PokerPlayer.Find(Data.SurvivorClientId.Value);

			if (_titleLabel) _titleLabel.text = survivor ? _survivorTitle : _nobodyTitle;
			if (_nameLabel) _nameLabel.text = survivor ? survivor.DisplayName : string.Empty;
		}

		private void SetBlink(bool shut, bool animate)
		{
			if (!_blinkScreen) return;

			if (shut) _stage = GameMode.FindStage(Data.StageId.Value.ToString()) as PokerMatchOverStage;

			var duration = _stage ? (shut ? _stage.BlinkCloseDuration : _stage.BlinkOpenDuration) : 0f;
			var target = shut ? 1f : 0f;

			_blinkTween?.Kill();

			if (!animate || duration <= 0f)
			{
				_blinkScreen.alpha = target;
				return;
			}

			// Driven by value rather than DOFade: DOTween's UI module is not in this project.
			_blinkTween = DOTween.To(() => _blinkScreen.alpha, alpha => _blinkScreen.alpha = alpha, target, duration)
				.SetEase(shut ? _closeEase : _openEase)
				.SetUpdate(true)
				.SetTarget(_blinkScreen);
		}

		private static void SetAlpha(CanvasGroup group, float alpha)
		{
			if (!group) return;

			group.alpha = alpha;
			group.blocksRaycasts = false;
			group.interactable = false;
		}
	}
}
