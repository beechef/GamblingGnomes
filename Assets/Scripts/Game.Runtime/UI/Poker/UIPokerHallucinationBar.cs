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

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (LocalData) LocalData.OnStateChanged -= Refresh;

			if (_meter) _meter.Unbind();
			if (_panel) _panel.SetActive(false);
		}

		private void Refresh()
		{
			var show = LocalData && LocalData.IsSeated;

			if (_panel && _panel.activeSelf != show) _panel.SetActive(show);
			if (!_meter) return;

			if (show) _meter.Bind(LocalPlayer);
			else _meter.Unbind();
		}
	}
}
