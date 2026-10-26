using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Runtime.Controller
{
	// A pad made in code, for tests and the editor's Virtual Gamepad window: it is a real Gamepad device fed
	// state events, so the input module, actions and InputSchemeController hear it exactly as they hear hardware.
	// A PlayStation one is a DualShockGamepad, which InputSchemeController draws with the PlayStation set.
	public sealed class VirtualGamepad : IDisposable
	{
		public Gamepad Device { get; }

		private GamepadState _state;

		private VirtualGamepad(Gamepad device)
		{
			Device = device;
		}

		public static VirtualGamepad Create(GamepadKind kind)
		{
			Gamepad device = kind == GamepadKind.PlayStation
				? InputSystem.AddDevice<DualShockGamepad>("VirtualGamepad_PlayStation")
				: InputSystem.AddDevice<Gamepad>("VirtualGamepad_Xbox");

			return new VirtualGamepad(device);
		}

		public void Hold(GamepadButton button) => SetButton(button, true);

		public void Release(GamepadButton button) => SetButton(button, false);

		public void SetLeftStick(Vector2 value)
		{
			_state.leftStick = value;
			Queue();
		}

		public void SetRightStick(Vector2 value)
		{
			_state.rightStick = value;
			Queue();
		}

		public void ReleaseAll()
		{
			_state = default;
			Queue();
		}

		private void SetButton(GamepadButton button, bool pressed)
		{
			_state = _state.WithButton(button, pressed);
			Queue();
		}

		private void Queue()
		{
			if (Device != null && Device.added) InputSystem.QueueStateEvent(Device, _state);
		}

		public void Dispose()
		{
			if (Device != null && Device.added) InputSystem.RemoveDevice(Device);
		}
	}
}
