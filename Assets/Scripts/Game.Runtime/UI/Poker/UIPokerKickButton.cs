using System;
using System.Collections.Generic;
using Game.Runtime.Controller;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Button;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The cross over another player's head that removes them from the room. Shown only to the host, only in the
	// waiting room before a match, like the bot buttons: taking a body out mid-hand strands the turn.
	public class UIPokerKickButton : UIPokerView
	{
		[Tooltip("The player this cross removes: the body the tag hangs over.")]
		[Required]
		[SerializeField] private PokerPlayer _player;

		[Tooltip("World-space canvas the cross is raycast through; given the game camera to aim with.")]
		[Required]
		[SerializeField] private Canvas _canvas;

		[Tooltip("Switched off whenever the cross is not offered. A child, so this keeps listening while hidden.")]
		[Required]
		[SerializeField] private GameObject _content;

		[Required]
		[SerializeField] private UIButton _button;

		private static readonly List<UIPokerKickButton> OfferedList = new();

		// Every cross currently offered, for the HUD's pad picker (UIPokerKickPicker).
		public static IReadOnlyList<UIPokerKickButton> Offered => OfferedList;

		public static event Action OnOfferedChanged;

		public PokerPlayer Player => _player;

		private PlayerManager _players;
		private bool _picked;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			OfferedList.Clear();
			OnOfferedChanged = null;
		}

		// Held for the unsubscribe: the table can be destroyed before this body during a shutdown.
		private PokerGameData _data;

		private void Awake()
		{
			if (_content) _content.SetActive(false);
		}

		protected override void OnBind()
		{
			_players = PlayerManager.Instance;
			if (_canvas) _canvas.worldCamera = _canvas.rootCanvas.worldCamera = GameCamera.Main;

			if (_players) _players.Players.OnListChanged += HandlePlayersChanged;
			_data = Data;
			_data.Phase.OnValueChanged += HandlePhaseChanged;
			_button.OnClick += HandleKick;

			Refresh();
		}

		protected override void OnUnbind()
		{
			_button.OnClick -= HandleKick;
			_data.Phase.OnValueChanged -= HandlePhaseChanged;
			if (_players) _players.Players.OnListChanged -= HandlePlayersChanged;

			_players = null;
			_data = null;

			SetOffered(false);
		}

		private void HandlePlayersChanged(NetworkListEvent<NetworkObjectReference> change) => Refresh();
		private void HandlePhaseChanged(PokerPhase previous, PokerPhase current) => Refresh();

		private void Refresh()
		{
			var shown = _players && GameMode && GameMode.IsHost && GameMode.IsInWaitingRoom && _player && _player.IsSpawned && _player != LocalPlayer;
			SetOffered(shown);
		}

		private void SetOffered(bool offered)
		{
			if (!offered) SetPicked(false);
			if (_content) _content.SetActive(offered);

			if (offered == OfferedList.Contains(this)) return;

			if (offered) OfferedList.Add(this);
			else OfferedList.Remove(this);

			OnOfferedChanged?.Invoke();
		}

		// Marked by the pad picker: the cross pops as if hovered, the body wears the red warning outline and the
		// name lights like an aimed item's target.
		public void SetPicked(bool picked)
		{
			if (_picked == picked) return;

			_picked = picked;
			_button.IsSelected = picked;

			if (!_player) return;

			if (_player.Visual) _player.Visual.SetLocalWarned(picked);
			if (_player.NameTag) _player.NameTag.SetLocalHighlighted(picked);
		}

		public void Kick() => HandleKick();

		private void HandleKick()
		{
			if (_players && _player && GameMode && GameMode.IsInWaitingRoom) _players.ServerKick(_player.ClientId);
		}
	}
}
