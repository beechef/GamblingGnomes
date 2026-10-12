using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// A die whose sides are suits. Thrown at a spot, it bounces and tumbles there and comes to rest with the
	// asked suit facing up — the result is decided before the throw, the roll only shows it.
	public class PokerSuitDie : MonoBehaviour
	{
		[Serializable]
		private struct Face
		{
			public CardSuit Suit;

			[Tooltip("The face's outward direction in this object's space.")]
			public Vector3 LocalNormal;
		}

		[Tooltip("Every side of the die. A suit on several sides lands on any one of them.")]
		[SerializeField] private List<Face> _faces = new();

		[Header("Roll")]
		[Tooltip("Seconds the throw takes to reach the spot; the die spins on there for the rest of the roll.")]
		[Min(0.01f)]
		[SerializeField] private float _throwDuration = 0.7f;

		[Tooltip("Height of the first bounce, in metres.")]
		[Min(0f)]
		[SerializeField] private float _jumpPower = 0.12f;

		[Min(1)]
		[SerializeField] private int _bounces = 3;

		[Tooltip("Turns the die tumbles through per second of roll before it settles.")]
		[Min(0f)]
		[SerializeField] private float _turnsPerSecond = 1.5f;

		[SerializeField] private Ease _settleEase = Ease.OutCubic;

		private readonly List<Vector3> _matches = new();
		private Sequence _roll;

		// Rolls for exactly `duration`, coming to rest on the suit as it ends. No suit lands on any side: a screen
		// that may not know the result is shown none.
		public void RollTo(Vector3 landing, CardSuit? suit, float duration)
		{
			duration = Mathf.Max(0.01f, duration);
			var turns = Mathf.Max(1, Mathf.RoundToInt(_turnsPerSecond * duration));
			Stop();

			var normal = suit.HasValue ? NormalFor(suit.Value) : RandomNormal();
			var turn = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 4) * 90f + transform.eulerAngles.y, 0f);
			var rest = turn * Quaternion.FromToRotation(normal, Vector3.up);
			var start = transform.rotation;
			var axis = Vector3.Cross(Vector3.up, landing - transform.position);
			if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;
			axis.Normalize();

			_roll = DOTween.Sequence()
				.Join(transform.DOJump(landing, _jumpPower, _bounces, Mathf.Min(_throwDuration, duration)))
				.Join(DOVirtual.Float(0f, 1f, duration, t =>
					transform.rotation = Quaternion.Slerp(start, Quaternion.AngleAxis(360f * turns * (1f - t), axis) * rest, t)).SetEase(_settleEase))
				.SetLink(gameObject);
		}

		public void Stop() => _roll?.Kill();

		private Vector3 NormalFor(CardSuit suit)
		{
			_matches.Clear();
			foreach (var face in _faces)
			{
				if (face.Suit == suit) _matches.Add(face.LocalNormal);
			}

			return _matches.Count > 0 ? _matches[UnityEngine.Random.Range(0, _matches.Count)] : RandomNormal();
		}

		private Vector3 RandomNormal() => _faces.Count > 0 ? _faces[UnityEngine.Random.Range(0, _faces.Count)].LocalNormal : Vector3.up;
	}
}
