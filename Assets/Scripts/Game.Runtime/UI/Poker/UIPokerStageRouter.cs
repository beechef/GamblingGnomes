using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	public class UIPokerStageRouter : UIPokerView
	{
		[Header("Panels")]
		[Tooltip("Left empty, every UIPokerStagePanel underneath this object is routed.")]
		[SerializeField] private List<UIPokerStagePanel> _panels = new();

		private void Awake()
		{
			if (_panels.Count == 0) GetComponentsInChildren(true, _panels);

			foreach (var panel in _panels)
			{
				if (panel) panel.SetVisible(false);
			}
		}

		protected override void OnTick()
		{
			var stageId = Data.StageId.Value.ToString();
			var overlayId = Data.OverlayStageId.Value.ToString();

			foreach (var panel in _panels)
			{
				if (!panel) continue;

				var id = panel.IsOverlay ? overlayId : stageId;
				panel.SetVisible(!string.IsNullOrEmpty(id) && panel.StageId == id);
			}
		}

		protected override void OnUnbind()
		{
			foreach (var panel in _panels)
			{
				if (panel) panel.SetVisible(false);
			}
		}
	}
}
