using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// Bound by the table and by the player this client owns, both of which announce themselves. Views
	// subscribe to what they display in OnBind and let go in OnUnbind; nothing polls. A view that shows
	// something genuinely continuous — a countdown — opts into Tick and says when it is worth running.
	public abstract class UIPokerView : MonoBehaviour
	{
		protected PokerGameMode GameMode { get; private set; }
		// Held from bind to unbind: in a shutdown the table or the player can be destroyed before this view
		// lets go of them, and every OnUnbind unsubscribes through Data and LocalData.
		protected PokerGameData Data { get; private set; }

		protected PokerPlayer LocalPlayer { get; private set; }
		protected PokerPlayerData LocalData { get; private set; }

		protected bool IsBound { get; private set; }

		protected ulong LocalClientId => LocalPlayer ? LocalPlayer.ClientId : ulong.MaxValue;
		protected bool IsLocalTurn => Data && LocalPlayer && Data.CurrentTurnClientId.Value == LocalPlayer.ClientId;

		protected virtual bool WantsTick => false;

		// Not virtual, and neither is OnDisable: a subclass that overrode either and forgot base would
		// silently stop binding. Subclass work goes in OnBind/OnUnbind, which cannot be skipped.
		private void OnEnable()
		{
			PokerGameMode.OnInstanceChanged += HandleInstanceChanged;
			PokerPlayer.OnLocalPlayerChanged += HandleLocalPlayerChanged;

			Rebind();
		}

		private void OnDisable()
		{
			PokerGameMode.OnInstanceChanged -= HandleInstanceChanged;
			PokerPlayer.OnLocalPlayerChanged -= HandleLocalPlayerChanged;

			Unbind();
		}

		private void Update()
		{
			if (IsBound && WantsTick) OnTick();
		}

		private void HandleInstanceChanged(PokerGameMode gameMode) => Rebind();
		private void HandleLocalPlayerChanged(PokerPlayer player) => Rebind();

		private void Rebind()
		{
			Unbind();

			GameMode = PokerGameMode.Instance;
			Data = GameMode ? GameMode.Data : null;
			LocalPlayer = PokerPlayer.Local;
			LocalData = LocalPlayer ? LocalPlayer.Data : null;

			// The table and its player arrive in either order, so binding waits for both.
			if (!GameMode || !Data || !LocalPlayer) return;

			IsBound = true;
			OnBind();
		}

		private void Unbind()
		{
			if (IsBound) OnUnbind();

			IsBound = false;
			GameMode = null;
			Data = null;
			LocalPlayer = null;
			LocalData = null;
		}

		protected virtual void OnBind() { }
		protected virtual void OnUnbind() { }
		protected virtual void OnTick() { }
	}
}
