using Game.Runtime.GameMode.Poker;
using Game.Runtime.UI.Motion;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Which betting round of the hand is running, read off the replicated street number, and popped as each
	// street opens. The whole round panel is up only while a hand is being played.
	public class UIPokerRoundCounter : UIPokerView
	{
		[Tooltip("Faded out outside a hand. A CanvasGroup rather than SetActive, so this keeps listening while hidden.")]
		[SerializeField] private CanvasGroup _content;

		[SerializeField] private TMP_Text _numberLabel;

		[Tooltip("Optional. Punched as each street opens.")]
		[SerializeField] private UIPunchScaleMotion _pop;

		[SerializeField] private string _numberFormat = "00";

		private void Awake() => SetShown(false);

		protected override void OnBind()
		{
			Data.StreetNumber.OnValueChanged += HandleStreetNumberChanged;
			Data.Phase.OnValueChanged += HandlePhaseChanged;

			SetShown(IsHandPhase(Data.Phase.Value));
			DrawNumber(Data.StreetNumber.Value);
		}

		protected override void OnUnbind()
		{
			Data.Phase.OnValueChanged -= HandlePhaseChanged;
			Data.StreetNumber.OnValueChanged -= HandleStreetNumberChanged;
			SetShown(false);
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => SetShown(IsHandPhase(current));

		private void HandleStreetNumberChanged(int previous, int current)
		{
			DrawNumber(current);
			if (current > 0 && _pop) _pop.Play();
		}

		// From the deal to the first street the round about to be played is shown, so the number never reads 00.
		private void DrawNumber(int streetNumber)
		{
			if (_numberLabel) _numberLabel.text = Mathf.Max(1, streetNumber).ToString(_numberFormat);
		}

		private static bool IsHandPhase(PokerPhase phase) =>
			phase != PokerPhase.Waiting && phase != PokerPhase.Finished && phase != PokerPhase.MatchOver;

		private void SetShown(bool shown)
		{
			if (!_content) return;

			_content.alpha = shown ? 1f : 0f;
			_content.blocksRaycasts = false;
			_content.interactable = false;
		}
	}
}
