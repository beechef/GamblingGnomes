using UnityEngine;

namespace Game.Runtime.UI
{
	// A set of panels of which at most one is up. Showing one takes the others down, so two screens that
	// answer the same moment — a menu and the picker it opens — can never be drawn over each other, and
	// nobody showing a panel has to know what else might be open.
	//
	// It only switches the panels' GameObjects. Whatever decides when a panel is wanted lives on an object
	// that stays active, or it would switch itself off with the panel and never hear the next change.
	public class UIPanelStateGroup : MonoBehaviour
	{
		[Tooltip("The panels in the group. Each is switched on only while it is the current one.")]
		[SerializeField] private GameObject[] _panels;

		public GameObject Current { get; private set; }

		private void Awake() => HideAll();

		public void Show(GameObject panel)
		{
			Current = panel;

			foreach (var candidate in _panels)
			{
				if (!candidate) continue;

				var active = candidate == panel;
				if (candidate.activeSelf != active) candidate.SetActive(active);
			}
		}

		public void HideAll() => Show(null);

		public bool IsShowing(GameObject panel) => panel && Current == panel;
	}
}
