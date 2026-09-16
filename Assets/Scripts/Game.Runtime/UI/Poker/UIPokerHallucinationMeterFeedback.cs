using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Poker
{
	// What the meter does when something happens to it — placeholder until art decides. Kept out of the
	// meter on purpose: the meter says *that* a rung was crossed or a roll went under, and this says what
	// that looks like, so swapping the placeholder for real VFX or sound is a change to this component and
	// nothing else.
	//
	// Every effect moves the art inside the bar, never the rect the layout group places, so a shake cannot
	// fight the layout and a punch always comes back to where it started.
	public class UIPokerHallucinationMeterFeedback : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private UIPokerHallucinationMeter _meter;

		[Tooltip("The bar's art, under the rect the layout places. What punches and shakes.")]
		[SerializeField] private RectTransform _visual;

		[Tooltip("An overlay over the fill, clear at rest, flashed for each event.")]
		[SerializeField] private Image _flash;

		[Header("Rung crossed")]
		[SerializeField] private Color _climbColor = new(1f, 0.85f, 0.3f, 0.85f);
		[SerializeField] private Color _dropColor = new(0.4f, 0.9f, 1f, 0.6f);

		[Min(0f)]
		[SerializeField] private float _rungFlashDuration = 0.5f;

		[SerializeField] private Vector3 _rungPunch = new(0.08f, 0.25f, 0f);

		[Min(0f)]
		[SerializeField] private float _rungPunchDuration = 0.4f;

		[Tooltip("How much the crossed rung's own mark swells.")]
		[SerializeField] private Vector3 _markPunch = new(1.2f, 0.6f, 0f);

		[Header("Roll went under")]
		[SerializeField] private Color _deathColor = new(1f, 0.1f, 0.15f, 0.9f);

		[Min(0f)]
		[SerializeField] private float _deathFlashDuration = 0.9f;

		[Tooltip("Local units the bar is shaken by.")]
		[SerializeField] private float _deathShakeStrength = 14f;

		[Min(0f)]
		[SerializeField] private float _deathShakeDuration = 0.6f;

		[SerializeField] private Vector3 _knotPunch = new(0.6f, 0.6f, 0f);

		private Vector3 _visualRestPosition;
		private Vector3 _visualRestScale;
		private Tween _flashTween;
		private Tween _visualTween;

		private void Awake()
		{
			if (!_meter) _meter = GetComponent<UIPokerHallucinationMeter>();

			if (_visual)
			{
				_visualRestPosition = _visual.localPosition;
				_visualRestScale = _visual.localScale;
			}

			SetFlashAlpha(0f);
		}

		private void OnEnable()
		{
			if (!_meter) return;

			_meter.OnRungCrossed += HandleRungCrossed;
			_meter.OnRollSettled += HandleRollSettled;
		}

		private void OnDisable()
		{
			if (_meter)
			{
				_meter.OnRollSettled -= HandleRollSettled;
				_meter.OnRungCrossed -= HandleRungCrossed;
			}

			ResetVisual();
			_flashTween?.Kill();
			SetFlashAlpha(0f);
		}

		private void HandleRungCrossed(int rungIndex, bool climbed)
		{
			Flash(climbed ? _climbColor : _dropColor, _rungFlashDuration);

			if (!climbed) return;

			ResetVisual();
			if (_visual) _visualTween = _visual.DOPunchScale(_rungPunch, _rungPunchDuration, 8, 0.6f).SetLink(gameObject);

			var mark = _meter ? _meter.RungMark(rungIndex) : null;
			if (!mark) return;

			mark.DOKill(true);
			mark.localScale = Vector3.one;
			mark.DOPunchScale(_markPunch, _rungPunchDuration, 6, 0.5f).SetLink(mark.gameObject);
		}

		// Only a death has feedback for now; surviving is the absence of one until art says otherwise.
		private void HandleRollSettled(int roll, bool fatal)
		{
			if (!fatal) return;

			Flash(_deathColor, _deathFlashDuration);

			ResetVisual();
			if (_visual)
			{
				_visualTween = DOTween.Shake(() => _visual.localPosition, value => _visual.localPosition = value,
						_deathShakeDuration, new Vector3(_deathShakeStrength, _deathShakeStrength * 0.5f, 0f), 30)
					.OnComplete(ResetVisual)
					.SetLink(gameObject);
			}

			var knot = _meter ? _meter.Knot : null;
			if (!knot) return;

			knot.DOKill(true);
			knot.localScale = Vector3.one;
			knot.DOPunchScale(_knotPunch, _deathShakeDuration, 6, 0.5f).SetLink(knot.gameObject);
		}

		private void Flash(Color color, float duration)
		{
			if (!_flash) return;

			_flashTween?.Kill();
			_flash.color = color;

			if (duration <= 0f)
			{
				SetFlashAlpha(0f);
				return;
			}

			_flashTween = DOTween.To(() => _flash.color.a, SetFlashAlpha, 0f, duration)
				.SetEase(Ease.OutQuad)
				.SetLink(gameObject);
		}

		private void SetFlashAlpha(float alpha)
		{
			if (!_flash) return;

			var color = _flash.color;
			color.a = alpha;
			_flash.color = color;
		}

		private void ResetVisual()
		{
			_visualTween?.Kill();
			_visualTween = null;

			if (!_visual) return;

			_visual.localPosition = _visualRestPosition;
			_visual.localScale = _visualRestScale;
		}
	}
}
