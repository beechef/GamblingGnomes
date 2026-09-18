using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Props
{
	// Picks one of several takes for a prop's animator to play — which dance a transformed mushroom breaks into.
	// Rolled once each time the prop is switched on and written as an int the controller branches on, so two
	// props of one kind side by side need not move in step. Local and cosmetic: what a prop looks like is
	// already a per-screen matter, and a roll nobody decides is not worth replicating.
	public class PropAnimatorRandomIndex : MonoBehaviour
	{
		[Required]
		[SerializeField] private Animator _animator;

		[Tooltip("Int parameter on the animator. A controller without it is skipped, so the prop works before its art does.")]
		[SerializeField] private string _parameter = "DanceIndex";

		[Tooltip("How many takes the controller branches between; the index is rolled from 0 up to this, exclusive.")]
		[MinValue(1)]
		[SerializeField] private int _count = 4;

		private int _hash;
		private bool _hasParameter;

		private void Awake()
		{
			if (!_animator) _animator = GetComponentInChildren<Animator>(true);

			_hash = Animator.StringToHash(_parameter);

			if (!_animator) return;

			foreach (var parameter in _animator.parameters)
			{
				if (parameter.nameHash != _hash || parameter.type != AnimatorControllerParameterType.Int) continue;

				_hasParameter = true;
				break;
			}
		}

		private void OnEnable()
		{
			if (_hasParameter && _animator) _animator.SetInteger(_hash, Random.Range(0, Mathf.Max(1, _count)));
		}
	}
}
