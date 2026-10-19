using Game.Runtime.UI.Progress;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The turn's seconds in the top-left corner, up while somebody is on a clock. The warning sound is only
	// for the player whose turn is running out.
	public class UIPokerTurnClock : UIPokerView
	{
		[Tooltip("Switched on while a turn is on the clock.")]
		[SerializeField] private GameObject _content;

		[SerializeField] private UICountdownBadge _badge;

		protected override bool WantsTick => IsShown;

		private bool IsShown => Data.HasTurn && Data.HasTurnClock;

		private void Awake()
		{
			if (_content) _content.SetActive(false);
		}

		protected override void OnBind()
		{
			Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			Data.TurnDuration.OnValueChanged += HandleDurationChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.TurnDuration.OnValueChanged -= HandleDurationChanged;
			Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;

			if (_content) _content.SetActive(false);
		}

		private void HandleTurnChanged(ulong previous, ulong current) => Refresh();
		private void HandleDurationChanged(float previous, float current) => Refresh();

		private void Refresh()
		{
			var shown = IsShown;
			if (_content && _content.activeSelf != shown) _content.SetActive(shown);

			if (shown) OnTick();
		}

		protected override void OnTick()
		{
			if (_badge) _badge.SetTime(Data.TurnRemaining, Data.CurrentTurnClientId.Value == LocalClientId);
		}
	}
}
