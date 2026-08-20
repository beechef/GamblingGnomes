using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.UI
{
	// Escape means "close the newest thing that is open", and the pause menu is only what it falls through
	// to when nothing else is. Binding a second action to the same key instead would not express that: both
	// would fire, so a ranking board dismissed with Escape would pull the pause menu up behind it.
	//
	// One key, one handler, and whatever is on screen says whether it wants the press. Anything dismissible
	// pushes while it is up and takes itself off when it goes — paired like every other binding here, so a
	// panel destroyed mid-frame cannot leave a handler pointing at nothing.
	public static class UIEscapeStack
	{
		private static readonly List<Action> Handlers = new();

		public static bool Any => Handlers.Count > 0;

		// Domain Reload is disabled, so this would otherwise still hold last session's panels.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics() => Handlers.Clear();

		// Re-pushing something already on the stack moves it to the top rather than listing it twice: it
		// has just been raised, so it is what the next Escape should reach.
		public static void Push(Action dismiss)
		{
			if (dismiss == null) return;

			Handlers.Remove(dismiss);
			Handlers.Add(dismiss);
		}

		public static void Remove(Action dismiss)
		{
			if (dismiss == null) return;

			Handlers.Remove(dismiss);
		}

		// Taken off before it is invoked, so a handler that closes by way of something that pushes again
		// cannot leave a stale entry behind.
		public static bool DismissTop()
		{
			if (Handlers.Count == 0) return false;

			var index = Handlers.Count - 1;
			var handler = Handlers[index];
			Handlers.RemoveAt(index);

			handler.Invoke();
			return true;
		}
	}
}
