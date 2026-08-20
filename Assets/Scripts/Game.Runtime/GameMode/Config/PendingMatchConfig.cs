using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Config
{
	// What the host chose on the room screen, carried across the scene load. The mode's MatchConfigData
	// consumes it once while seeding its replicated list and clears it, so one lobby's choices can never
	// leak into a later session.
	public static class PendingMatchConfig
	{
		private static readonly Dictionary<string, float> Values = new();

		public static void Set(string id, float value) => Values[id] = value;

		public static bool TryGet(string id, out float value) => Values.TryGetValue(id, out value);

		public static void Clear() => Values.Clear();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Values.Clear();
	}
}
