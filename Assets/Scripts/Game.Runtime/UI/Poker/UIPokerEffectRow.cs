using System;
using System.Collections.Generic;
using Game.Runtime.GameMode.Poker.Player;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// The effects a player is under, drawn as a row of icons over their head: one per tag on their
	// PokerPlayerEffectTags, in the tags' order. An effect with no icon here is skipped. With nothing to draw
	// the row is switched off, so whatever is stacked above it settles down onto the head.
	public class UIPokerEffectRow : MonoBehaviour
	{
		[Serializable]
		private struct EffectIcon
		{
			public PokerPlayerEffect Effect;
			public Sprite Icon;
		}

		[SerializeField] private PokerPlayerEffectTags _tags;

		[Tooltip("Shown while at least one icon is drawn. A child, so this row keeps listening while hidden; out of the stack's layout while off.")]
		[SerializeField] private GameObject _content;

		[Tooltip("Auto-layout row the icons go in.")]
		[SerializeField] private RectTransform _row;

		[Tooltip("One effect (UI_PokerEffectIcon).")]
		[SerializeField] private UIPokerEffectIcon _entryPrefab;

		[SerializeField] private EffectIcon[] _icons;

		private readonly List<UIPokerEffectIcon> _entries = new();

		private void OnEnable()
		{
			if (!_tags) return;

			_tags.OnChanged += Rebuild;
			Rebuild();
		}

		private void OnDisable()
		{
			if (_tags) _tags.OnChanged -= Rebuild;
		}

		// Views already made are re-bound rather than rebuilt, so the row never flashes.
		private void Rebuild()
		{
			var used = 0;

			foreach (var effect in _tags.Active)
			{
				var icon = IconOf(effect);
				if (!icon || !_entryPrefab || !_row) continue;

				if (used == _entries.Count) _entries.Add(Instantiate(_entryPrefab, _row));

				var entry = _entries[used++];
				entry.gameObject.SetActive(true);
				entry.Bind(icon);
			}

			for (var i = used; i < _entries.Count; i++) _entries[i].gameObject.SetActive(false);

			if (_content) _content.SetActive(used > 0);
		}

		private Sprite IconOf(PokerPlayerEffect effect)
		{
			foreach (var entry in _icons)
			{
				if (entry.Effect == effect) return entry.Icon;
			}

			return null;
		}
	}
}
