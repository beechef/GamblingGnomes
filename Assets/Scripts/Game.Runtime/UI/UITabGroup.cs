using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.UI
{
	// A row of tabs over a set of pages, exactly one page up. The tab last chosen stays current across a close
	// and reopen; before any choice it is the first.
	public class UITabGroup : MonoBehaviour
	{
		[Tooltip("Order matters: the first tab is current until another is chosen.")]
		[SerializeField] private List<UITab> _tabs = new();

		private UITab _current;

		private void OnEnable()
		{
			foreach (var tab in _tabs)
			{
				if (tab) tab.OnRequested += Select;
			}

			Select(_current ? _current : _tabs.Count > 0 ? _tabs[0] : null);
		}

		private void OnDisable()
		{
			foreach (var tab in _tabs)
			{
				if (tab) tab.OnRequested -= Select;
			}
		}

		public void Select(UITab current)
		{
			_current = current;

			foreach (var tab in _tabs)
			{
				if (tab) tab.SetCurrent(tab == current);
			}
		}
	}
}
