using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Motion;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The round's turn order down the left edge: the deal first, popped as the cards go out and as each street
	// opens, then everyone playing the match in seat order from whoever opens the hand, the player whose turn
	// it is popped out with their name once the deal's pop has gone. Read off replicated state, so every screen
	// draws the same column.
	public class UIPokerTurnOrder : UIPokerView
	{
		[Header("Entries")]
		[Required]
		[SerializeField] private UIPokerTurnOrderEntry _entryPrefab;

		[Tooltip("Auto-layout column the entries are laid out in, top to bottom.")]
		[Required]
		[SerializeField] private RectTransform _entryParent;

		[Header("Deal")]
		[Tooltip("The first icon in the column, kept above the entries: the deal comes before anyone's turn. Punched as a deal begins and as each street opens.")]
		[SerializeField] private UIPunchScaleMotion _dealIcon;

		[Tooltip("How long a turn's pop waits after the deal icon's, when a street opens straight into a turn.")]
		[MinValue(0f)]
		[SerializeField] private float _turnAfterDealDelay = 0.4f;

		private readonly List<UIPokerTurnOrderEntry> _entries = new();
		private readonly List<PokerPlayer> _order = new();
		private readonly List<PokerPlayer> _watched = new();

		private float _turnHeldUntil;

		protected override void OnBind()
		{
			PokerPlayer.OnRegistryChanged += HandleRegistryChanged;
			Data.HandOpenerClientId.OnValueChanged += HandleClientIdChanged;
			Data.CurrentTurnClientId.OnValueChanged += HandleClientIdChanged;
			Data.Phase.OnValueChanged += HandlePhaseChanged;
			Data.StreetNumber.OnValueChanged += HandleStreetNumberChanged;

			WatchPlayers();
			Rebuild();
		}

		protected override void OnUnbind()
		{
			UnwatchPlayers();

			Data.StreetNumber.OnValueChanged -= HandleStreetNumberChanged;
			Data.Phase.OnValueChanged -= HandlePhaseChanged;
			Data.CurrentTurnClientId.OnValueChanged -= HandleClientIdChanged;
			Data.HandOpenerClientId.OnValueChanged -= HandleClientIdChanged;
			PokerPlayer.OnRegistryChanged -= HandleRegistryChanged;
		}

		private void HandleRegistryChanged()
		{
			WatchPlayers();
			Rebuild();
		}

		private void HandleClientIdChanged(ulong previous, ulong current) => Rebuild();
		private void HandleInMatchChanged(bool previous, bool current) => Rebuild();

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current)
		{
			if (current == PokerPhase.Dealing) PlayDeal();
		}

		private void HandleStreetNumberChanged(int previous, int current)
		{
			if (current > 0) PlayDeal();
		}

		// The street and its first turn arrive together, in either order, so a turn already popped is replayed
		// behind the deal.
		private void PlayDeal()
		{
			if (_dealIcon) _dealIcon.Play();

			_turnHeldUntil = Time.unscaledTime + _turnAfterDealDelay;

			foreach (var entry in _entries)
			{
				if (entry.isActiveAndEnabled) entry.ReplayCurrent(_turnAfterDealDelay);
			}
		}

		// Every player, not only the seated: who is in the match, where they sit and whether they are still
		// standing all live on each player's own object.
		private void WatchPlayers()
		{
			UnwatchPlayers();

			foreach (var player in PokerPlayer.All)
			{
				if (!player || !player.Data) continue;

				player.Data.OnStateChanged += Rebuild;
				player.Data.InMatch.OnValueChanged += HandleInMatchChanged;
				if (player.Identity) player.Identity.OnIdentityChanged += Rebuild;
				_watched.Add(player);
			}
		}

		private void UnwatchPlayers()
		{
			foreach (var player in _watched)
			{
				if (!player) continue;

				if (player.Identity) player.Identity.OnIdentityChanged -= Rebuild;
				if (!player.Data) continue;

				player.Data.InMatch.OnValueChanged -= HandleInMatchChanged;
				player.Data.OnStateChanged -= Rebuild;
			}

			_watched.Clear();
		}

		// Views already made are re-bound rather than rebuilt, so an icon mid-pop is not thrown away.
		private void Rebuild()
		{
			if (!IsBound || !_entryPrefab || !_entryParent) return;

			CollectOrder();

			for (var i = 0; i < _order.Count; i++)
			{
				if (i == _entries.Count) _entries.Add(Instantiate(_entryPrefab, _entryParent));

				var entry = _entries[i];
				entry.gameObject.SetActive(true);
				entry.Bind(_order[i]);
				entry.SetCurrent(_order[i].ClientId == Data.CurrentTurnClientId.Value, Mathf.Max(0f, _turnHeldUntil - Time.unscaledTime));
			}

			for (var i = _order.Count; i < _entries.Count; i++) _entries[i].gameObject.SetActive(false);

			if (_dealIcon) _dealIcon.transform.SetAsFirstSibling();
		}

		// Seat order, turned so the hand's opener comes first: the order every street walks the table in.
		private void CollectOrder()
		{
			_order.Clear();

			foreach (var player in PokerPlayer.All)
			{
				if (player && player.Data && player.Data.IsSeated && GameMode.IsPlayingThisMatch(player.Data)) _order.Add(player);
			}

			_order.Sort((left, right) => left.Data.SeatIndex.Value.CompareTo(right.Data.SeatIndex.Value));

			var opener = _order.FindIndex(player => player.ClientId == Data.HandOpenerClientId.Value);
			if (opener <= 0) return;

			var head = _order.GetRange(0, opener);
			_order.RemoveRange(0, opener);
			_order.AddRange(head);
		}
	}
}
