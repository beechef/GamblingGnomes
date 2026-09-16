using System.Threading;
using UnityEngine;

namespace Game.Runtime.Utility
{
	public static class AwaitableUtility
	{
		// Counted down to an unscaled deadline rather than awaited as a span: Awaitable.WaitForSecondsAsync
		// runs on scaled time, so at timeScale 0 — a loading screen, a pause menu, the very moments a UI
		// wait happens in — it never finishes at all.
		public static async Awaitable WaitUnscaledAsync(float seconds, CancellationToken ct = default)
		{
			var deadline = Time.unscaledTime + seconds;

			while (Time.unscaledTime < deadline) await Awaitable.NextFrameAsync(ct);
		}
	}
}
