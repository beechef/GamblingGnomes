using System.Collections.Generic;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Switches pieces of every body the target names off — a hat taken away so something else can sit where
	// it was. Usually one half of a composite whose other half puts that something on.
	[CreateAssetMenu(fileName = "Hallucination_HideSlots", menuName = "Game/Poker/Hallucination/Hide Slots")]
	public class PokerHallucinationHideSlotsEffect : PokerHallucinationAppearanceEffect
	{
		[SerializeField] private List<PlayerSlot> _slots = new() { PlayerSlot.Hat };

		public IReadOnlyList<PlayerSlot> Slots => _slots;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationHideSlotsBehaviour>();
	}
}
