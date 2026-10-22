using System;
using Game.Runtime.UI.Button;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI
{
	// One tab of a UITabGroup: the button that asks for it and the page it shows. The group decides which tab
	// is current; the tab only draws that answer.
	public class UITab : MonoBehaviour
	{
		[Header("References")]
		[Required]
		[SerializeField] private UIButton _button;

		[Required]
		[SerializeField] private GameObject _page;

		[Tooltip("Drawn while the tab is not the current one (the paper inside the frame); the current tab shows the frame's black.")]
		[SerializeField] private GameObject _idleArt;

		[SerializeField] private TMP_Text _label;

		[Header("Look")]
		[SerializeField] private Color _currentLabelColor = Color.white;
		[SerializeField] private Color _idleLabelColor = Color.black;

		public event Action<UITab> OnRequested;

		private void Reset()
		{
			_button = GetComponent<UIButton>();
		}

		private void OnEnable()
		{
			if (_button) _button.OnClick += HandleClick;
		}

		private void OnDisable()
		{
			if (_button) _button.OnClick -= HandleClick;
		}

		public void SetCurrent(bool current)
		{
			if (_page && _page.activeSelf != current) _page.SetActive(current);
			if (_idleArt) _idleArt.SetActive(!current);
			if (_label) _label.color = current ? _currentLabelColor : _idleLabelColor;
		}

		private void HandleClick() => OnRequested?.Invoke(this);
	}
}
