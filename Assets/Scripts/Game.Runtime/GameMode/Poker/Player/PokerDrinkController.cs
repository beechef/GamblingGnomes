using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// What a round of drinks leaves behind on one player. The drinking itself is a gesture the whole table
	// watches; the two things it can leave are both invisible from outside, and that is what the card is
	// played on.
	//
	// Being drunk is drawn on the drinker's own screen and nowhere else, so it is owner-read: nobody can
	// look across the table and see who was left sober, which is the only thing separating the cheat from
	// its honest twin. The sight of somebody's hand is owner-read for the reason every grant here is —
	// the cards replicate to everyone already, and refusing to draw them is the whole of the secret.
	//
	// Both end on a clock the server owns, and the server is what notices: a value that expired quietly
	// would leave the screen wobbling and the cards on show with nothing left to turn them off.
	public class PokerDrinkController : NetworkBehaviour
	{
		[Header("Debug")]
		[Tooltip("Writes one line per round of drinks saying what this player was left with. An honest card leaving nothing is the bluff working rather than a fault, which is exactly why the two are impossible to tell apart without asking.")]
		[SerializeField] private bool _logGrants;

		// When this player sobers up. Absolute server time rather than a countdown, so a client arriving
		// mid-round reads what is left instead of starting the clock again.
		[HideInInspector] public NetworkVariable<double> DrunkUntil = new(0d,
			readPerm: NetworkVariableReadPermission.Owner, writePerm: NetworkVariableWritePermission.Server);

		// Whose hand this player has been handed a look at. Empty on the card that was only a round of
		// drinks — which is the whole bluff.
		[HideInInspector] public NetworkVariable<NetworkBehaviourReference> ExposedTarget = new(default,
			readPerm: NetworkVariableReadPermission.Owner, writePerm: NetworkVariableWritePermission.Server);

		private double _exposedUntil;
		private bool _ruleInstalled;

		public bool IsDrunk => NetworkManager && NetworkManager.ServerTime.Time < DrunkUntil.Value;

		public double DrunkRemaining =>
			NetworkManager ? System.Math.Max(0d, DrunkUntil.Value - NetworkManager.ServerTime.Time) : 0d;

		public PokerPlayerData ExposedHand =>
			ExposedTarget.Value.TryGet(out PokerPlayerData data) ? data : null;

		public override void OnNetworkSpawn()
		{
			// Only this player's own client is ever granted the sight, so only their copy installs the rule.
			if (!IsOwner) return;

			PokerPlayerData.AddHandVisibilityProvider(CanSeeHandOf);
			ExposedTarget.OnValueChanged += HandleExposedChanged;

			_ruleInstalled = true;

			// A client joining mid-round is handed the value before it ever changes again, so what is
			// already standing is read rather than waited for.
			PokerPlayerData.NotifyHandVisibilityRulesChanged();
		}

		public override void OnNetworkDespawn()
		{
			// Ownership can pass to the server as a client leaves, so the instance that installed the rule
			// is the one that takes it back out — asking whether we are the owner is too late by now.
			if (!_ruleInstalled) return;

			_ruleInstalled = false;

			ExposedTarget.OnValueChanged -= HandleExposedChanged;

			PokerPlayerData.RemoveHandVisibilityProvider(CanSeeHandOf);
			PokerPlayerData.NotifyHandVisibilityRulesChanged();
		}

		public void ServerMakeDrunk(float seconds)
		{
			if (!IsServer || seconds <= 0f) return;

			DrunkUntil.Value = NetworkManager.ServerTime.Time + seconds;
		}

		// The look is handed over and taken back by the same component, so there is one place the grant can
		// possibly outlive its round.
		public void ServerExpose(PokerPlayerData target, float seconds)
		{
			if (!IsServer || !target || seconds <= 0f) return;

			_exposedUntil = NetworkManager.ServerTime.Time + seconds;
			ExposedTarget.Value = new NetworkBehaviourReference(target);
		}

		public void ServerClear()
		{
			if (!IsServer) return;

			_exposedUntil = 0d;

			if (DrunkUntil.Value != 0d) DrunkUntil.Value = 0d;
			if (ExposedTarget.Value.TryGet(out PokerPlayerData _)) ExposedTarget.Value = default;
		}

		// Not a poll for a dependency: the look ends on a clock, and somebody has to notice the moment it
		// does. Only the grant needs watching — the drunk clock is read live by the screen drawing it,
		// which is already ticking to draw the fade.
		private void Update()
		{
			if (!IsServer || !IsSpawned || _exposedUntil <= 0d) return;
			if (NetworkManager.ServerTime.Time < _exposedUntil) return;

			_exposedUntil = 0d;
			ExposedTarget.Value = default;
		}

		private bool CanSeeHandOf(PokerPlayerData other)
		{
			if (!other) return false;

			var target = ExposedHand;

			return target && target.NetworkObjectId == other.NetworkObjectId;
		}

		private void HandleExposedChanged(NetworkBehaviourReference previous, NetworkBehaviourReference current)
		{
			if (_logGrants)
			{
				var target = ExposedHand;
				Debug.Log($"[{nameof(PokerDrinkController)}] exposure changed: target={(target ? target.name : "none")}");
			}

			PokerPlayerData.NotifyHandVisibilityRulesChanged();
		}
	}
}
