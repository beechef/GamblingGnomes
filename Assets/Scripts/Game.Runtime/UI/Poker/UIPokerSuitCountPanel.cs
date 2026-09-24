using Game.Runtime.GameMode.Poker;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// What the Suit Count item told this player: the cards of each suit still in the undealt deck when they
	// counted. Up from the moment they learn it until the hand is put away, read straight off their own
	// item knowledge, which only they receive.
	public class UIPokerSuitCountPanel : UIPokerView
	{
		[Tooltip("Shown while a count is known. A child, so this view keeps listening while it is hidden.")]
		[SerializeField] private GameObject _content;

		[Header("Labels")]
		[SerializeField] private TMP_Text _clubsLabel;
		[SerializeField] private TMP_Text _diamondsLabel;
		[SerializeField] private TMP_Text _heartsLabel;
		[SerializeField] private TMP_Text _spadesLabel;

		private void Awake()
		{
			if (_content) _content.SetActive(false);
		}

		protected override void OnBind()
		{
			if (LocalPlayer.ItemKnowledge) LocalPlayer.ItemKnowledge.OnKnowledgeChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (LocalPlayer.ItemKnowledge) LocalPlayer.ItemKnowledge.OnKnowledgeChanged -= Refresh;

			if (_content) _content.SetActive(false);
		}

		private void Refresh()
		{
			var knowledge = LocalPlayer.ItemKnowledge;
			var counts = knowledge ? knowledge.SuitCounts.Value : default;

			if (_content) _content.SetActive(counts.IsKnown);
			if (!counts.IsKnown) return;

			SetLabel(_clubsLabel, counts.Get(CardSuit.Clubs));
			SetLabel(_diamondsLabel, counts.Get(CardSuit.Diamonds));
			SetLabel(_heartsLabel, counts.Get(CardSuit.Hearts));
			SetLabel(_spadesLabel, counts.Get(CardSuit.Spades));
		}

		private static void SetLabel(TMP_Text label, int count)
		{
			if (label) label.text = count.ToString();
		}
	}
}
