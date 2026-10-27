using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.UI.Settings
{
	// A settings row is one stop for the pad: its Selectable navigates up and down only, and left/right are
	// handed to whatever the row holds (a stepper's arrows, a slider's value).
	public class UISettingRow : MonoBehaviour, IMoveHandler
	{
		public event Action<int> OnStepped;

		public void OnMove(AxisEventData eventData)
		{
			if (eventData.moveDir == MoveDirection.Left) OnStepped?.Invoke(-1);
			else if (eventData.moveDir == MoveDirection.Right) OnStepped?.Invoke(1);
		}
	}
}
