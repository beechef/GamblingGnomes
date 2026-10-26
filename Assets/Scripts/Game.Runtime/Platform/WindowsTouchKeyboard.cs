using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Debug = UnityEngine.Debug;

namespace Game.Runtime.Platform
{
	// Windows' own touch keyboard (TabTip), the one handhelds running plain Windows show (ROG Ally, Legion Go).
	// It types into whatever has keyboard focus, like a physical keyboard, so the caller keeps its field open.
	// Shown through ITipInvocation, the only way to open it on demand; TabTip is started first if it is not
	// running, since the COM object only exists while it is. False on any other platform, and on any failure.
	public static class WindowsTouchKeyboard
	{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
		private const string TabTipPath = @"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe";
		private const string WindowClass = "IPTip_Main_Window";
		private const int GwlStyle = -16;
		private const uint WsVisible = 0x10000000;
		private const uint WsDisabled = 0x08000000;

		[ComImport, Guid("37c994e7-432b-4834-a2f7-dce1f13b834b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		private interface ITipInvocation
		{
			void Toggle(IntPtr hwnd);
		}

		[ComImport, Guid("4ce576fa-83dc-4f88-951c-9d0782b4e376")]
		private class UIHostNoLaunch
		{
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr FindWindow(string className, string windowName);

		[DllImport("user32.dll")]
		private static extern uint GetWindowLong(IntPtr hwnd, int index);

		[DllImport("user32.dll")]
		private static extern IntPtr GetDesktopWindow();

		// ponytail: reads the classic TabTip window's style; where a Windows build hides it differently this
		// answers false and Show may toggle an open keyboard shut. Swap for the UI Automation check if seen.
		private static bool IsShown
		{
			get
			{
				var window = FindWindow(WindowClass, null);
				if (window == IntPtr.Zero) return false;

				var style = GetWindowLong(window, GwlStyle);
				return (style & WsVisible) != 0 && (style & WsDisabled) == 0;
			}
		}

		public static bool Show()
		{
			try
			{
				if (IsShown) return true;

				if (FindWindow(WindowClass, null) == IntPtr.Zero)
				{
					if (!File.Exists(TabTipPath)) return false;
					Process.Start(new ProcessStartInfo(TabTipPath) { UseShellExecute = true });
				}

				Toggle();
				return true;
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"[WindowsTouchKeyboard] Could not show the touch keyboard: {exception.Message}");
				return false;
			}
		}

		public static void Hide()
		{
			try
			{
				if (IsShown) Toggle();
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"[WindowsTouchKeyboard] Could not hide the touch keyboard: {exception.Message}");
			}
		}

		private static void Toggle()
		{
			var invocation = (ITipInvocation)new UIHostNoLaunch();
			try
			{
				invocation.Toggle(GetDesktopWindow());
			}
			finally
			{
				Marshal.ReleaseComObject(invocation);
			}
		}
#else
		public static bool Show() => false;

		public static void Hide()
		{
		}
#endif
	}
}
