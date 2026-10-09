using Game.Runtime.Audio;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// Sits on the button or toggle it voices, so each prefab picks its own sound; an empty event stays silent.
	public class UIButtonClickSound : MonoBehaviour
	{
		[SerializeField] private AudioEvent _click;

		private UIButton _button;
		private UISelectableHover _toggle;

		private void Awake()
		{
			TryGetComponent(out _button);
			TryGetComponent(out _toggle);
		}

		private void OnEnable()
		{
			if (_button) _button.OnPress += Play;
			if (_toggle) _toggle.OnToggleFlipped += Play;
		}

		private void OnDisable()
		{
			if (_toggle) _toggle.OnToggleFlipped -= Play;
			if (_button) _button.OnPress -= Play;
		}

		private void Play()
		{
			if (_click && AudioManager.Instance) AudioManager.Instance.PlayOneShot(_click, transform.position);
		}
	}
}
