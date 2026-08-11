using Game.Runtime.GameMode.Poker;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The table spawns with the network, long after scene UI has woken up, so every poker view binds
	// lazily and rebinds if the mode is replaced instead of grabbing a reference once and going stale.
	public abstract class UIPokerView : MonoBehaviour
	{
		protected PokerGameMode GameMode { get; private set; }
		protected PokerGameData Data => GameMode ? GameMode.Data : null;

		protected bool IsBound { get; private set; }

		protected virtual void Update()
		{
			var gameMode = PokerGameMode.Instance;

			if (gameMode != GameMode)
			{
				Unbind();
				GameMode = gameMode;

				if (GameMode && Data)
				{
					OnBind();
					IsBound = true;
				}
			}

			if (IsBound) OnTick();
		}

		protected virtual void OnDisable() => Unbind();
		protected virtual void OnDestroy() => Unbind();

		private void Unbind()
		{
			if (IsBound) OnUnbind();

			IsBound = false;
			GameMode = null;
		}

		protected virtual void OnBind() { }
		protected virtual void OnUnbind() { }
		protected virtual void OnTick() { }

		protected bool IsLocalTurn => Data && Data.CurrentTurnClientId.Value == LocalClientId;

		protected ulong LocalClientId => Unity.Netcode.NetworkManager.Singleton
			? Unity.Netcode.NetworkManager.Singleton.LocalClientId
			: ulong.MaxValue;
	}
}
