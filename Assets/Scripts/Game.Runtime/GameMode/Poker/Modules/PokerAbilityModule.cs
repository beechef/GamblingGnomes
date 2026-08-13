using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Modules
{
	// The ability game, plugged into the table as one module: a pool dealt out every hand, cheats
	// that tag their user, and the report that calls the tag. Everything that decides guilt lives
	// server-side in plain collections — replicating any of it would hand the answer to the very
	// players who are supposed to be guessing.
	public class PokerAbilityModule : PokerModule
	{
		[Header("Deal")]
		[SerializeField] private PokerAbilityPool _pool;

		[Tooltip("Cards are dealt one per seat, empty chairs included, so the count in play never says who is holding what.")]
		[SerializeField] private int _guaranteedCheatCards = 1;

		[Header("Ability Use")]
		[Tooltip("Stages abilities may be used in, matched by stage id. Empty allows every stage.")]
		[SerializeField] private List<PokerStage> _abilityStages = new();

		[Tooltip("Seconds from a stage opening that abilities stay usable. Zero or less keeps the window open for the whole stage.")]
		[SerializeField] private float _abilityWindowSeconds;

		[Header("Report")]
		[SerializeField] private int _reportsPerRound = 1;

		[Tooltip("Moved from the loser's wallet to the winner's — a wrong accusation costs the accuser exactly what a right one would have won them.")]
		[SerializeField] private int _reportStake = 100;

		[Tooltip("Stages a report may be filed in, matched by stage id. Empty allows any moment of the hand.")]
		[SerializeField] private List<PokerStage> _reportStages = new();

		[Tooltip("Overlay shown while the verdict lands. Empty resolves on the spot with no pause.")]
		[SerializeField] private PokerStage _reportStage;

		[Header("Toggle")]
		[Tooltip("Where the switch starts. Flip the replicated value at runtime to turn the whole ability game on or off.")]
		[SerializeField] private bool _enabledByDefault = true;

		[HideInInspector] public NetworkVariable<bool> Enabled = new(true,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<PokerReportResult> LastReport = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Published as the report is filed, so every table shows the accusation while the verdict is
		// still being weighed. Its sequence is the one the matching verdict will carry.
		[HideInInspector] public NetworkVariable<PokerReportAccusation> Accusation = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Server-only truth. The held cards, the cheater tags and the spent reports never replicate —
		// the report game is a guessing game, and this is the answer sheet.
		private readonly Dictionary<ulong, PokerAbility> _held = new();
		private readonly HashSet<ulong> _cheaters = new();
		private readonly HashSet<ulong> _usedAbility = new();
		private readonly Dictionary<ulong, int> _reportsUsed = new();
		private readonly List<PokerAbility> _dealBuffer = new();

		private ulong _pendingAccuser;
		private ulong _pendingTarget;
		private bool _hasPendingReport;
		private double _stageOpenedTime;

		// What this client knows about its own hand of the ability game, fed by targeted RPCs.
		public string LocalAbilityId { get; private set; }
		public string LocalAbilityName { get; private set; }
		public PokerAbilityKind LocalAbilityKind { get; private set; }
		public bool LocalAbilityUsed { get; private set; }
		public int LocalReportsLeft { get; private set; }
		public bool HasLocalAbility => !string.IsNullOrEmpty(LocalAbilityId);

		public event Action OnLocalStateChanged;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();

			if (IsServer) Enabled.Value = _enabledByDefault;

			LastReport.OnValueChanged += HandleReportChanged;
		}

		public override void OnNetworkDespawn()
		{
			LastReport.OnValueChanged -= HandleReportChanged;

			base.OnNetworkDespawn();
		}

		public void SetEnabledServer(bool enabled)
		{
			if (!IsServer) return;

			Enabled.Value = enabled;
		}

		public override void OnStageStarted(PokerStage stage)
		{
			_stageOpenedTime = NetworkManager ? NetworkManager.ServerTime.Time : 0d;

			if (stage is PokerDealStage) DealServer();
		}

		public override void OnGameEnded() => ClearRoundServer();

		private void DealServer()
		{
			if (!IsServer) return;

			ClearRoundServer();

			if (!Enabled.Value || !_pool) return;

			var seats = GameMode.Seats;
			_pool.DrawDeal(seats.Count, _guaranteedCheatCards, _dealBuffer);

			for (var i = 0; i < seats.Count && i < _dealBuffer.Count; i++)
			{
				var seat = seats[i];

				// An empty chair's card stays on the table unheld — it exists so the number of cheats
				// in circulation never betrays who drew one.
				if (!seat || !seat.IsOccupied) continue;

				var player = GameMode.FindSeatedPlayer(seat.OccupantClientId);
				if (!player || !player.Data.IsInHand) continue;

				var ability = _dealBuffer[i];
				if (!ability) continue;

				_held[player.ClientId] = ability;

				GrantAbilityRPC(ability.AbilityId, ability.DisplayName, ability.Kind,
					RpcTarget.Single(player.ClientId, RpcTargetUse.Temp));
			}
		}

		private void ClearRoundServer()
		{
			if (!IsServer) return;

			_held.Clear();
			_cheaters.Clear();
			_usedAbility.Clear();
			_reportsUsed.Clear();
			_hasPendingReport = false;

			ClearLocalAbilityRPC();
		}

		[Rpc(SendTo.Server)]
		public void UseAbilityRPC(RpcParams rpcParams = default)
		{
			var clientId = rpcParams.Receive.SenderClientId;

			if (!Enabled.Value || !GameMode || !GameMode.IsGameRunning) return;
			if (!IsStageAllowed(_abilityStages) || !IsInsideAbilityWindow()) return;
			if (_usedAbility.Contains(clientId)) return;
			if (!_held.TryGetValue(clientId, out var ability) || !ability) return;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !player.Data.IsInHand) return;

			if (!ability.ActivateServer(GameMode, player)) return;

			_usedAbility.Add(clientId);
			if (ability.Kind == PokerAbilityKind.Cheat) _cheaters.Add(clientId);

			// Only the user learns their card is spent. Nobody else hears a thing — an ability is
			// played in silence, and the report game is the only way it ever comes to light.
			ConfirmAbilityUsedRPC(RpcTarget.Single(clientId, RpcTargetUse.Temp));
		}

		[Rpc(SendTo.Server)]
		public void ReportRPC(ulong targetClientId, RpcParams rpcParams = default)
		{
			var accuser = rpcParams.Receive.SenderClientId;

			if (!Enabled.Value || !GameMode || !GameMode.IsGameRunning) return;
			if (_hasPendingReport || accuser == targetClientId) return;
			if (!IsStageAllowed(_reportStages)) return;
			if (GetReportsUsed(accuser) >= _reportsPerRound) return;

			// A folded player has no voice left this hand — they can be accused, not accuse.
			var accuserPlayer = GameMode.FindSeatedPlayer(accuser);
			if (!accuserPlayer || !accuserPlayer.Data.IsInHand) return;

			// The accused only has to have been dealt in: folding out does not launder a cheat.
			var targetPlayer = GameMode.FindSeatedPlayer(targetClientId);
			if (!targetPlayer || !WasDealtIn(targetPlayer)) return;

			_reportsUsed[accuser] = GetReportsUsed(accuser) + 1;

			_pendingAccuser = accuser;
			_pendingTarget = targetClientId;
			_hasPendingReport = true;

			Accusation.Value = new PokerReportAccusation
			{
				AccuserClientId = accuser,
				TargetClientId = targetClientId,
				Sequence = LastReport.Value.Sequence + 1
			};

			if (_reportStage) GameMode.PushOverlay(_reportStage);
			else ResolvePendingReportServer();
		}

		// Called by the report overlay as it opens — or directly, when no overlay is configured.
		public void ResolvePendingReportServer()
		{
			if (!IsServer || !_hasPendingReport) return;

			_hasPendingReport = false;

			var accuser = GameMode.FindSeatedPlayer(_pendingAccuser);
			var target = GameMode.FindSeatedPlayer(_pendingTarget);
			if (accuser == null || target == null) return;

			var wasCheater = _cheaters.Contains(_pendingTarget);
			var loser = wasCheater ? target : accuser;
			var winner = wasCheater ? accuser : target;

			var wallet = loser.Wallet;
			var amount = Math.Min(_reportStake, wallet ? wallet.Money.Value : 0);
			if (amount > 0 && wallet.ServerTryWithdraw(amount)) winner.Data.ServerWinChips(amount);

			FoldServer(loser);

			LastReport.Value = new PokerReportResult
			{
				AccuserClientId = _pendingAccuser,
				TargetClientId = _pendingTarget,
				WasCheater = wasCheater,
				Amount = amount,
				Sequence = LastReport.Value.Sequence + 1
			};
		}

		private void FoldServer(PokerPlayer player)
		{
			// Already folded — the report only costs them the stake.
			if (!player.Data.IsInHand) return;

			player.Data.Status.Value = PokerPlayerStatus.Folded;
			player.Data.HasActed.Value = true;

			// Folded from outside their own action path: a street waiting on them would wait forever,
			// so it hears the same thing a departure would tell it. Under the report overlay the turn
			// slot is already parked, and the paused street re-reads the table when it resumes.
			if (Data.CurrentTurnClientId.Value != player.ClientId) return;

			var seatIndex = player.Data.SeatIndex.Value;
			GameMode.ClearTurn();
			GameMode.ActiveStage?.HandlePlayerLeft(player.ClientId, seatIndex);
		}

		private static bool WasDealtIn(PokerPlayer player)
		{
			return player.Data.IsInHand || player.Data.Status.Value == PokerPlayerStatus.Folded;
		}

		private int GetReportsUsed(ulong clientId) => _reportsUsed.GetValueOrDefault(clientId, 0);

		// Empty list keeps the gate open — restriction is opt-in, per the configured stage ids.
		private bool IsStageAllowed(List<PokerStage> stages)
		{
			if (stages.Count == 0) return true;

			var active = GameMode.ActiveStage;
			if (!active) return false;

			foreach (var stage in stages)
			{
				if (stage && stage.StageId == active.StageId) return true;
			}

			return false;
		}

		private bool IsInsideAbilityWindow()
		{
			if (_abilityWindowSeconds <= 0f || !NetworkManager) return true;

			return NetworkManager.ServerTime.Time - _stageOpenedTime <= _abilityWindowSeconds;
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void GrantAbilityRPC(FixedString32Bytes abilityId, FixedString64Bytes displayName, PokerAbilityKind kind, RpcParams rpcParams = default)
		{
			LocalAbilityId = abilityId.ToString();
			LocalAbilityName = displayName.ToString();
			LocalAbilityKind = kind;
			LocalAbilityUsed = false;
			LocalReportsLeft = _reportsPerRound;

			OnLocalStateChanged?.Invoke();
		}

		[Rpc(SendTo.ClientsAndHost)]
		private void ClearLocalAbilityRPC()
		{
			LocalAbilityId = null;
			LocalAbilityName = null;
			LocalAbilityUsed = false;

			OnLocalStateChanged?.Invoke();
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ConfirmAbilityUsedRPC(RpcParams rpcParams = default)
		{
			LocalAbilityUsed = true;
			OnLocalStateChanged?.Invoke();
		}

		private void HandleReportChanged(PokerReportResult previous, PokerReportResult current)
		{
			if (NetworkManager && current.AccuserClientId == NetworkManager.LocalClientId && LocalReportsLeft > 0)
			{
				LocalReportsLeft--;
			}

			OnLocalStateChanged?.Invoke();
		}
	}
}
