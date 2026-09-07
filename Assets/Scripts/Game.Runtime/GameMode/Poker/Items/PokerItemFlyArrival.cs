using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Items
{
	// Handed over: it starts where the winner had it and travels across the table. The arc is what makes
	// it read as a thing being pushed at somebody rather than a thing teleporting — and the source is
	// the whole point of this one, so with nothing to come from it falls back to appearing in place
	// rather than flying out of the table's middle.
	[CreateAssetMenu(fileName = "ItemArrival_Fly", menuName = "Game/Poker/Item Arrivals/Fly")]
	public class PokerItemFlyArrival : PokerItemArrival
	{
		[Header("Flight")]
		[Tooltip("How high it arcs on the way over, measured straight up in world space.")]
		[SerializeField] private float _arc = 0.25f;

		[SerializeField] private Ease _ease = Ease.InOutQuad;

		[Header("Spin")]
		[Tooltip("Degrees it turns on the way. Zero flies it flat.")]
		[SerializeField] private float _spin = 180f;

		protected override void OnPlay(Transform item, Vector3 resting, Transform source)
		{
			if (!source)
			{
				item.localPosition = resting;
				return;
			}

			var parent = item.parent;
			var from = parent ? parent.InverseTransformPoint(source.position) : resting;
			var lift = parent ? parent.InverseTransformVector(Vector3.up) * _arc : Vector3.up * _arc;

			item.localPosition = from;

			// Driven by an explicit float rather than a path tween: the arc has to be added on top of a
			// straight line in the plate's own space, and a world-space path would tilt with the seat.
			DOVirtual.Float(0f, 1f, Duration, t =>
				{
					if (!item) return;

					item.localPosition = Vector3.Lerp(from, resting, t) + lift * Mathf.Sin(t * Mathf.PI);
				})
				.SetEase(_ease)
				.OnComplete(() => { if (item) item.localPosition = resting; });

			if (_spin != 0f) item.DOLocalRotate(new Vector3(0f, _spin, 0f), Duration, RotateMode.LocalAxisAdd).SetEase(_ease);
		}
	}
}
