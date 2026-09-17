using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// How one place on the showdown board arrives. The board decides when each place goes; this says what
	// arriving looks like for its own face, so the winner and a row can differ without the board knowing how.
	//
	// Three steps, each used only when its references are set, in this order: what pops (the crown and the
	// name) swells out of nothing; the fan opens from the left; what slides rises from below. Everything is
	// tweened on objects no layout group places — a pop is a scale, which no layout drives; a slide moves a
	// body stretched inside the entry, not the entry the board's layout owns.
	public class UIPokerRankingEntryVisual : MonoBehaviour
	{
		[Header("Pop")]
		[Tooltip("Swell out of nothing together, first. The crown and the name, on the winner.")]
		[SerializeField] private RectTransform[] _pop = new RectTransform[0];

		[MinValue(0f)]
		[SerializeField] private float _popDuration = 0.4f;

		[SerializeField] private Ease _popEase = Ease.OutBack;

		[Header("Fan")]
		[Tooltip("Opened from the left after the pop.")]
		[SerializeField] private UIFanLayoutGroup _fan;

		[Tooltip("Faded in as the fan starts opening, so the stacked cards are not sitting there during the pop.")]
		[SerializeField] private CanvasGroup _fanGroup;

		[MinValue(0f)]
		[SerializeField] private float _fanDuration = 0.6f;

		[SerializeField] private Ease _fanEase = Ease.OutCubic;

		[Header("Slide")]
		[Tooltip("Rises from below into place. A child stretched over the entry, so the board's layout never fights it.")]
		[SerializeField] private RectTransform _slide;

		[SerializeField] private CanvasGroup _slideGroup;

		[SerializeField] private float _slideDistance = 80f;

		[MinValue(0f)]
		[SerializeField] private float _slideDuration = 0.35f;

		[SerializeField] private Ease _slideEase = Ease.OutCubic;

		private Vector3[] _popRest;
		private Vector2 _slideRest;

		private void Awake()
		{
			_popRest = new Vector3[_pop.Length];
			for (var i = 0; i < _pop.Length; i++)
				if (_pop[i]) _popRest[i] = _pop[i].localScale;

			if (_slide) _slideRest = _slide.anchoredPosition;
		}

		// Put in the pose the reveal starts from, so nothing is seen before its turn.
		public void Conceal()
		{
			for (var i = 0; i < _pop.Length; i++)
				if (_pop[i]) _pop[i].localScale = Vector3.zero;

			if (_fan) _fan.Openness = 0f;
			if (_fanGroup) _fanGroup.alpha = 0f;

			if (_slide) _slide.anchoredPosition = _slideRest - new Vector2(0f, _slideDistance);
			if (_slideGroup) _slideGroup.alpha = 0f;
		}

		// Snapped to rest, for a place that arrives after the reveal has already played.
		public void ShowAtRest()
		{
			for (var i = 0; i < _pop.Length; i++)
				if (_pop[i]) _pop[i].localScale = _popRest[i];

			if (_fan) _fan.Openness = 1f;
			if (_fanGroup) _fanGroup.alpha = 1f;

			if (_slide) _slide.anchoredPosition = _slideRest;
			if (_slideGroup) _slideGroup.alpha = 1f;
		}

		// The whole arrival as one sequence the board can place on its own timeline. Not linked here: the
		// board owns the sequence it is inserted into and kills that.
		public Sequence Reveal()
		{
			var sequence = DOTween.Sequence();

			for (var i = 0; i < _pop.Length; i++)
			{
				if (_pop[i]) sequence.Insert(0f, _pop[i].DOScale(_popRest[i], _popDuration).SetEase(_popEase));
			}

			if (_fan)
			{
				var at = sequence.Duration();
				if (_fanGroup) sequence.Insert(at, DOTween.To(() => _fanGroup.alpha, value => _fanGroup.alpha = value, 1f, _fanDuration * 0.25f));
				sequence.Insert(at, DOTween.To(() => _fan.Openness, value => _fan.Openness = value, 1f, _fanDuration).SetEase(_fanEase));
			}

			if (_slide)
			{
				var at = sequence.Duration();
				sequence.Insert(at, DOTween.To(() => _slide.anchoredPosition, value => _slide.anchoredPosition = value, _slideRest, _slideDuration).SetEase(_slideEase));
				if (_slideGroup) sequence.Insert(at, DOTween.To(() => _slideGroup.alpha, value => _slideGroup.alpha = value, 1f, _slideDuration));
			}

			return sequence;
		}
	}
}
