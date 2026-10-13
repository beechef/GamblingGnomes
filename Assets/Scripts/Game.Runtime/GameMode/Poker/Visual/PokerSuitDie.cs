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

		[Header("Models")]
		[Tooltip("The die with suits on its sides, shown to a screen that may see the card it decides.")]
		[SerializeField] private GameObject _suitModel;

		[Tooltip("The die with question marks, shown to every screen that may not: it never tells the table the suit.")]
		[SerializeField] private GameObject _secretModel;

		[Header("Throw")]
		[Tooltip("Seconds the throw takes to reach the spot, tumbling; the die lands standing on an edge.")]
		[Min(0.01f)]
		[SerializeField] private float _throwDuration = 0.35f;

		[Tooltip("Height of the first bounce, in metres.")]
		[Min(0f)]
		[SerializeField] private float _jumpPower = 0.08f;

		[Min(1)]
		[SerializeField] private int _bounces = 1;

		[Tooltip("Turns the die tumbles through in the air.")]
		[Min(0)]
		[SerializeField] private int _throwTurns = 2;

		[Header("Spin")]
		[Tooltip("Turns per second the die spins on its edge as it lands; it slows to a stop by the spin's ease.")]
		[Min(0f)]
		[SerializeField] private float _turnsPerSecond = 8f;

		[SerializeField] private Ease _spinEase = Ease.OutQuad;

		[Header("Tip")]
		[Tooltip("Seconds the die takes to fall off its edge onto the result face.")]
		[Min(0.01f)]
		[SerializeField] private float _tipDuration = 0.25f;

		[SerializeField] private Ease _tipEase = Ease.InQuad;

		private readonly List<Vector3> _matches = new();
		private Sequence _roll;
		private float _halfSize;

		private void Awake()
		{
			var filter = _suitModel ? _suitModel.GetComponent<MeshFilter>() : null;
			_halfSize = filter && filter.sharedMesh ? filter.sharedMesh.bounds.extents.x * _suitModel.transform.localScale.x : 0.03f;
		}

		public void ShowSecret(bool secret)
		{
			if (_suitModel) _suitModel.SetActive(!secret);
			if (_secretModel) _secretModel.SetActive(secret);
		}

		// Rolls for exactly `duration`, `landing` being the spot on the table it comes to rest on: thrown there tumbling, it
		// spins on one edge, slows, and falls onto the suit as the roll ends. No suit lands on any side, on the
		// secret die: a screen that may not know the result is shown none.
		public void RollTo(Vector3 landing, CardSuit? suit, float duration)
		{
			ShowSecret(!suit.HasValue);
			Stop();

			var throwDuration = Mathf.Min(_throwDuration, duration * 0.3f);
			var tipDuration = Mathf.Min(_tipDuration, duration * 0.3f);
			var spinDuration = Mathf.Max(0.01f, duration - throwDuration - tipDuration);
			var turns = Mathf.Max(1, Mathf.RoundToInt(_turnsPerSecond * spinDuration));

			var normal = suit.HasValue ? NormalFor(suit.Value) : RandomNormal();
			var rest = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 4) * 90f + transform.eulerAngles.y, 0f) * Quaternion.FromToRotation(normal, Vector3.up);

			// Tipped 45 degrees about one bottom edge it stands on that edge, its centre straight above it.
			var side = rest * EdgeAxis(normal);
			var edgeAxis = Vector3.Cross(side, Vector3.up);
			var tilt = Quaternion.AngleAxis(45f, edgeAxis);
			var centre = landing + Vector3.up * _halfSize;
			var edge = centre - (Vector3.up + side) * _halfSize;
			var onEdge = tilt * rest;
			var spinCentre = edge + Vector3.up * (_halfSize * Mathf.Sqrt(2f));

			var start = transform.rotation;
			var throwAxis = Vector3.Cross(Vector3.up, spinCentre - transform.position);
			if (throwAxis.sqrMagnitude < 0.0001f) throwAxis = Vector3.right;
			throwAxis.Normalize();

			_roll = DOTween.Sequence()
				.Append(transform.DOJump(spinCentre, _jumpPower, _bounces, throwDuration))
				.Join(DOVirtual.Float(0f, 1f, throwDuration, t =>
					transform.rotation = Quaternion.Slerp(start, Quaternion.AngleAxis(360f * _throwTurns * (1f - t), throwAxis) * onEdge, t)))
				.Append(DOVirtual.Float(0f, 360f * turns, spinDuration, angle =>
					transform.rotation = Quaternion.AngleAxis(angle, Vector3.up) * onEdge).SetEase(_spinEase))
				.Append(DOVirtual.Float(45f, 0f, tipDuration, angle =>
				{
					var turn = Quaternion.AngleAxis(angle, edgeAxis);
					transform.SetPositionAndRotation(edge + turn * (centre - edge), turn * rest);
				}).SetEase(_tipEase))
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

		// A local axis lying along the face that lands up, so the edge it spins on is one of the cube's.
		private static Vector3 EdgeAxis(Vector3 normal)
		{
			var axis = Mathf.Abs(normal.x) < 0.5f ? Vector3.right : Vector3.forward;
			return Vector3.ProjectOnPlane(axis, normal).normalized;
		}

		private Vector3 RandomNormal() => _faces.Count > 0 ? _faces[UnityEngine.Random.Range(0, _faces.Count)].LocalNormal : Vector3.up;
	}
}
