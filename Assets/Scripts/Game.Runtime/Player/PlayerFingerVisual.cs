using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Pieces of the model that come off one at a time, in an order somebody authored. Domain-agnostic on
	// purpose: what makes a finger fall is never this component's business — it is handed a count and draws
	// it, so anything that ever costs a player a finger gets the display for nothing.
	public class PlayerFingerVisual : MonoBehaviour
	{
		[Serializable]
		public struct Finger
		{
			[Tooltip("Read the list by, not used at runtime.")]
			public string Name;

			[Tooltip("Every mesh this one finger is cut into, across every rig — the owner's hand-only pair included, or a player keeps a finger only they can see.")]
			public GameObject[] Pieces;
		}

		[Header("Fingers")]
		[Tooltip("Lost in list order: the first entry is the first to go. A reorder is a behaviour change no compiler catches.")]
		[SerializeField] private List<Finger> _fingers = new();

		public int FingerCount => _fingers.Count;

		public int LostFingerCount => Mathf.Max(0, _lostCount);

		// Nothing has said how many are gone yet, so the first answer always draws — a model saved with a
		// finger switched off is put right rather than left the way it was found.
		private int _lostCount = -1;

		private void Awake() => SetLostFingerCount(0);

		public void SetLostFingerCount(int lostCount)
		{
			var lost = Mathf.Clamp(lostCount, 0, _fingers.Count);
			if (lost == _lostCount) return;

			_lostCount = lost;

			for (var i = 0; i < _fingers.Count; i++) SetAttached(_fingers[i], i >= lost);
		}

		// The renderer's own enabled flag belongs to PlayerVisual, which switches whole rigs on and off for
		// the owner and re-hangs materials on every skin change — a finger taken off there would be handed
		// back the next time either of those ran. Switching the object off composes with both instead.
		private static void SetAttached(Finger finger, bool attached)
		{
			if (finger.Pieces == null) return;

			foreach (var piece in finger.Pieces)
			{
				if (piece) piece.SetActive(attached);
			}
		}
	}
}
