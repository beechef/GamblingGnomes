using Game.Runtime.UI.Button;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Config
{
	public class UIMatchConfigBoolRow : UIMatchConfigRow
	{
		[Header("Toggle")]
		[SerializeField] private UIButton _toggleButton;
		[SerializeField] private TextMeshProUGUI _valueLabel;

		private void Awake()
		{
			if (_toggleButton) _toggleButton.OnClick += HandleToggle;
		}

		private void OnDestroy()
		{
			if (_toggleButton) _toggleButton.OnClick -= HandleToggle;
		}

		protected override void OnRefresh()
		{
			var on = Entry != null && Access != null && Access.GetValue(Entry) >= 0.5f;

			if (_valueLabel) _valueLabel.text = on ? "On" : "Off";
			if (_toggleButton) _toggleButton.IsInteractable = IsEditable;
		}

		private void HandleToggle()
		{
			if (!IsEditable || Entry == null || Access == null) return;

			var on = Access.GetValue(Entry) >= 0.5f;

			Access.SetValue(Entry, on ? 0f : 1f);
			Refresh();
		}
	}
}
