using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Props
{
	// A prop that burns down once lit: its animator plays the burn state, whose clip is authored one second long and
	// slowed to last the duration, then it shrinks away and is destroyed. Only a lit copy burns (Ignite); the
	// template it was copied from never does.
	public class PropBurnOut : MonoBehaviour
	{
		[Required]
		[SerializeField] private Animator _animator;

		[Tooltip("State played when lit; its clip runs one second, so the animator's speed is 1 / duration.")]
		[ValueDropdown(nameof(StateNames))]
		[SerializeField] private string _burnState = "Smoke";

		[Tooltip("Seconds from lit to burnt down.")]
		[Min(0.01f)]
		[SerializeField] private float _duration = 20f;

		[Tooltip("Seconds it takes to shrink away once burnt down.")]
		[Min(0f)]
		[SerializeField] private float _shrinkDuration = 0.3f;

		[SerializeField] private Ease _shrinkEase = Ease.InBack;

		private Sequence _sequence;
		private bool _lit;

		public static event Action<PropBurnOut> OnLit;
		public static event Action<PropBurnOut> OnBurntOut;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			OnLit = null;
			OnBurntOut = null;
		}

		private void OnDestroy()
		{
			_sequence?.Kill();
			if (_lit) OnBurntOut?.Invoke(this);
		}

		public void Ignite()
		{
			_sequence?.Kill();

			if (_animator)
			{
				_animator.speed = 1f / _duration;
				_animator.Play(_burnState, 0, 0f);
			}

			_sequence = DOTween.Sequence()
				.AppendInterval(_duration)
				.Append(transform.DOScale(Vector3.zero, _shrinkDuration).SetEase(_shrinkEase))
				.OnComplete(() => Destroy(gameObject))
				.SetLink(gameObject);

			if (_lit) return;

			_lit = true;
			OnLit?.Invoke(this);
		}

		private IEnumerable<string> StateNames
		{
			get
			{
				var names = new List<string>();

#if UNITY_EDITOR
				if (_animator && _animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController controller)
				{
					foreach (var state in controller.layers[0].stateMachine.states)
					{
						names.Add(state.state.name);
					}
				}
#endif

				return names;
			}
		}
	}
}
