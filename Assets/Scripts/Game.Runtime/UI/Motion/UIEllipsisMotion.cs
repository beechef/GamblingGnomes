using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Motion
{
	// Runs dots after a label ("LOADING", "LOADING.", "LOADING..", ...) for as long as it is on. The dots are
	// always in the text and only revealed, so a centred label never shifts as they come and go.
	[RequireComponent(typeof(TMP_Text))]
	public class UIEllipsisMotion : MonoBehaviour
	{
		[Min(1)]
		[SerializeField] private int _dotCount = 3;

		[Tooltip("Seconds between one dot appearing and the next; the full row holds as long before starting over.")]
		[Min(0.05f)]
		[SerializeField] private float _stepDuration = 0.4f;

		private TMP_Text _label;
		private int _textLength;
		private Tween _dots;

		// Looked up on first use: an owner may set the text while this object is still switched off and unawoken.
		private TMP_Text Label => _label ? _label : _label = GetComponent<TMP_Text>();

		private void Awake() => SetText(Label.text);

		public void SetText(string text)
		{
			text = text.TrimEnd('.', '…', ' ');
			_textLength = text.Length;
			Label.text = text + new string('.', _dotCount);
			ShowDots(0);
		}

		private void OnEnable()
		{
			var steps = _dotCount + 1;
			_dots = DOVirtual.Float(0f, steps, steps * _stepDuration, value => ShowDots(Mathf.Min(_dotCount, Mathf.FloorToInt(value))))
				.SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true);
		}

		private void OnDisable() => _dots?.Kill();

		private void ShowDots(int count) => Label.maxVisibleCharacters = _textLength + count;
	}
}
