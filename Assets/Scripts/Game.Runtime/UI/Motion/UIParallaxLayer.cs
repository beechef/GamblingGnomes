using UnityEngine;

namespace Game.Runtime.UI.Motion
{
	// Slides with the nearest UIPointerTilt above it as a layer standing Depth in front of it would, without
	// leaving the plane: at rest it is exactly where it was placed. Owns this object's anchored position, so a
	// float or any other motion goes on a child.
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	public class UIParallaxLayer : MonoBehaviour
	{
		[Tooltip("How far in front of the tilt this layer stands, in UI units. Negative sits behind and slides the other way.")]
		[SerializeField] private float _depth = 20f;

		private RectTransform _rect;
		private UIPointerTilt _tilt;
		private Vector2 _rest;
		private bool _hasRest;

		private void OnEnable()
		{
			_rect = (RectTransform)transform;
			if (!_hasRest)
			{
				_rest = _rect.anchoredPosition;
				_hasRest = true;
			}

			_tilt = GetComponentInParent<UIPointerTilt>(true);
			if (!_tilt) return;

			_tilt.OnTilted += Apply;
			Apply(_tilt.Tilt);
		}

		private void OnDisable()
		{
			if (_tilt) _tilt.OnTilted -= Apply;
			_tilt = null;

			if (_hasRest) _rect.anchoredPosition = _rest;
		}

		private void Apply(Quaternion tilt)
		{
			var shift = tilt * new Vector3(0f, 0f, -_depth * _tilt.ParallaxStrength);
			_rect.anchoredPosition = _rest + new Vector2(shift.x, shift.y);
		}
	}
}
