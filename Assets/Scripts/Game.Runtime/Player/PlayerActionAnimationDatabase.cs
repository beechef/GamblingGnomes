using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Every gesture a player can be seen making, by name. Code asks for an id and the database says which
	// animator state that is on this project's rigs — so a new action animation is a new entry here and a
	// new state in the controller, never a new code path.
	[CreateAssetMenu(fileName = "PlayerActionAnimationDatabase", menuName = "Game/Player/Action Animation Database")]
	public class PlayerActionAnimationDatabase : ScriptableObject
	{
		[Serializable]
		public struct Entry
		{
			public string Id;

			[Tooltip("Animator state the id plays. Empty uses the id itself as the state name.")]
			public string StateName;

			[Tooltip("Animator layer the state lives on. Gestures belong on a layer that hands back to empty when they finish, so they never fight the seat poses on the base layer.")]
			public int Layer;

			public float CrossFade;
		}

		[SerializeField] private List<Entry> _entries = new();

		public bool TryFind(string id, out Entry entry)
		{
			foreach (var candidate in _entries)
			{
				if (candidate.Id != id) continue;

				entry = candidate;
				return true;
			}

			entry = default;
			return false;
		}
	}
}
