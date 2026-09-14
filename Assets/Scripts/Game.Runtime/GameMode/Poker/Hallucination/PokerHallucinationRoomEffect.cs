using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// Puts the table in a different room. It names which and nothing else: where that room is, what is in
	// it and how long it takes to arrive all belong to PokerRoomController, which lives in the scene beside
	// the rooms themselves.
	//
	// No target, unlike every other effect here. There is one room and the scene is the only thing that can
	// hold it — a target resolving transforms would be answering a question that has one answer.
	[CreateAssetMenu(fileName = "Hallucination_Room", menuName = "Game/Poker/Hallucination/Room")]
	public class PokerHallucinationRoomEffect : PokerHallucinationEffect
	{
		[Tooltip("Which room the table is put in while this is running. A room the scene does not carry leaves it where it is.")]
		[SerializeField] private PokerRoomVariant _room = PokerRoomVariant.Water;

		public PokerRoomVariant Room => _room;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationRoomBehaviour>();
	}
}
