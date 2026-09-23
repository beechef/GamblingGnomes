using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// How every roll against somebody's life plays out, whatever set it off: a Colorful cap or an item. Read by
	// PokerHallucinationRollController, which sends the numbers to every client with the roll, so the skull on
	// every bar and the server paying the result run on the one clock.
	[CreateAssetMenu(fileName = "PokerRollPacing", menuName = "Game/Poker/Roll Pacing")]
	public class PokerRollPacing : ScriptableObject
	{
		[Tooltip("Seconds between whatever set the roll off and the skull starting to move, so the bar has filled to the rate being rolled against. When a rung was crossed on the way, the blink is waited out instead.")]
		[field: SerializeField, Min(0f)] public float LeadIn { get; private set; } = 0.6f;

		[Tooltip("Seconds the skull spends sweeping before it stops on the number.")]
		[field: SerializeField, Min(0.1f)] public float SweepDuration { get; private set; } = 3.5f;

		[Tooltip("Seconds the stopped skull holds on the result before anything follows: the death on a fatal roll, the next beat on a survived one. Feedback on the result plays inside it.")]
		[field: SerializeField, Min(0f)] public float ResultHold { get; private set; } = 2f;
	}
}
