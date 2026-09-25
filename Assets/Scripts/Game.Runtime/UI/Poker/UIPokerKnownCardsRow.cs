using System.Collections.Generic;
using Game.Runtime.GameMode.Poker;
using Game.Runtime.GameMode.Poker.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The cards of one player this screen knows before the showdown. Over somebody else's head: what they
	// turned up for the table and what this player peeked at. In the local player's own items panel: which of
	// their cards are out, and who has seen them.
	//
	// Binds itself: a row inside a player's body is about that player, a row outside one (the HUD) is about
	// the local player. Either way what this screen knows is read from the local player's own item knowledge,
	// which only they receive.
	public class UIPokerKnownCardsRow : MonoBehaviour
	{
		[Tooltip("Shown while there is at least one card to show. A child, so this row keeps listening while hidden.")]
		[SerializeField] private GameObject _content;

		[Tooltip("Auto-layout row the entries go in.")]
		[SerializeField] private RectTransform _row;

		[Tooltip("One exposed card (UI_PokerKnownCard).")]
		[SerializeField] private UIPokerKnownCardEntry _entryPrefab;

		[Header("Labels")]
		[SerializeField] private string _shownLabel = "SHOWN";
		[SerializeField] private string _peekedLabel = "PEEKED";

		private readonly List<UIPokerKnownCardEntry> _entries = new();

		private PokerPlayer _bodyOwner;
		private PokerPlayer _subject;
		private PokerPlayer _local;
		private bool _started;

		private void Awake()
		{
			_bodyOwner = GetComponentInParent<PokerPlayer>();
			if (_content) _content.SetActive(false);
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

			PokerPlayer.OnLocalPlayerChanged += HandleLocalPlayerChanged;

			_local = PokerPlayer.Local;
			_subject = _bodyOwner ? _bodyOwner : _local;

			if (_subject && _subject.Data)
			{
				_subject.Data.OnHoleCardPresentationChanged += Rebuild;
				_subject.Data.OnHoleCardsChanged += HandleHoleCardsChanged;
			}

			if (_local && _local.ItemKnowledge) _local.ItemKnowledge.OnKnowledgeChanged += Rebuild;

			Rebuild();
		}

		private void Unbind()
		{
			PokerPlayer.OnLocalPlayerChanged -= HandleLocalPlayerChanged;

			if (_local && _local.ItemKnowledge) _local.ItemKnowledge.OnKnowledgeChanged -= Rebuild;

			if (_subject && _subject.Data)
			{
				_subject.Data.OnHoleCardsChanged -= HandleHoleCardsChanged;
				_subject.Data.OnHoleCardPresentationChanged -= Rebuild;
			}

			_subject = null;
			_local = null;
		}

		private void HandleLocalPlayerChanged(PokerPlayer player)
		{
			if (isActiveAndEnabled) Bind();
		}

		private void HandleHoleCardsChanged(NetworkListEvent<CardData> change) => Rebuild();

		private void Rebuild()
		{
			var used = 0;

			if (_subject && _subject.Data)
			{
				var data = _subject.Data;

				for (var slot = 0; slot < data.CardCount; slot++)
				{
					if (data.IsHoleCardShown(slot)) Place(ref used, data.HoleCards[slot], _shownLabel);
				}

				var knowledge = _local ? _local.ItemKnowledge : null;
				if (knowledge && _subject == _local)
				{
					foreach (var exposed in knowledge.ExposedCards) Place(ref used, exposed.Card, PokerPlayer.NameOf(exposed.OtherClientId));
				}
				else if (knowledge)
				{
					foreach (var known in knowledge.KnownCards)
					{
						if (known.OtherClientId == _subject.ClientId) Place(ref used, known.Card, _peekedLabel);
					}
				}
			}

			for (var i = used; i < _entries.Count; i++) _entries[i].gameObject.SetActive(false);

			if (_content) _content.SetActive(used > 0);
		}

		// Views already made are re-bound rather than rebuilt, so the row never flashes.
		private void Place(ref int used, CardData card, string label)
		{
			if (!_entryPrefab || !_row) return;

			if (used == _entries.Count) _entries.Add(Instantiate(_entryPrefab, _row));

			var entry = _entries[used++];
			entry.gameObject.SetActive(true);
			entry.Bind(card, label);
		}
	}
}
