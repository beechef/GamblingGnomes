using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI
{
	// Lays its children out as a fan: each turned by the same step about one shared point below them, the
	// way a hand of cards opens in a fist. Turning each child about its own middle would splay tops one way
	// and bottoms the other; turning them about a pivot underneath keeps the bottoms close and lets the
	// outer ones ride lower, which is what reads as a fan.
	//
	// Children are sized here rather than by their own content — a card has a fixed shape — and ordered
	// left to right in sibling order, so a later child overlaps the one before it.
	[AddComponentMenu("Layout/UI Fan Layout Group")]
	public class UIFanLayoutGroup : LayoutGroup
	{
		[Tooltip("Size every child is given.")]
		[SerializeField] private Vector2 _childSize = new(110f, 160f);

		[Tooltip("Degrees between neighbouring children. The whole spread comes from this and the pivot drop.")]
		[SerializeField] private float _angleStep = 6f;

		[Tooltip("How far below the children's middle the shared pivot sits, in UI units. Further down is a flatter arc with more room between children.")]
		[MinValue(1f)]
		[SerializeField] private float _pivotDrop = 900f;

		[Tooltip("How far the fan is open: 0 has every child lying on the first child's pose, 1 is the full spread. Tweened to open the fan from the left.")]
		[PropertyRange(0f, 1f)]
		[SerializeField] private float _openness = 1f;

		public float Openness
		{
			get => _openness;
			set
			{
				value = Mathf.Clamp01(value);
				if (Mathf.Approximately(_openness, value)) return;

				_openness = value;
				SetDirty();
			}
		}

		public override void CalculateLayoutInputHorizontal()
		{
			base.CalculateLayoutInputHorizontal();

			var width = SpanX() * 2f + _childSize.x + padding.horizontal;
			SetLayoutInputForAxis(width, width, -1f, 0);
		}

		public override void CalculateLayoutInputVertical()
		{
			var height = _childSize.y + Sag() + padding.vertical;
			SetLayoutInputForAxis(height, height, -1f, 1);
		}

		public override void SetLayoutHorizontal()
		{
		}

		public override void SetLayoutVertical()
		{
			m_Tracker.Clear();

			var count = rectChildren.Count;
			var centre = rectTransform.rect.center + new Vector2((padding.left - padding.right) * 0.5f, (padding.bottom - padding.top) * 0.5f);

			// Raised by half the sag, so the arc as a whole sits in the middle of the rect.
			var lift = Sag() * 0.5f;

			for (var i = 0; i < count; i++)
			{
				var child = rectChildren[i];
				// Every child sweeps out from the first one's pose, so opening reads as the fan spreading from the left.
				var angle = Mathf.Lerp(AngleOf(0, count), AngleOf(i, count), _openness);
				var offset = OffsetOf(angle);

				m_Tracker.Add(this, child, DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition |
					DrivenTransformProperties.SizeDelta | DrivenTransformProperties.Pivot | DrivenTransformProperties.Rotation);

				var middle = new Vector2(0.5f, 0.5f);
				child.anchorMin = middle;
				child.anchorMax = middle;
				child.pivot = middle;
				child.sizeDelta = _childSize;

				// Anchored to the rect's centre, so the rect's own offset from its pivot is added back.
				child.anchoredPosition = centre - rectTransform.rect.center + offset + new Vector2(0f, lift);
				child.localRotation = Quaternion.Euler(0f, 0f, angle);
			}
		}

		// The leftmost child leans left: a positive angle turns counter-clockwise.
		private float AngleOf(int index, int count) => ((count - 1) * 0.5f - index) * _angleStep;

		private Vector2 OffsetOf(float angle)
		{
			var radians = angle * Mathf.Deg2Rad;
			return new Vector2(-Mathf.Sin(radians) * _pivotDrop, (Mathf.Cos(radians) - 1f) * _pivotDrop);
		}

		private float SpanX() => rectChildren.Count > 1 ? Mathf.Abs(OffsetOf(AngleOf(0, rectChildren.Count)).x) : 0f;

		private float Sag() => rectChildren.Count > 1 ? -OffsetOf(AngleOf(0, rectChildren.Count)).y : 0f;
	}
}
