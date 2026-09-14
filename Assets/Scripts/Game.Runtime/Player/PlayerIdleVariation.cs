using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Runtime.Player
{
	// Now and then, a fidget: a hand tapping the table, cards shifted in the grip. Which fidget belongs to
	// which pose is the animator's business — a trigger sends each resting state into its own — and this
	// only says when. Local on every client and never replicated: it is set dressing, and two screens
	// showing the same player drum their fingers a few seconds apart tell nobody anything.
	//
	// The trigger is set only while an animator is actually in one of the resting states, so it is
	// consumed the frame it is set and can never be left latched to fire later in the middle of something
	// else.
	public class PlayerIdleVariation : MonoBehaviour
	{
		[Header("Animators")]
		[Tooltip("Every rig the player can be drawn with. Each is asked separately, so a rig that is not resting is simply skipped.")]
		[SerializeField] private List<Animator> _animators = new();

		[Header("Variation")]
		[Tooltip("Trigger parameter that sends a resting state into its fidget.")]
		[SerializeField] private string _trigger = "IdleVariation";

		[Tooltip("Base-layer states a fidget may start from.")]
		[SerializeField] private List<string> _restingStates = new() { "Sit", "Sit_Peek" };

		[Tooltip("Seconds between fidgets, picked at random in this range each time.")]
		[SerializeField] private Vector2 _interval = new(6f, 12f);

		private readonly List<int> _restingHashes = new();

		private int _triggerHash;
		private CancellationTokenSource _cancellation;

		private void Awake()
		{
			_triggerHash = Animator.StringToHash(_trigger);

			foreach (var state in _restingStates) _restingHashes.Add(Animator.StringToHash(state));
		}

		private void OnEnable()
		{
			_cancellation = new CancellationTokenSource();
			_ = RunAsync(_cancellation.Token);
		}

		private void OnDisable()
		{
			_cancellation?.Cancel();
			_cancellation?.Dispose();
			_cancellation = null;
		}

		private async Awaitable RunAsync(CancellationToken ct)
		{
			try
			{
				while (!ct.IsCancellationRequested)
				{
					var min = Mathf.Max(0.1f, _interval.x);
					await Awaitable.WaitForSecondsAsync(Random.Range(min, Mathf.Max(min, _interval.y)), ct);

					Fidget();
				}
			}
			catch (OperationCanceledException) { }
		}

		private void Fidget()
		{
			foreach (var animator in _animators)
			{
				if (!animator || !animator.isActiveAndEnabled || animator.IsInTransition(0)) continue;

				var current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
				if (_restingHashes.Contains(current)) animator.SetTrigger(_triggerHash);
			}
		}
	}
}
