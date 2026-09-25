using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// How long two cards changing places take, read by the server, which writes the swap once they have
	// landed, and by every screen, which flies them. One number, so the data never lands mid-flight.
	[CreateAssetMenu(fileName = "PokerCardExchangePacing", menuName = "Game/Poker/Items/Card Exchange Pacing")]
	public class PokerCardExchangePacing : ScriptableObject
	{
		[Tooltip("Seconds each card spends in the air.")]
		[field: SerializeField, Min(0.1f)] public float FlightDuration { get; private set; } = 0.7f;

		[Tooltip("How high the two cards arc, so they pass over rather than through each other.")]
		[field: SerializeField, Min(0f)] public float Arc { get; private set; } = 0.18f;

		[field: SerializeField] public Ease Ease { get; private set; } = Ease.InOutCubic;

		[Tooltip("Seconds a landed card stays hidden waiting for its new face to arrive before it is shown anyway.")]
		[field: SerializeField, Min(0f)] public float LandingBackstop { get; private set; } = 1f;
	}
}
