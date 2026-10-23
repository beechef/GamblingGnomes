using Game.Runtime.Controller;
using Game.Runtime.Lobby;
using Localization;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Popup
{
	// Every way into a room that does not end at the table, and every table lost, is told to the player.
	public class UIConnectFailureBinder : MonoBehaviour
	{
		[Required]
		[SerializeField] private UIErrorPopup _popup;

		private void Start()
		{
			if (GameNetworkManager.Instance) GameNetworkManager.Instance.OnConnectFailed += HandleConnectFailed;
		}

		private void OnDestroy()
		{
			if (GameNetworkManager.Instance) GameNetworkManager.Instance.OnConnectFailed -= HandleConnectFailed;
		}

		private void HandleConnectFailed(ConnectFailure failure) => _popup.Show(Localizer.Get(MessageKey(failure)));

		private static string MessageKey(ConnectFailure failure) => failure switch
		{
			ConnectFailure.RoomFull => LocalizationKeys.Error.Connect.RoomFull,
			ConnectFailure.Timeout => LocalizationKeys.Error.Connect.Timeout,
			ConnectFailure.RoomGone => LocalizationKeys.Error.Connect.RoomGone,
			ConnectFailure.Unavailable => LocalizationKeys.Error.Connect.Unavailable,
			ConnectFailure.Disconnected => LocalizationKeys.Error.Connect.Disconnected,
			ConnectFailure.Kicked => LocalizationKeys.Error.Connect.Kicked,
			_ => LocalizationKeys.Error.Connect.Unknown
		};
	}
}
