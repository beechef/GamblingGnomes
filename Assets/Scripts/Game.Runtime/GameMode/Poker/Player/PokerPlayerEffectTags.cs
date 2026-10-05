using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// The effects a player is under right now, as tags anybody can list. Derived on every peer from replicated
	// state the moment it changes, so a tag can never disagree with the rule it stands for: a card shown to the
	// table, folding forbidden by an item. A new effect is an enum value plus its line in Collect.
	public class PokerPlayerEffectTags : MonoBehaviour
	{
		private readonly List<PokerPlayerEffect> _active = new();
		private readonly List<PokerPlayerEffect> _scratch = new();

		private PokerPlayer _player;
		private PokerPlayerData _data;
		private PokerGameMode _gameMode;
		private bool _started;

		public IReadOnlyList<PokerPlayerEffect> Active => _active;

		public event Action OnChanged;

		public bool Has(PokerPlayerEffect effect) => _active.Contains(effect);

		private void Awake()
		{
			_player = GetComponentInParent<PokerPlayer>();
		}

		// From Start, not the first OnEnable: a spawned body is enabled before its synced values arrive.
		private void Start()
		{
			_started = true;
			Bind();
		}

		private void OnEnable()
		{
			if (_started) Bind();
		}

		private void OnDisable() => Unbind();

		private void Bind()
		{
			Unbind();

			_data = _player ? _player.Data : null;
			if (_data)
			{
				_data.OnHoleCardPresentationChanged += Refresh;
				_data.OnStateChanged += Refresh;
			}

			PokerGameMode.OnInstanceChanged += HandleGameModeChanged;
			BindGameMode(PokerGameMode.Instance);
		}

		private void Unbind()
		{
			BindGameMode(null);
			PokerGameMode.OnInstanceChanged -= HandleGameModeChanged;

			if (_data)
			{
				_data.OnStateChanged -= Refresh;
				_data.OnHoleCardPresentationChanged -= Refresh;
			}

			_data = null;
		}

		private void HandleGameModeChanged(PokerGameMode gameMode) => BindGameMode(gameMode);

		private void BindGameMode(PokerGameMode gameMode)
		{
			if (_gameMode)
			{
				_gameMode.OnActionRulesChanged -= Refresh;
				if (_gameMode.Data) _gameMode.Data.StageId.OnValueChanged -= HandleStageChanged;
			}

			_gameMode = gameMode;

			if (_gameMode)
			{
				_gameMode.OnActionRulesChanged += Refresh;
				if (_gameMode.Data) _gameMode.Data.StageId.OnValueChanged += HandleStageChanged;
			}

			Refresh();
		}

		// Whether an item's fold lock covers this moment hangs on which stage is running.
		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();

		private void Refresh()
		{
			_scratch.Clear();
			if (_data) Collect(_scratch);

			if (SameAsActive(_scratch)) return;

			_active.Clear();
			_active.AddRange(_scratch);
			OnChanged?.Invoke();
		}

		private void Collect(List<PokerPlayerEffect> effects)
		{
			if (_data.ShownHoleCards.Value != 0 && !_data.HandRevealed.Value) effects.Add(PokerPlayerEffect.CardShown);
			if (_data.IsInHand && _gameMode && !_gameMode.IsActionAllowed(_data, PokerActionType.Fold)) effects.Add(PokerPlayerEffect.FoldLocked);
		}

		private bool SameAsActive(List<PokerPlayerEffect> effects)
		{
			if (effects.Count != _active.Count) return false;

			for (var i = 0; i < effects.Count; i++)
			{
				if (effects[i] != _active[i]) return false;
			}

			return true;
		}
	}
}
