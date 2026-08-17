using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Button
{
	// Narrows an Image's clickable area to where its art actually is. A rectangle is the right hit region
	// for a rectangular button and the wrong one for a diamond, an arrow or anything with cut corners —
	// there, the gaps between the shapes still swallow the click, and two neighbouring buttons overlap in
	// the corner nobody drew. uGUI answers this with alphaHitTestMinimumThreshold; this component is where
	// the number is set, and where a sprite that cannot support it says so instead of throwing mid-click.
	[RequireComponent(typeof(Image))]
	public class UIAlphaHitArea : MonoBehaviour
	{
		[Header("Threshold")]
		[Tooltip("Pixels fainter than this stop counting as the button. Zero accepts the whole rectangle, which is the same as not having this component.")]
		[PropertyRange(0f, 1f)]
		[SerializeField] private float _threshold = 0.1f;

		private Image _image;

		private void Awake()
		{
			_image = GetComponent<Image>();
		}

		private void OnEnable()
		{
			Apply();
		}

		// Re-applied rather than set once: the value lives on the Image, and an Image handed a new sprite
		// keeps the threshold but not necessarily a readable texture behind it.
		public void Apply()
		{
			if (!_image) _image = GetComponent<Image>();
			if (!_image) return;

			// Left untouched rather than reset on failure: writing the property at all is what throws on an
			// unreadable texture, so a zero here would trip the very exception the check is avoiding.
			if (!IsUsable()) return;

			_image.alphaHitTestMinimumThreshold = _threshold;
		}

		// uGUI reads the texture's pixels to answer a raycast, which a texture imported without Read/Write
		// refuses — and it refuses by throwing, from inside the raycast, on the first click. Caught here so
		// the failure names the asset to fix instead of surfacing as a dead button.
		private bool IsUsable()
		{
			if (_threshold <= 0f) return false;

			var sprite = _image.sprite;
			if (!sprite)
			{
				Debug.LogError($"{name}: alpha hit area needs a sprite on the Image.", this);
				return false;
			}

			if (!sprite.texture.isReadable)
			{
				Debug.LogError($"{name}: '{sprite.texture.name}' needs Read/Write enabled in its import settings for the alpha hit area to work.", this);
				return false;
			}

			return true;
		}

#if UNITY_EDITOR
		private void OnValidate()
		{
			if (!isActiveAndEnabled) return;

			Apply();
		}
#endif
	}
}
