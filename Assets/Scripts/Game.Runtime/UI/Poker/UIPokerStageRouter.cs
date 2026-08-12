using System.Collections.Generic;
using Unity.Collections;
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

			HideAll();
		}

		protected override void OnBind()
		{
			Data.StageId.OnValueChanged += HandleStageChanged;
			Data.OverlayStageId.OnValueChanged += HandleStageChanged;

			Refresh();
		}

		protected override void OnUnbind()
		{
			Data.StageId.OnValueChanged -= HandleStageChanged;
			Data.OverlayStageId.OnValueChanged -= HandleStageChanged;

			HideAll();
		}

		private void HandleStageChanged(FixedString32Bytes previous, FixedString32Bytes current) => Refresh();

		private void Refresh()
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

		private void HideAll()
		{
			foreach (var panel in _panels)
			{
				if (panel) panel.SetVisible(false);
			}
		}
	}
}
