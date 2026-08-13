using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The world-space counterpart to UIPokerView: the table announces itself, so anything drawing from
	// it binds on the event rather than watching for the instance to appear. Subclasses fill in OnBind
	// and OnUnbind and never touch the subscription itself, so the pairing cannot come apart.
	public abstract class PokerVisual : MonoBehaviour
	{
		protected PokerGameMode GameMode { get; private set; }
		protected PokerGameData Data => GameMode ? GameMode.Data : null;

		protected bool IsBound { get; private set; }

		protected virtual void OnEnable()
		{
			PokerGameMode.OnInstanceChanged += HandleInstanceChanged;

			Rebind();
		}

		protected virtual void OnDisable()
		{
			PokerGameMode.OnInstanceChanged -= HandleInstanceChanged;

			Unbind();
		}

		private void HandleInstanceChanged(PokerGameMode gameMode) => Rebind();

		private void Rebind()
		{
			Unbind();

			GameMode = PokerGameMode.Instance;

			// The table and its data arrive together, but the mode can be up before its NetworkBehaviour
			// has spawned — binding waits for both rather than half-wiring.
			if (!GameMode || !Data) return;

			IsBound = true;
			OnBind();
		}

		private void Unbind()
		{
			if (IsBound) OnUnbind();

			IsBound = false;
			GameMode = null;
		}

		protected virtual void OnBind() { }
		protected virtual void OnUnbind() { }
	}
}
