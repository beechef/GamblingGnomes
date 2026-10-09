using Game.Runtime.Audio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	// One click sound for every button and checkbox on the canvas, so no prefab carries its own.
	public class UIButtonClickSound : MonoBehaviour
	{
		[Required]
		[SerializeField] private AudioEvent _click;

		private void OnEnable()
		{
			UIButton.OnAnyClicked += HandleButtonClicked;
			UISelectableHover.OnToggleFlipped += HandleToggleFlipped;
		}

		private void OnDisable()
		{
			UISelectableHover.OnToggleFlipped -= HandleToggleFlipped;
			UIButton.OnAnyClicked -= HandleButtonClicked;
		}

		private void HandleButtonClicked(UIButton button) => Play(button.transform.position);

		private void HandleToggleFlipped(UISelectableHover toggle) => Play(toggle.transform.position);

		private void Play(Vector3 position)
		{
			if (AudioManager.Instance) AudioManager.Instance.PlayOneShot(_click, position);
		}
	}
}
