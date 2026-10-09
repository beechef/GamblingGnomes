using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.UI
{
	// Whether something is covering the whole screen right now. A 3D model on the UI stands in front of the canvas
	// plane, so no image can draw over it whatever its place in the hierarchy; a full-screen cover says it is up
	// here instead, and the models step aside while it is (UIModelView).
	public static class UIScreenCover
	{
		private static readonly HashSet<object> Covers = new();

		public static bool IsCovered => Covers.Count > 0;

		public static event Action OnChanged;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Covers.Clear();
			OnChanged = null;
		}

		public static void Set(object cover, bool covering)
		{
			var changed = covering ? Covers.Add(cover) : Covers.Remove(cover);
			if (changed) OnChanged?.Invoke();
		}
	}
}
