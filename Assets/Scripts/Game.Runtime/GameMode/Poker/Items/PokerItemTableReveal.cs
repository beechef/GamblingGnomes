using Game.Runtime.GameMode.Poker.Player;
using Localization;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Every player still in the hand, the user included, shows one hidden card drawn at random to the whole
	// table for a number of streets, over their heads (the known-cards row); the cards stay in the hand.
	[CreateAssetMenu(fileName = "PokerItem_TableReveal", menuName = "Game/Poker/Items/Table Reveal")]
	public class PokerItemTableReveal : PokerItem
	{
		[Header("Reveal")]
		[Tooltip("Streets the cards stay shown, starting with the one the item is played on.")]
		[MinValue(1)]
		[SerializeField] private int _shownStreets = 1;

		protected override PokerItemAvailability OnGetAvailability(in PokerItemContext context)
		{
			foreach (var player in context.GameMode.SeatedPlayers)
			{
				if (HasHiddenCard(player)) return PokerItemAvailability.Usable;
			}

			return PokerItemAvailability.Dimmed(Localizer.Get(LocalizationKeys.Item.Reason.AllFaceUp));
		}

		protected override void OnUseServer(in PokerItemContext context, in PokerItemUseRequest request)
		{
			foreach (var player in context.GameMode.SeatedPlayers)
			{
				if (!HasHiddenCard(player)) continue;

				var slot = PokerItemModule.PickRandomSlot(player.Data.CardCount, s => IsHidden(player, s));
				if (slot >= 0) context.Module.ServerShowCard(player, slot, _shownStreets);
			}
		}

		private static bool HasHiddenCard(PokerPlayer player)
		{
			if (!player || !player.Data || !player.Data.IsInHand) return false;

			for (var slot = 0; slot < player.Data.CardCount; slot++)
			{
				if (IsHidden(player, slot)) return true;
			}

			return false;
		}

		private static bool IsHidden(PokerPlayer player, int slot) => !player.Data.IsHoleCardShown(slot);
	}
}
