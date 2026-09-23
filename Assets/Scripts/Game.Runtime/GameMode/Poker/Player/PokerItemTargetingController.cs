using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Items;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// This client's side of playing an item that points at something, and of answering one somebody else
	// played. Owner-only. It walks the item's target steps through the pointer, builds the request, and sends
	// it once every step has an answer; what may be lit at each step is the item's own Accepts*, which the
	// server asks again.
	public class PokerItemTargetingController : NetworkBehaviour
	{
		[Header("References")]
		[Tooltip("The player's pointer. Found on the player when left empty.")]
		[SerializeField] private PokerTargetPointer _pointer;

		private readonly List<PokerItemTargetKind> _steps = new();

		private PokerPlayer _player;
		private PokerGameMode _gameMode;
		private PokerItemModule _module;

		private PokerItem _item;
		private int _stepIndex;
		private PokerItemUseRequest _request;
		private bool _answering;

		public bool IsTargeting => _item;
		public string Prompt { get; private set; }

		// Raised when targeting starts, moves to its next step, or ends.
		public event Action OnTargetingChanged;

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			_player = GetComponentInParent<PokerPlayer>();
			if (!_pointer && _player) _pointer = _player.GetComponentInChildren<PokerTargetPointer>(true);

			PokerGameMode.OnInstanceChanged += BindGameMode;
			BindGameMode(PokerGameMode.Instance);
		}

		public override void OnNetworkDespawn()
		{
			if (!IsOwner) return;

			PokerGameMode.OnInstanceChanged -= BindGameMode;
			BindGameMode(null);
		}

		private void BindGameMode(PokerGameMode gameMode)
		{
			if (_gameMode == gameMode) return;

			if (_gameMode && _gameMode.Data) _gameMode.Data.CurrentTurnClientId.OnValueChanged -= HandleTurnChanged;
			if (_module) _module.OnPendingResponseChanged -= HandlePendingResponseChanged;

			Cancel();
			StopAnswering();

			_gameMode = gameMode;
			_module = _gameMode ? _gameMode.FindModule<PokerItemModule>() : null;

			if (_gameMode && _gameMode.Data) _gameMode.Data.CurrentTurnClientId.OnValueChanged += HandleTurnChanged;
			if (_module) _module.OnPendingResponseChanged += HandlePendingResponseChanged;

			HandlePendingResponseChanged();
		}

		// Starts playing an item: straight away if it points at nothing, otherwise one step at a time.
		public void BeginUse(PokerItem item)
		{
			if (!IsOwner || !item || !_module || !_gameMode) return;

			Cancel();

			_item = item;
			_request = new PokerItemUseRequest(item.Type);
			_stepIndex = 0;
			item.CollectTargetSteps(_steps);

			if (_steps.Count == 0)
			{
				Send();
				return;
			}

			AskStep();
		}

		public void Cancel()
		{
			if (!_item) return;

			_item = null;
			Prompt = null;
			if (_pointer && !_answering) _pointer.End();

			OnTargetingChanged?.Invoke();
		}

		private void AskStep()
		{
			var kind = _steps[_stepIndex];
			var context = _module.ContextFor(_player);
			var item = _item;
			var self = _player;

			var query = new PokerTargetQuery();
			switch (kind)
			{
				case PokerItemTargetKind.Player:
					query.AcceptPlayer = target => item.AcceptsPlayer(context, target);
					break;

				case PokerItemTargetKind.OpponentCard:
					query.AcceptHoleCard = (target, slot) => target != self && item.AcceptsOpponentCard(context, target, slot);
					break;

				case PokerItemTargetKind.OwnCard:
					query.AcceptHoleCard = (target, slot) => target == self && item.AcceptsOwnCard(context, slot);
					break;

				default:
					query.AcceptBoardCard = slot => item.AcceptsBoardCard(context, slot);
					break;
			}

			Prompt = item.GetTargetPrompt(kind);
			if (_pointer) _pointer.Begin(query, HandleStepPicked);

			OnTargetingChanged?.Invoke();
		}

		private void HandleStepPicked(PokerTarget target)
		{
			if (!_item) return;

			var seat = target.Player && target.Player.Data ? target.Player.Data.SeatIndex.Value : -1;

			_request = _steps[_stepIndex] switch
			{
				PokerItemTargetKind.Player => _request.WithTarget(seat),
				PokerItemTargetKind.OpponentCard => _request.WithTarget(seat).WithCard(target.Slot),
				PokerItemTargetKind.OwnCard => _request.WithOwnCard(target.Slot),
				_ => _request.WithCard(target.Slot)
			};

			_stepIndex++;
			if (_stepIndex < _steps.Count) AskStep();
			else Send();
		}

		private void Send()
		{
			_gameMode.SubmitModuleCommandRPC(PokerItemModule.UseCommand, _request.Pack());
			Cancel();
		}

		// The turn moving on takes any half-aimed item with it.
		private void HandleTurnChanged(ulong previous, ulong current)
		{
			if (current != OwnerClientId) Cancel();
		}

		private void HandlePendingResponseChanged()
		{
			var pending = _module ? _module.PendingResponse.Value : PokerItemResponse.None;

			if (pending.IsPending && pending.ResponderClientId == OwnerClientId) StartAnswering(pending);
			else StopAnswering();
		}

		private void StartAnswering(PokerItemResponse pending)
		{
			if (_answering || !_pointer || !_module.TryGetItem(pending.Item, out var item)) return;

			var requester = PokerPlayer.Find(pending.RequesterClientId);
			var context = _module.ContextFor(requester);
			var self = _player;

			_answering = true;
			_pointer.Begin(new PokerTargetQuery
			{
				AcceptHoleCard = (target, slot) => target == self && item.AcceptsResponseCard(context, self, slot)
			}, HandleAnswerPicked);
		}

		private void StopAnswering()
		{
			if (!_answering) return;

			_answering = false;
			if (_pointer) _pointer.End();
		}

		private void HandleAnswerPicked(PokerTarget target)
		{
			if (!_answering || !_module) return;

			_module.RespondWithCardRPC(target.Slot);
			StopAnswering();
		}
	}
}
