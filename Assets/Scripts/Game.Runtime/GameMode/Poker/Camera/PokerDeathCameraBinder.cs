using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player.Camera;
using Game.Runtime.Utility;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// Going under is a moment the whole room turns to watch, the one going under included. Every client
	// hears it the same way — a player's hallucination reaching the ceiling is already replicated — so the
	// shot is derived on each screen from that change rather than announced, and nobody needs to be told
	// who to look at.
	//
	// Only the change counts. A player who was already under when this client arrived raises no event, so
	// joining a table with a body on it does not replay somebody else's death.
	public class PokerDeathCameraBinder : NetworkBehaviour
	{
		[Header("Shot")]
		[Required]
		[Tooltip("The state this puts up. Normally the one on this same object.")]
		[SerializeField] private PokerDeathCameraState _state;

		[Tooltip("Where the view goes back to. Empty finds it up the hierarchy.")]
		[SerializeField] private PlayerCameraController _camera;

		[Tooltip("How long the room watches, counted from the moment the ceiling is reached. Long enough to see the fall and the head go.")]
		[MinValue(0f)]
		[SerializeField] private float _duration = 4f;

		private readonly Dictionary<PokerPlayerData, Action<int, int>> _watched = new();

		private int _handle;
		private CancellationTokenSource _hold;

		// Whoever the room is watching go under. Read by the state, which aims at them.
		public PokerPlayer Dying { get; private set; }

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			if (!_state) _state = GetComponent<PokerDeathCameraState>();
			if (!_camera) _camera = GetComponentInParent<PlayerCameraController>();

			PokerPlayer.OnRegistryChanged += HandleRegistryChanged;
			WatchPlayers();
		}

		public override void OnNetworkDespawn()
		{
			if (!IsOwner) return;

			PokerPlayer.OnRegistryChanged -= HandleRegistryChanged;
			UnwatchPlayers();
			EndShot();
		}

		// Every seat re-bound whenever the room changes, the same shape UIPokerStartPanel takes: the list is
		// small, and a player leaving must drop their handler with them.
		private void HandleRegistryChanged()
		{
			WatchPlayers();

			if (Dying && !Contains(PokerPlayer.All, Dying)) EndShot();
		}

		private void WatchPlayers()
		{
			UnwatchPlayers();

			foreach (var player in PokerPlayer.All)
			{
				if (!player || !player.Data || _watched.ContainsKey(player.Data)) continue;

				var watched = player;
				Action<int, int> handler = (previous, current) => HandleHallucinationChanged(watched, previous, current);

				player.Data.OnHallucinationChanged += handler;
				_watched.Add(player.Data, handler);
			}
		}

		private void UnwatchPlayers()
		{
			foreach (var pair in _watched)
				if (pair.Key) pair.Key.OnHallucinationChanged -= pair.Value;

			_watched.Clear();
		}

		private void HandleHallucinationChanged(PokerPlayer player, int previous, int current)
		{
			var wentUnder = previous < PokerPlayerData.MaxHallucination && current >= PokerPlayerData.MaxHallucination;
			var cameBack = current < PokerPlayerData.MaxHallucination;

			if (wentUnder) BeginShot(player);
			else if (cameBack && player == Dying) EndShot();
		}

		private void BeginShot(PokerPlayer player)
		{
			CancelHold();

			Dying = player;

			// A second death landing while the first is still being watched re-aims the shot already up
			// rather than stacking a second request on top of it.
			if (_handle == 0) _handle = _camera ? _camera.Request(_state) : 0;
			else if (_state) _state.Refocus();

			_hold = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			_ = EndAfter(_duration, _hold.Token);
		}

		private async Awaitable EndAfter(float seconds, CancellationToken ct)
		{
			try
			{
				await AwaitableUtility.WaitUnscaledAsync(seconds, ct);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			EndShot();
		}

		private void EndShot()
		{
			CancelHold();

			if (_handle != 0 && _camera) _camera.Release(_handle);
			_handle = 0;

			Dying = null;
		}

		private void CancelHold()
		{
			if (_hold == null) return;

			_hold.Cancel();
			_hold.Dispose();
			_hold = null;
		}

		private static bool Contains(IReadOnlyList<PokerPlayer> players, PokerPlayer player)
		{
			foreach (var candidate in players)
				if (candidate == player) return true;

			return false;
		}
	}
}
