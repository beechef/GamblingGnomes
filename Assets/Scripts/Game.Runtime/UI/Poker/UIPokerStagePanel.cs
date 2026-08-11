using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// One panel per stage id. The router shows whichever matches the running stage, so a module adding
	// a stage only has to drop a panel in with the same id — no routing code to edit.
	public class UIPokerStagePanel : MonoBehaviour
	{
		[SerializeField] private string _stageId;

		[Tooltip("On, this panel answers to the overlay slot instead of the base stage slot.")]
		[SerializeField] private bool _isOverlay;

		public string StageId => _stageId;
		public bool IsOverlay => _isOverlay;

		public void SetVisible(bool visible)
		{
			if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
		}
	}
}
