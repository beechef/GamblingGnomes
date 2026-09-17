using Game.Runtime.GameMode.Poker;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// How far gone this player is, 0 to 100. The one number left on the HUD now that the round is played
	// for neither money nor blood: reaching the ceiling is elimination, so it is the only thing a player
	// is really watching about themselves.
	//
	// Their own, not the table's. Everybody else's is drawn over their head by the same meter
	// (PokerHallucinationTagVisual), which is where you read the room. This only decides when the local
	// player's meter is up and hands it the player; the meter draws.
	public class UIPokerHallucinationBar : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Meter")]
		[SerializeField] private UIPokerHallucinationMeter _meter;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			LocalData.OnStateChanged += Refresh;
			Data.Phase.OnValueChanged += HandlePhaseChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (Data) Data.Phase.OnValueChanged -= HandlePhaseChanged;
			if (LocalData) LocalData.OnStateChanged -= Refresh;

			if (_meter) _meter.Unbind();
			if (_panel) _panel.SetActive(false);
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();

		// Down while the table waits for the host to start: nothing has been eaten and nothing can be, so the
		// meter would only be an empty bar under the start button. Heads keep theirs — that is another view.
		private void Refresh()
		{
			var show = LocalData && LocalData.IsSeated && Data && Data.Phase.Value != PokerPhase.Waiting;

			if (_panel && _panel.activeSelf != show) _panel.SetActive(show);
			if (!_meter) return;

			if (show) _meter.Bind(LocalPlayer);
			else _meter.Unbind();
		}
	}
}
