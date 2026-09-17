using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Puts the HUD in one of its two states. While the showdown board is up everything the hand was played
	// with steps aside, so the ranking is the only thing on screen; when the board comes down it all comes
	// back. The state is read off the same thing the board reads — the table's showdown list — so the two can
	// never disagree about whether a ranking is up.
	//
	// The panels are faded through one CanvasGroup over all of them and never switched off: a switched-off
	// view unbinds, and the bars that decide their own visibility have to go on hearing the turn while hidden.
	public class UIPokerHudStateController : UIPokerView
	{
		[Header("Playing")]
		[Required]
		[Tooltip("Over every panel of the playing HUD — everything except the ranking board.")]
		[SerializeField] private CanvasGroup _playing;

		[Header("Transition")]
		[MinValue(0f)]
		[SerializeField] private float _fadeDuration = 0.3f;

		[SerializeField] private Ease _fadeEase = Ease.OutQuad;

		private Tween _fade;

		public UIPokerHudState State { get; private set; } = UIPokerHudState.Playing;

		public event Action<UIPokerHudState> OnStateChanged;

		protected override void OnBind()
		{
			Data.OnShowdownChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.OnShowdownChanged -= Refresh;

			SetState(UIPokerHudState.Playing, true);
		}

		private void OnDestroy() => _fade?.Kill();

		private void Refresh() => SetState(Data.Showdown.Count > 0 ? UIPokerHudState.Ranking : UIPokerHudState.Playing, false);

		private void SetState(UIPokerHudState state, bool instant)
		{
			if (State == state && !instant) return;

			State = state;

			var playing = state == UIPokerHudState.Playing;

			if (_playing)
			{
				// Nothing under a faded HUD can be clicked, and nothing coming back is clickable before it shows.
				_playing.blocksRaycasts = playing;
				_playing.interactable = playing;

				_fade?.Kill();

				if (instant || _fadeDuration <= 0f) _playing.alpha = playing ? 1f : 0f;
				else
				{
					_fade = DOTween.To(() => _playing.alpha, value => _playing.alpha = value, playing ? 1f : 0f, _fadeDuration)
						.SetEase(_fadeEase)
						.SetLink(gameObject);
				}
			}

			OnStateChanged?.Invoke(state);
		}
	}
}
