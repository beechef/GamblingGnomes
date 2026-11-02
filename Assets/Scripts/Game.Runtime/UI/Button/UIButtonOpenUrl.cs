using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Button
{
	[RequireComponent(typeof(UIButton))]
	public class UIButtonOpenUrl : MonoBehaviour
	{
		[Required]
		[SerializeField] private string _url;

		private UIButton _button;

		private void Awake() => _button = GetComponent<UIButton>();

		private void OnEnable() => _button.OnClick += OpenUrl;

		private void OnDisable() => _button.OnClick -= OpenUrl;

		private void OpenUrl() => Application.OpenURL(_url);
	}
}
