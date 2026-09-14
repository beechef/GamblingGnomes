using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Progress;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// How far gone this player is, 0 to 100. The one number left on the HUD now that the round is played
	// for neither money nor blood: reaching the ceiling is elimination, so it is the only thing a player
	// is really watching about themselves.
	//
	// Their own, not the table's. Everybody's rate is public — PokerHallucinationTagVisual writes it over
	// each head, which is where you read the room — and a HUD listing four bars would be a second place
	// the same numbers live, kept in step by hand.
	//
	// Placeholder art: the project's own progress bar, tinted by which rung the rate has climbed to.
	public class UIPokerHallucinationBar : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("Bar")]
		[SerializeField] private UIProgressBar _bar;

		[Tooltip("Colour at each rung, in ascending order — the fill takes the last one the rate has reached. Empty leaves the bar whatever the prefab was authored with.")]
		[SerializeField] private HallucinationTint[] _tints =
		{
			new() { Threshold = 0, Colour = new Color(0.45f, 0.75f, 0.45f) },
			new() { Threshold = 30, Colour = new Color(0.85f, 0.78f, 0.35f) },
			new() { Threshold = 60, Colour = new Color(0.9f, 0.55f, 0.25f) },
			new() { Threshold = 85, Colour = new Color(0.85f, 0.3f, 0.35f) }
		};

		[System.Serializable]
		public struct HallucinationTint
		{
			[Tooltip("Rate at or above which this colour is used.")]
			public int Threshold;

			public Color Colour;
		}

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			LocalData.OnHallucinationChanged += HandleHallucinationChanged;

			// Whatever it already stands at. A bar that only listens for changes shows nothing until the
			// next one, and for a player who joined mid-match that is a lie about how far gone they are.
			Refresh();
		}

		protected override void OnUnbind()
		{
			if (LocalData) LocalData.OnHallucinationChanged -= HandleHallucinationChanged;

			if (_panel) _panel.SetActive(false);
		}

		private void HandleHallucinationChanged(int previous, int current) => Refresh();

		private void Refresh()
		{
			var show = LocalData && LocalData.IsSeated;

			if (_panel && _panel.activeSelf != show) _panel.SetActive(show);
			if (!show || !_bar) return;

			var rate = LocalData.HallucinationRate.Value;

			_bar.SetProgress(rate / (float)PokerPlayerData.MaxHallucination);
			_bar.SetFillColor(TintFor(rate));
		}

		private Color TintFor(int rate)
		{
			var colour = Color.white;

			foreach (var tint in _tints)
			{
				if (rate >= tint.Threshold) colour = tint.Colour;
			}

			return colour;
		}
	}
}
