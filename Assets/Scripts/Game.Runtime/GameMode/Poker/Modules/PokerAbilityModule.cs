using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.GameMode.Poker.Stages;
using Game.Runtime.Player;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Modules
{
	// The ability game, plugged into the table as one module: a pool dealt out every hand, cheats
	// that tag their user, and the report that calls the tag. Guilt lives server-side in plain
	// collections — replicating it would hand the answer to the very players supposed to be
	// guessing. Everything a client needs rides replicated state instead of fired-off RPCs: the
	// card sits owner-read on the player, and whether the windows are open is decided here and
	// replicated, so the UI never has to guess at server config.
	public class PokerAbilityModule : PokerModule
	{
		[Header("Deal")]
		[SerializeField] private PokerAbilityPool _pool;

		[Tooltip("How many cheats are guaranteed in the deal, rolled fresh each hand between these two. A fixed count is a tell — once the table learns the number, the arithmetic does the guessing for them.")]
		[MinMaxSlider(0, 12, true)]
		[SerializeField] private Vector2Int _guaranteedCheatCards = new(1, 2);

		[Tooltip("How many abilities each seat is dealt. The wheel is what the player picks between, so more than one is the point of having it.")]
		[MinValue(1)]
		[SerializeField] private int _abilitiesPerSeat = 2;

		[Header("Ability Use")]
		[Tooltip("Stages abilities may be used in, matched by stage id. Empty allows every stage.")]
		[SerializeField] private List<PokerStage> _abilityStages = new();

		[Tooltip("Seconds from a stage opening that abilities stay usable. Zero or less keeps the window open for the whole stage.")]
		[SerializeField] private float _abilityWindowSeconds;

		[Header("Report")]
		[SerializeField] private int _reportsPerRound = 1;

		[Tooltip("Blood the accuser pays in as they name somebody, and the floor the accused answers with. Money is what the hand is played for; an accusation is paid for in blood.")]
		[MinValue(1)]
		[SerializeField] private int _reportStake = 1;

		[Tooltip("Stages a report may be filed in, matched by stage id. Empty allows any moment of the hand.")]
		[SerializeField] private List<PokerStage> _reportStages = new();

		[Tooltip("Overlay the accusation plays out in — aiming, answering, verdict. A report cannot be filed without one: there is nowhere to point from.")]
		[Required]
		[SerializeField] private PokerStage _reportStage;

		[Header("Toggle")]
		[Tooltip("Where the switch starts. Flip the replicated value at runtime to turn the whole ability game on or off.")]
		[SerializeField] private bool _enabledByDefault = true;

		[HideInInspector] public NetworkVariable<bool> Enabled = new(true,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// The module's standing decisions, made on the server and replicated — a client's UI shows the
		// ability game exactly when these say it is on, whatever stages and windows are configured.
		[HideInInspector] public NetworkVariable<bool> AbilityWindowOpen = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<bool> ReportWindowOpen = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		[HideInInspector] public NetworkVariable<PokerReportResult> LastReport = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Published as the report is filed, so every table shows the accusation while the verdict is
		// still being weighed. Its sequence is the one the matching verdict will carry.
		[HideInInspector] public NetworkVariable<PokerReportAccusation> Accusation = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Blood on the table for this accusation, per side. Set as the accuser names somebody and raised if
		// the accused shoves — the number both of them are in for, which is what the bar offers to match.
		[HideInInspector] public NetworkVariable<int> ReportStake = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Blood on the table for this accusation, both sides together. It grows the moment somebody acts —
		// staking is the act, and a pot that only fills once the result is in makes every answer look like
		// it did nothing. What comes after is only the wait for a verdict, not a wait for the money.
		[HideInInspector] public NetworkVariable<int> ReportPot = new(0,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Each answer given inside the accusation, announced in its own right. The table's own ActionNotice
		// cannot carry these: it prices everything in money off a change in Bet, and nothing here touches
		// Bet — so a call for three blood would go out as "CALL" with nothing after it.
		[HideInInspector] public NetworkVariable<PokerActionNotice> ReportAction = new(default,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Which step of the accusation the table is on. Replicated because each step asks a different
		// question, and a UI working that out from whose turn it is only stays right until there is a
		// fourth step.
		[HideInInspector] public NetworkVariable<PokerReportPhase> ReportPhase = new(PokerReportPhase.None,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		// Server-only truth. Which card each player holds and who cheated never replicate in the clear —
		// the report game is a guessing game, and this is the answer sheet.
		private readonly Dictionary<ulong, List<PokerAbility>> _held = new();
		private readonly HashSet<ulong> _cheaters = new();
		private readonly List<PokerAbility> _dealBuffer = new();

		// Nobody. A sentinel rather than a bool beside the id, so there is one place the answer lives and
		// no way for the two of them to disagree.
		private const ulong NoAim = ulong.MaxValue;

		private ulong _pendingAccuser;
		private ulong _pendingTarget;
		private bool _hasPendingReport;
		private ulong _aimClientId = NoAim;
		private int _accuserPaid;
		private int _accusedPaid;
		private double _stageOpenedTime;

		// Clients resolve their owner-read ability id against the same pool asset the server deals from.
		public PokerAbilityPool Pool => _pool;

		// Read by the report overlay as it opens: it runs the accusation, this holds the answer sheet.
		public bool HasPendingReport => _hasPendingReport;
		public ulong PendingAccuserClientId => _pendingAccuser;
		public ulong PendingTargetClientId => _pendingTarget;
		public bool HasAim => _aimClientId != NoAim;

		public int ReportBloodStake => Mathf.Max(1, _reportStake);

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();

			if (IsServer) Enabled.Value = _enabledByDefault;
		}

		// The report overlay never joins the loop, but every client renders it and reads its numbers off the
		// clone, so it is named here rather than left to the server's push to conjure up.
		public override void CollectReferencedStages(List<PokerStage> stages)
		{
			if (_reportStage) stages.Add(_reportStage);
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

		// A NetworkBehaviour update, not a poll for a dependency: the window closes on a clock, and
		// somebody has to notice the moment it does.
		private void Update()
		{
			if (!IsServer || !IsSpawned) return;

			RefreshWindowsServer();
		}

		private void RefreshWindowsServer()
		{
			var running = Enabled.Value && GameMode && GameMode.IsGameRunning;
			var uninterrupted = GameMode && GameMode.CurrentOverlay == null;

			var abilityOpen = running && uninterrupted && IsStageAllowed(_abilityStages) && IsInsideAbilityWindow();
			if (AbilityWindowOpen.Value != abilityOpen) AbilityWindowOpen.Value = abilityOpen;

			var reportOpen = running && uninterrupted && !_hasPendingReport && IsStageAllowed(_reportStages);
			if (ReportWindowOpen.Value != reportOpen) ReportWindowOpen.Value = reportOpen;
		}

		private void DealServer()
		{
			if (!IsServer) return;

			ClearRoundServer();

			if (!Enabled.Value || !_pool) return;

			var seats = GameMode.Seats;

			// Rolled per hand, inclusive of both ends, so the number of cheats in circulation is itself
			// something the table cannot count on.
			var cheats = UnityEngine.Random.Range(_guaranteedCheatCards.x, _guaranteedCheatCards.y + 1);

			_pool.DrawDeal(seats.Count * _abilitiesPerSeat, cheats, _dealBuffer);

			for (var i = 0; i < seats.Count; i++)
			{
				var seat = seats[i];

				// An empty chair's cards stay on the table unheld — they exist so the number of cheats
				// in circulation never betrays who drew one.
				if (!seat || !seat.IsOccupied) continue;

				var player = GameMode.FindSeatedPlayer(seat.OccupantClientId);
				if (!player || !player.Data.IsInHand) continue;

				var hand = new List<PokerAbility>();
				var data = player.Data;

				for (var slot = 0; slot < _abilitiesPerSeat; slot++)
				{
					var index = i * _abilitiesPerSeat + slot;
					if (index >= _dealBuffer.Count) break;

					var ability = _dealBuffer[index];
					if (!ability) continue;

					hand.Add(ability);
					data.AbilityIds.Add(ability.AbilityId);
				}

				_held[player.ClientId] = hand;
				data.ReportsLeft.Value = _reportsPerRound;
			}
		}

		private void ClearRoundServer()
		{
			if (!IsServer || !GameMode) return;

			_held.Clear();
			_cheaters.Clear();

			// A hand torn down mid-accusation leaves an arm across the table and somebody lit up, because
			// nothing else is coming to put them back — the round ending is the last thing that happens.
			EndAimServer();

			_hasPendingReport = false;
			_accuserPaid = 0;
			_accusedPaid = 0;
			ReportStake.Value = 0;
			ReportPot.Value = 0;
			ReportPhase.Value = PokerReportPhase.None;

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !player.Data) continue;

				player.Data.AbilityIds.Clear();
				player.Data.ReportsLeft.Value = 0;
			}
		}

		// Named rather than implied: the player holds several and the wheel says which one they turned up,
		// so the server is told the choice instead of inferring it from a hand it would have to assume the
		// order of.
		[Rpc(SendTo.Server)]
		public void UseAbilityRPC(FixedString64Bytes abilityId, RpcParams rpcParams = default)
		{
			var clientId = rpcParams.Receive.SenderClientId;

			// The replicated window is the same decision the UI showed — the server just has the
			// final word on it.
			if (!AbilityWindowOpen.Value) return;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !player.Data.IsInHand) return;
			if (!_held.TryGetValue(clientId, out var hand) || hand == null) return;

			var index = hand.FindIndex(candidate => candidate && candidate.AbilityId == abilityId.ToString());
			if (index < 0) return;

			var ability = hand[index];
			if (!ability.ActivateServer(GameMode, player)) return;

			// Only the owner's replica learns the card is spent. Nobody else hears a thing — an
			// ability is played in silence, and the report game is how it ever comes to light.
			hand.RemoveAt(index);
			RemoveFirst(player.Data.AbilityIds, abilityId);

			if (ability.Kind == PokerAbilityKind.Cheat) _cheaters.Add(clientId);
		}

		// One entry, not every match: a player dealt the same trick twice spends one copy and keeps the
		// other, the same way two identical cards in a hand are still two cards.
		private static void RemoveFirst(NetworkList<FixedString64Bytes> list, FixedString64Bytes value)
		{
			for (var i = 0; i < list.Count; i++)
			{
				if (!list[i].Equals(value)) continue;

				list.RemoveAt(i);
				return;
			}
		}

		// Opens the accusation without naming anybody: who it lands on is decided by where the accuser
		// looks over the next few seconds, not by a menu. The charge is spent here rather than at the
		// moment a name is picked, so opening the floor and then pointing at nobody still costs one.
		[Rpc(SendTo.Server)]
		public void ReportRPC(RpcParams rpcParams = default)
		{
			var accuser = rpcParams.Receive.SenderClientId;

			// Refusals say which gate they came from. A report that quietly does nothing is indisting-
			// uishable from a button that is not wired, an animation that did not import and a stage that
			// was never pushed — and the client has already been told it may ask, so a no is worth a line.
			if (!ReportWindowOpen.Value) { RefuseReport(accuser, "the report window is closed"); return; }
			if (_hasPendingReport) { RefuseReport(accuser, "another report is already running"); return; }
			if (!_reportStage) { RefuseReport(accuser, "no report stage is assigned on the module"); return; }

			// Folding is not a gag order. A player out of the hand watched the same table everyone else
			// did, and folding out does not launder a cheat on the other side either — so being dealt in
			// is the whole test, at both ends of the finger.
			var accuserPlayer = GameMode.FindSeatedPlayer(accuser);
			if (!accuserPlayer) { RefuseReport(accuser, "they are not seated at this table"); return; }
			if (!WasDealtIn(accuserPlayer)) { RefuseReport(accuser, $"they were never dealt in (status {accuserPlayer.Data.Status.Value})"); return; }
			if (accuserPlayer.Data.ReportsLeft.Value <= 0) { RefuseReport(accuser, "they have no reports left this hand"); return; }

			accuserPlayer.Data.ReportsLeft.Value -= 1;

			_pendingAccuser = accuser;
			_pendingTarget = accuser;
			_hasPendingReport = true;
			_aimClientId = NoAim;
			_accuserPaid = 0;
			_accusedPaid = 0;

			ReportStake.Value = 0;
			ReportPot.Value = 0;

			GameMode.PushOverlay(_reportStage);
		}

		// The accusation opens as a scene rather than a menu: the accuser throws an arm out and holds it
		// there while they look for a face. The gesture starts it and the pose is what lasts, so the two
		// are played together — and from the stage opening rather than from the click that pushed it, or
		// the arm goes out before the table has been stopped to watch it.
		public void BeginAimServer()
		{
			if (!IsServer || !_hasPendingReport) return;

			var accuser = GameMode.FindSeatedPlayer(_pendingAccuser);
			if (!accuser) return;

			// Said out loud rather than skipped over: the arm not going out is the first thing anybody
			// notices, and a null here looks exactly like a missing animator state from the outside.
			if (!accuser.ActionAnimator) Debug.LogWarning($"[PokerAbilityModule] {accuser.DisplayName} has no PlayerActionAnimator — the report gesture cannot play.");
			if (!accuser.Point) Debug.LogWarning($"[PokerAbilityModule] {accuser.DisplayName} has no PlayerPointController — the pointing pose cannot be held.");

			accuser.ActionAnimator?.ServerPlay(PlayerActionIds.Report);
			if (accuser.Point) accuser.Point.ServerSetPointing(true);
		}

		private static void RefuseReport(ulong accuser, string reason)
		{
			Debug.LogWarning($"[PokerAbilityModule] Report from client {accuser} refused: {reason}.");
		}

		// Where the accuser is looking, sent as it changes rather than every frame. Only the accuser's own
		// client can see through their eyes, so the aim has to come from there — the server's word is on
		// whether whoever they named can be accused at all.
		[Rpc(SendTo.Server)]
		public void AimReportRPC(ulong targetClientId, RpcParams rpcParams = default)
		{
			if (!_hasPendingReport || ReportPhase.Value != PokerReportPhase.Aiming) return;
			if (rpcParams.Receive.SenderClientId != _pendingAccuser) return;

			SetAimServer(targetClientId);
		}

		public void AnnounceReportActionServer(ulong clientId, PokerActionType action, int amount)
		{
			if (!IsServer) return;

			ReportAction.Value = new PokerActionNotice
			{
				ClientId = clientId,
				Action = action,
				Amount = Mathf.Max(0, amount),
				Sequence = ReportAction.Value.Sequence + 1
			};
		}

		public void SetReportPhaseServer(PokerReportPhase phase)
		{
			if (!IsServer) return;

			ReportPhase.Value = phase;
		}

		// Lit up for the whole room, not just for the accuser: the table watching a finger travel from
		// face to face is the point of aiming out loud.
		private void SetAimServer(ulong targetClientId)
		{
			var next = targetClientId != _pendingAccuser && CanBeReported(targetClientId) ? targetClientId : NoAim;
			if (_aimClientId == next) return;

			SetHighlightServer(_aimClientId, false);
			_aimClientId = next;
			SetHighlightServer(_aimClientId, true);
		}

		// The finger comes down where it was pointing. Aimed at nobody, the accusation is dropped rather
		// than thrown at whoever happens to be sitting opposite — failing to pick anybody is not the same
		// as picking wrong, and only the second one is supposed to cost blood.
		public bool LockReportTargetServer()
		{
			if (!IsServer || !_hasPendingReport) return false;

			var accuser = GameMode.FindSeatedPlayer(_pendingAccuser);
			var target = _aimClientId != NoAim ? GameMode.FindSeatedPlayer(_aimClientId) : null;

			EndAimServer();

			if (!accuser || !target) return false;

			_pendingTarget = target.ClientId;

			Accusation.Value = new PokerReportAccusation
			{
				AccuserClientId = _pendingAccuser,
				TargetClientId = _pendingTarget,
				Sequence = LastReport.Value.Sequence + 1
			};

			// Paid the moment the name is said, so the accused is being asked to match something already on
			// the table rather than to open the bidding themselves.
			ReportStake.Value = Mathf.Min(ReportBloodStake, accuser.Data.Health.Value);
			CommitReportBloodServer(_pendingAccuser, ReportStake.Value);

			target.ActionAnimator?.ServerPlay(PlayerActionIds.Reported);

			return true;
		}

		// What the accused can shove: every drop either of them has, since neither side can be in for more
		// than the other can cover. The bar offers this number by calling the same method the server
		// clamps with.
		public int AllInStake(PokerPlayer accuser, PokerPlayer accused)
		{
			// What is left plus what is already on the table: a stake is measured from where somebody
			// started, not from what the pot has taken off them since.
			var accuserBlood = accuser && accuser.Data ? accuser.Data.Health.Value + _accuserPaid : 0;
			var accusedBlood = accused && accused.Data ? accused.Data.Health.Value + _accusedPaid : 0;

			return Mathf.Max(ReportStake.Value, Mathf.Min(accuserBlood, accusedBlood));
		}

		// Blood put in, there and then. Each side is tracked against what it has already staked so a call
		// after a shove pays only the difference, and a second call pays nothing.
		public int CommitReportBloodServer(ulong clientId, int stake)
		{
			if (!IsServer || !_hasPendingReport) return 0;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player) return 0;

			var isAccuser = clientId == _pendingAccuser;
			var alreadyPaid = isAccuser ? _accuserPaid : _accusedPaid;

			var paid = PayBlood(player, stake - alreadyPaid);
			if (paid <= 0) return 0;

			if (isAccuser) _accuserPaid += paid;
			else _accusedPaid += paid;

			ReportPot.Value += paid;

			return paid;
		}

		// A shove hands the question back. The accuser named a number and put blood behind it; being asked
		// for more than that is a new question, and one they are allowed to refuse — which is the whole
		// risk in shoving, and the reason a guilty player might.
		public bool RequiresAccuserCall(int accusedStake) => accusedStake > _accuserPaid;

		// What each side is now in for. Raised by a shove so the bar offers to match the shove rather than
		// the opening stake — the accuser's own contribution is tracked separately and is what says whether
		// they still owe anything.
		public void SetReportStakeServer(int stake)
		{
			if (!IsServer) return;

			ReportStake.Value = Mathf.Max(0, stake);
		}

		// The accuser walking away from a shove. Nothing is judged: the accused keeps their secret and
		// takes what was thrown at them, which is what the accuser paid for the privilege of asking.
		public void ConcedeReportServer()
		{
			if (!IsServer || !_hasPendingReport) return;

			_hasPendingReport = false;
			EndAimServer();

			var accuser = GameMode.FindSeatedPlayer(_pendingAccuser);
			var target = GameMode.FindSeatedPlayer(_pendingTarget);

			// Whatever is on the table goes to the one who was not backed down from. It is already out of
			// the accuser's veins — walking away is what decides where it lands, not whether it was paid.
			var pot = ReportPot.Value;
			if (target) target.Data.ServerChangeHealth(pot);

			// Read from the other side to whoever is watching: the one who shoved is laughing.
			accuser?.ActionAnimator?.ServerPlay(PlayerActionIds.Disappointed);
			target?.ActionAnimator?.ServerPlay(PlayerActionIds.Laugh);

			if (accuser && !accuser.Data.IsAlive) FoldServer(accuser);

			PublishReport(false, false, pot);
		}

		// The end of it, once both of them are in for the same number: the hand gets judged and whatever is
		// on the table goes one way or the other. Nothing is staked here — every drop was paid as it was
		// offered, and this is only the moment the table finds out whose it was.
		public void ResolveReportServer()
		{
			if (!IsServer || !_hasPendingReport) return;

			_hasPendingReport = false;
			EndAimServer();

			var accuser = GameMode.FindSeatedPlayer(_pendingAccuser);
			var target = GameMode.FindSeatedPlayer(_pendingTarget);

			if (!accuser || !target)
			{
				PublishReport(false, false, 0);
				return;
			}

			var pot = ReportPot.Value;
			var wasCheater = _cheaters.Contains(_pendingTarget);
			var winner = wasCheater ? accuser : target;

			winner.Data.ServerChangeHealth(pot);

			// A cheat caught in the act is out of the hand as well as out of blood. Pointing at the wrong
			// player only costs blood: the accuser plays on, which is what keeps a hunch worth acting on at
			// all rather than a way to talk yourself out of the hand.
			if (wasCheater) FoldServer(target);

			// Whoever the blood left may have had none to spare. Bleeding out ends this hand for them the
			// same way being caught does — the seat and the table are dealt with when the next hand is.
			var loser = wasCheater ? target : accuser;
			if (!loser.Data.IsAlive) FoldServer(loser);

			// The verdict on their faces: whoever the blood just left hangs their head, whoever it came to
			// laughs at them.
			accuser.ActionAnimator?.ServerPlay(wasCheater ? PlayerActionIds.Laugh : PlayerActionIds.Disappointed);
			target.ActionAnimator?.ServerPlay(wasCheater ? PlayerActionIds.Disappointed : PlayerActionIds.Laugh);

			PublishReport(true, wasCheater, pot);
		}

		// An accusation that never found a face, or whose two people are no longer both here. Nothing is
		// judged, so whatever either of them put in comes back: they are being let off the accusation, not
		// charged for one the table never heard the end of.
		public void DropReportServer()
		{
			if (!IsServer || !_hasPendingReport) return;

			_hasPendingReport = false;
			EndAimServer();

			Refund(_pendingAccuser, _accuserPaid);
			Refund(_pendingTarget, _accusedPaid);

			_accuserPaid = 0;
			_accusedPaid = 0;
			ReportStake.Value = 0;
			ReportPot.Value = 0;

			PublishReport(false, false, 0);
		}

		private void Refund(ulong clientId, int amount)
		{
			if (amount <= 0) return;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (player) player.Data.ServerChangeHealth(amount);
		}

		private void PublishReport(bool called, bool wasCheater, int amount)
		{
			LastReport.Value = new PokerReportResult
			{
				AccuserClientId = _pendingAccuser,
				TargetClientId = _pendingTarget,
				Called = called,
				WasCheater = wasCheater,
				Amount = amount,
				Sequence = LastReport.Value.Sequence + 1
			};
		}

		// Takes what is there rather than refusing what is not: a player answering with their last drop is
		// answering, and the caller is told what actually moved.
		private static int PayBlood(PokerPlayer player, int amount)
		{
			if (!player || !player.Data || amount <= 0) return 0;

			var paid = Mathf.Min(amount, player.Data.Health.Value);
			if (paid > 0) player.Data.ServerChangeHealth(-paid);

			return paid;
		}

		// The arm comes down and the room goes dark again, whichever way the accusation went.
		private void EndAimServer()
		{
			SetHighlightServer(_aimClientId, false);
			_aimClientId = NoAim;

			var accuser = GameMode.FindSeatedPlayer(_pendingAccuser);
			if (accuser && accuser.Point) accuser.Point.ServerSetPointing(false);
		}

		private void SetHighlightServer(ulong clientId, bool highlighted)
		{
			if (clientId == NoAim) return;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !player.Visual) return;

			player.Visual.ServerSetOutlined(highlighted);
		}

		private bool CanBeReported(ulong clientId)
		{
			var player = GameMode.FindSeatedPlayer(clientId);

			return player && WasDealtIn(player);
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
	}
}
