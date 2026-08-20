using Game.Runtime.GameMode.Config;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Config
{
	public abstract class UIMatchConfigRow : MonoBehaviour
	{
		[Header("Row")]
		[SerializeField] private TextMeshProUGUI _label;

		protected MatchConfigEntry Entry { get; private set; }
		protected IMatchConfigValueAccess Access { get; private set; }
		protected bool IsEditable { get; private set; }

		// Re-applies the whole look on every bind: a freshly instantiated row is wearing whatever the
		// prefab was saved with, and a change-guard has nothing to guard on a first draw.
		public void Bind(MatchConfigEntry entry, IMatchConfigValueAccess access)
		{
			Entry = entry;
			Access = access;

			if (_label) _label.text = entry?.Label ?? string.Empty;

			OnBind();
			Refresh();
		}

		public void SetEditable(bool editable)
		{
			IsEditable = editable;

			OnEditableChanged();
			Refresh();
		}

		public void Refresh() => OnRefresh();

		protected virtual void OnBind() { }

		protected virtual void OnEditableChanged() { }

		protected abstract void OnRefresh();
	}
}
