using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Modules
{
	// A house rule for a table that plays in two currencies: a player who has run out of money is offered
	// the only other thing they are carrying. At the top of their turn, if the purse is empty, blood buys
	// a stake back — so being broke is a decision about how much of yourself to spend rather than the end
	// of the hand.
	//
	// Bought at the turn rather than at the deal on purpose: it is the moment the table actually asks them
	// for something, so the price is paid against a question rather than in advance of one.
	public class PokerBloodExchangeModule : PokerModule
	{
		[Header("Exchange")]
		[Tooltip("Where the switch starts. Flip the replicated value at runtime to turn the exchange on or off for the table.")]
		[SerializeField] private bool _enabledByDefault = true;

		[Tooltip("Blood taken each time the exchange runs.")]
		[MinValue(1)]
		[SerializeField] private int _bloodSpent = 1;

		[Tooltip("Money handed over for it. Together with the blood spent this is the whole rate, written as two numbers anybody can read off the inspector.")]
		[MinValue(1)]
		[SerializeField] private int _moneyGained = 10;

		[Tooltip("Blood a player is never taken below. Somebody down to their last drop is left broke rather than bled out by a rule they did not choose.")]
		[MinValue(0)]
		[SerializeField] private int _minimumHealthKept = 1;

		[HideInInspector] public NetworkVariable<bool> Enabled = new(true,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public int BloodSpent => Mathf.Max(1, _bloodSpent);
		public int MoneyGained => Mathf.Max(1, _moneyGained);
		public int MinimumHealthKept => Mathf.Max(0, _minimumHealthKept);

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();

			if (IsServer) Enabled.Value = _enabledByDefault;
		}

		public void SetEnabledServer(bool enabled)
		{
			if (!IsServer) return;

			Enabled.Value = enabled;
		}

		public override void OnTurnBegan(ulong clientId)
		{
			if (!IsServer || !Enabled.Value) return;

			// An overlay hands out turns of its own, and those are already staked in blood — charging for
			// one again would take a second payment for the same moment.
			if (GameMode.CurrentOverlay) return;

			var player = GameMode.FindSeatedPlayer(clientId);
			if (!player || !player.Data) return;

			ExchangeServer(player);
		}

		// Once per turn rather than in a loop up to some target: a player is asked for the price of one
		// answer, and how far they want to go is a decision they get to make again next time round.
		private void ExchangeServer(PokerPlayer player)
		{
			var data = player.Data;

			if (data.Chips > 0) return;
			if (!data.IsAlive || !data.CanAct) return;
			if (data.Health.Value - BloodSpent < MinimumHealthKept) return;

			data.ServerChangeHealth(-BloodSpent);
			data.ServerGainChips(MoneyGained);
		}
	}
}
