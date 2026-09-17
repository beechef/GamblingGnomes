using UnityEngine;

namespace Game.Runtime.UI
{
	// A set of panels of which at most one is up. Showing one takes the others down, so two screens that
	// answer the same moment — a menu and the picker it opens — can never be drawn over each other, and
	// nobody showing a panel has to know what else might be open.
	//
	// A panel carrying a UIPopInVisual (on itself or its art) pops in when it is switched on and fades out
	// before it is switched off; one without simply switches. The group never animates anything itself — it
	// only waits for the panel's own transition before switching it off.
	//
	// Whatever decides when a panel is wanted lives on an object that stays active, or it would switch itself
	// off with the panel and never hear the next change.
	public class UIPanelStateGroup : MonoBehaviour
	{
		[Tooltip("The panels in the group. Each is switched on only while it is the current one.")]
		[SerializeField] private GameObject[] _panels;

		private UIPopInVisual[] _transitions;

		public GameObject Current { get; private set; }

		// Nothing has been shown yet, so nothing fades: the panels are simply put down.
		private void Awake()
		{
			_transitions = new UIPopInVisual[_panels.Length];

			for (var i = 0; i < _panels.Length; i++)
			{
				if (!_panels[i]) continue;

				_transitions[i] = _panels[i].GetComponentInChildren<UIPopInVisual>(true);
				_panels[i].SetActive(false);
			}
		}

		public void Show(GameObject panel)
		{
			Current = panel;

			for (var i = 0; i < _panels.Length; i++)
			{
				var candidate = _panels[i];
				if (!candidate) continue;

				var transition = _transitions != null ? _transitions[i] : null;

				if (candidate == panel)
				{
					// Brought back mid-fade: switching off first cancels the fade and lets the pop-in play again.
					if (candidate.activeSelf && transition && transition.IsHiding) candidate.SetActive(false);
					if (!candidate.activeSelf) candidate.SetActive(true);
					continue;
				}

				if (!candidate.activeSelf || (transition && transition.IsHiding)) continue;

				if (transition) transition.Hide(() => SwitchOffIfStillHidden(candidate));
				else candidate.SetActive(false);
			}
		}

		public void HideAll() => Show(null);

		public bool IsShowing(GameObject panel) => panel && Current == panel;

		private void SwitchOffIfStillHidden(GameObject panel)
		{
			if (panel && Current != panel) panel.SetActive(false);
		}
	}
}
