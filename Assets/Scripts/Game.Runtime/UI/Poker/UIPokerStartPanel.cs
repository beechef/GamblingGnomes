using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Button;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The host's seat is the dealer's chair: only they get the button, and only once they are sitting
	// at the table with enough company to deal.
	public class UIPokerStartPanel : UIPokerView
	{
		[Header("References")]
		[SerializeField] private GameObject _panel;
		[SerializeField] private UIButton _startButton;
		[SerializeField] private TextMeshProUGUI _hintLabel;

		// Every seat this panel is counting. It reads blood and money off other players' objects, so it has
		// to hear those change — the seated list only says who is at the table, never what they are
		// carrying. A match ending restores everyone at once, which arrives as a change on each of them and
		// on nothing this panel was listening to: the button stayed dead until somebody stood up and sat
		// down again, because that was the only thing that made the list say something.
		private readonly List<PokerPlayerData> _watched = new();

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			if (_startButton) _startButton.OnClick += HandleStartClicked;

			Data.Phase.OnValueChanged += HandlePhaseChanged;
			GameMode.OnSeatedPlayersChanged += HandleSeatedPlayersChanged;
			LocalData.SeatIndex.OnValueChanged += HandleSeatChanged;

			WatchSeatedPlayers();
			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_startButton) _startButton.OnClick -= HandleStartClicked;

			UnwatchSeatedPlayers();

			Data.Phase.OnValueChanged -= HandlePhaseChanged;
			GameMode.OnSeatedPlayersChanged -= HandleSeatedPlayersChanged;
			LocalData.SeatIndex.OnValueChanged -= HandleSeatChanged;

			if (_panel) _panel.SetActive(false);
		}

		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();
		private void HandleSeatChanged(int previous, int current) => Refresh();

		private void HandleSeatedPlayersChanged()
		{
			WatchSeatedPlayers();
			Refresh();
		}

		private void WatchSeatedPlayers()
		{
			UnwatchSeatedPlayers();

			foreach (var player in GameMode.SeatedPlayers)
			{
				if (!player || !player.Data) continue;

				player.Data.OnStateChanged += Refresh;
				_watched.Add(player.Data);
			}
		}

		private void UnwatchSeatedPlayers()
		{
			foreach (var data in _watched)
			{
				if (data) data.OnStateChanged -= Refresh;
			}

			_watched.Clear();
		}

		private void Refresh()
		{
			var isHost = NetworkManager.Singleton && NetworkManager.Singleton.IsHost;
			var visible = isHost && LocalData.IsSeated && Data.Phase.Value == PokerPhase.Waiting;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			// Counted the way the server counts it, so the button is never offered for a table that would
			// refuse to deal — a seat filled by a player with nothing left to bet is not company.
			var readyCount = GameMode.FundedPlayerCount;
			var required = GameMode.Rules ? GameMode.Rules.MinimumPlayersToStart : 2;
			var canStart = readyCount >= required;

			if (_startButton) _startButton.IsInteractable = canStart;

			if (_hintLabel)
			{
				_hintLabel.text = canStart
					? $"{readyCount} players ready"
					: $"Waiting for players ({readyCount}/{required})";
			}
		}

		private void HandleStartClicked()
		{
			if (GameMode) GameMode.RequestStartGameRPC();
		}
	}
}
