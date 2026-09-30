using Game.Runtime.Controller;
using Game.Runtime.UI.Button;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Invite
{
	// Opens Steam's friend picker on the room this player is in. Shown only while there is a Steam room to
	// ask anybody into; what happens on the friend's side is GameNetworkManager.AcceptInvite.
	public class UIInviteFriendsButton : MonoBehaviour
	{
		[Required]
		[SerializeField] private UIButton _button;

		[Tooltip("Switched off while there is no room to invite into. A child, so this keeps listening while hidden.")]
		[Required]
		[SerializeField] private GameObject _content;

		private GameNetworkManager _network;

		private void Reset()
		{
			_button = GetComponentInChildren<UIButton>(true);
		}

		private void OnEnable()
		{
			if (_button) _button.OnClick += HandleClick;
		}

		private void OnDisable()
		{
			if (_button) _button.OnClick -= HandleClick;
		}

		// Another object's singleton, so bound from Start rather than OnEnable.
		private void Start()
		{
			_network = GameNetworkManager.Instance;

			if (_network)
			{
				_network.OnHostStarted += Refresh;
				_network.OnLobbyEnter += Refresh;
				_network.OnGameLeft += Refresh;
			}

			Refresh();
		}

		private void OnDestroy()
		{
			if (!_network) return;

			_network.OnGameLeft -= Refresh;
			_network.OnLobbyEnter -= Refresh;
			_network.OnHostStarted -= Refresh;
		}

		private void Refresh()
		{
			if (_content) _content.SetActive(_network && _network.CanInviteFriends);
		}

		private void HandleClick()
		{
			if (_network) _network.InviteFriends();
		}
	}
}
