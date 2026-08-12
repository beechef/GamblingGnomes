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
		protected PokerGameData Data => GameMode ? GameMode.Data : null;

		protected PokerPlayer LocalPlayer { get; private set; }
		protected PokerPlayerData LocalData => LocalPlayer ? LocalPlayer.Data : null;

		protected bool IsBound { get; private set; }

		protected ulong LocalClientId => LocalPlayer ? LocalPlayer.ClientId : ulong.MaxValue;
		protected bool IsLocalTurn => Data && LocalPlayer && Data.CurrentTurnClientId.Value == LocalPlayer.ClientId;

		protected virtual bool WantsTick => false;

		protected virtual void OnEnable()
		{
			PokerGameMode.OnInstanceChanged += HandleInstanceChanged;
			PokerPlayer.OnLocalPlayerChanged += HandleLocalPlayerChanged;

			Rebind();
		}

		protected virtual void OnDisable()
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
			LocalPlayer = PokerPlayer.Local;

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
			LocalPlayer = null;
		}

		protected virtual void OnBind() { }
		protected virtual void OnUnbind() { }
		protected virtual void OnTick() { }
	}
}
