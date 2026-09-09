using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// Every cap standing on the table right now, wherever it was put down. A cap is spawned at runtime, so
	// nothing can serialize a reference to it and whoever wants to draw on one has to be told it exists.
	//
	// One list rather than one per visual: the pot and each player's plate both put caps out, and anything
	// asking "what mushrooms can I see" means all of them. Splitting it is how a hallucination came to
	// change the caps somebody staked and leave the ones they were served untouched.
	public static class PokerItemCapRegistry
	{
		private static readonly List<GameObject> Registered = new();

		public static IReadOnlyList<GameObject> All => Registered;

		public static event Action OnChanged;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Registered.Clear();
			OnChanged = null;
		}

		public static void Add(GameObject cap)
		{
			if (!cap) return;

			Registered.Add(cap);
			OnChanged?.Invoke();
		}

		// `is null` rather than Unity's own null check: a cap that has already been destroyed is still the
		// reference sitting in the list, and refusing to take it out would leave it there forever.
		public static void Remove(GameObject cap)
		{
			if (cap is null || !Registered.Remove(cap)) return;

			OnChanged?.Invoke();
		}
	}
}
