using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker
{
	// The room, handed to whoever needs to point at it. A ScriptableObject cannot serialize a scene
	// object, so an effect asset asking for "the room" has to be given it by something living in the
	// scene — this, which registers and unregisters in its own lifecycle the way PokerSeat and GameCamera
	// already do rather than being searched for.
	public class PokerScenery : MonoBehaviour
	{
		[Serializable]
		public class Group
		{
			[Tooltip("Which part of the room this is. Picked on both sides, so the asset asking for it cannot name a group the scene does not offer.")]
			[SerializeField] private PokerSceneryGroup _group;

			[Tooltip("The objects that make it up. Roots are enough: what is done to them is the effect's business.")]
			[SerializeField] private List<Transform> _members = new();

			public PokerSceneryGroup Id => _group;
			public IReadOnlyList<Transform> Members => _members;
		}

		public static PokerScenery Instance { get; private set; }

		public static event Action OnInstanceChanged;

		[SerializeField] private List<Group> _groups = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			Instance = null;
			OnInstanceChanged = null;
		}

		private void OnEnable()
		{
			if (Instance && Instance != this)
			{
				Debug.LogWarning($"{nameof(PokerScenery)}: a second one is in the scene, keeping the first.", this);
				return;
			}

			Instance = this;
			OnInstanceChanged?.Invoke();
		}

		private void OnDisable()
		{
			if (Instance != this) return;

			Instance = null;
			OnInstanceChanged?.Invoke();
		}

		// Nothing found is nothing added rather than an empty group being an error: a scene that has not
		// been given a starfield yet should play without one, not refuse to run.
		public void Collect(PokerSceneryGroup group, List<Transform> into)
		{
			if (into == null) return;

			foreach (var entry in _groups)
			{
				if (entry == null || entry.Id != group) continue;

				foreach (var member in entry.Members)
				{
					if (member) into.Add(member);
				}
			}
		}
	}
}
