using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
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
			[Tooltip("The id code triggers. Picked from PlayerActionIds so an entry can never answer to a name nothing asks for.")]
			[ValueDropdown(nameof(ActionIds))]
			public string Id;

			// Pickable but still typeable: a gesture is allowed to name a state the rigs have not been
			// given yet — PlayerActionAnimator skips a missing one — so the dropdown offers what exists
			// without locking out what is still being animated.
			[Tooltip("Animator state the id plays. Empty uses the id itself as the state name.")]
			[ValueDropdown(nameof(StateNames), AppendNextDrawer = true)]
			public string StateName;

			[Tooltip("Animator layer the state lives on. Gestures belong on a layer that hands back to empty when they finish, so they never fight the seat poses on the base layer.")]
			[MinValue(0)]
			public int Layer;

			[MinValue(0f)]
			public float CrossFade;
		}

		[Tooltip("Editor only: the controller the state dropdown reads. Nothing at runtime looks at it — the animator on the rig is whatever the prefab carries.")]
		[SerializeField] private RuntimeAnimatorController _stateSource;

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

		private static IEnumerable<string> ActionIds =>
			typeof(PlayerActionIds)
				.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
				.Where(field => field.IsLiteral && field.FieldType == typeof(string))
				.Select(field => (string)field.GetRawConstantValue());

		private IEnumerable<string> StateNames
		{
			get
			{
				var names = new List<string> { string.Empty };

#if UNITY_EDITOR
				if (_stateSource is UnityEditor.Animations.AnimatorController controller)
				{
					foreach (var layer in controller.layers)
					{
						CollectStateNames(layer.stateMachine, names);
					}
				}
#endif

				return names;
			}
		}

#if UNITY_EDITOR
		private static void CollectStateNames(UnityEditor.Animations.AnimatorStateMachine stateMachine, List<string> names)
		{
			if (!stateMachine) return;

			foreach (var state in stateMachine.states)
			{
				if (state.state && !names.Contains(state.state.name)) names.Add(state.state.name);
			}

			foreach (var child in stateMachine.stateMachines)
			{
				CollectStateNames(child.stateMachine, names);
			}
		}
#endif
	}
}
