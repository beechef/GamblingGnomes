using System;
using Game.Runtime.Controller;
using Game.Runtime.UI.Button;
using Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Matchmaking
{
	// What the player looks at while quick match runs: the search, then the count until the room is full,
	// with Cancel (and Escape) to walk out. A failure stays up with its reason until closed. The root stays
	// active and toggles _content, because a failure can arrive after the popup has stepped aside for the
	// loading screen. Whoever opened it hears OnClosed when the player is back to choosing.
	public class UIMatchmakingPopup : MonoBehaviour
	{
		[Required]
		[SerializeField] private GameObject _content;

		[Required]
		[SerializeField] private TMP_Text _title;

		[Required]
		[SerializeField] private TMP_Text _status;

		[Tooltip("Who is in the room so far, under the status; shown only while waiting for it to fill.")]
		[Required]
		[SerializeField] private TMP_Text _count;

		[Tooltip("Colour of the members already in, against the target.")]
		[SerializeField] private Color _joinedColor = new(0.788f, 0.243f, 0.251f);

		[Required]
		[SerializeField] private UIButton _cancelButton;

		[Required]
		[SerializeField] private UIButton _closeButton;

		private MatchmakingController _matchmaking;
		private bool _showingFailure;

		public event Action OnClosed;

		private void Awake() => _content.SetActive(false);

		private void OnEnable()
		{
			_cancelButton.OnClick += HandleCancel;
			_closeButton.OnClick += Close;
		}

		private void OnDisable()
		{
			_closeButton.OnClick -= Close;
			_cancelButton.OnClick -= HandleCancel;
		}

		private void Start()
		{
			_matchmaking = MatchmakingController.Instance;
			if (!_matchmaking)
			{
				Debug.LogWarning($"{nameof(UIMatchmakingPopup)}: no {nameof(MatchmakingController)} in Bootstrap; quick match cannot open.", this);
				return;
			}

			_matchmaking.OnStateChanged += HandleStateChanged;
			_matchmaking.OnMembersChanged += Redraw;
			_matchmaking.OnFailed += HandleFailed;
			Localizer.OnLocaleChanged += Redraw;
		}

		private void OnDestroy()
		{
			Localizer.OnLocaleChanged -= Redraw;
			UIEscapeStack.Remove(HandleCancel);
			UIEscapeStack.Remove(Close);

			if (!_matchmaking) return;

			_matchmaking.OnFailed -= HandleFailed;
			_matchmaking.OnMembersChanged -= Redraw;
			_matchmaking.OnStateChanged -= HandleStateChanged;
		}

		public bool CanOpen => _matchmaking && _matchmaking.State == MatchmakingState.Idle;

		public void Open()
		{
			if (!CanOpen) return;

			_showingFailure = false;
			Show();
			_matchmaking.StartMatchmaking();
		}

		private void HandleStateChanged(MatchmakingState state)
		{
			// Handing over to the session: the loading screen covers what comes next, and the menu has hidden
			// itself, so there is nothing to go back to.
			if (state == MatchmakingState.Starting)
			{
				Hide();
				return;
			}

			Redraw();
		}

		private void HandleCancel()
		{
			if (_matchmaking) _matchmaking.CancelMatchmaking();
			Close();
		}

		private void HandleFailed(MatchmakingFailure failure)
		{
			_showingFailure = true;
			_status.text = Localizer.Get(FailureKey(failure));
			Show();
		}

		private void Close()
		{
			_showingFailure = false;
			Hide();
			OnClosed?.Invoke();
		}

		private void Show()
		{
			_content.SetActive(true);
			Redraw();
		}

		private void Hide()
		{
			UIEscapeStack.Remove(HandleCancel);
			UIEscapeStack.Remove(Close);
			_content.SetActive(false);
		}

		private void Redraw()
		{
			if (!_content.activeSelf || !_matchmaking) return;

			_cancelButton.gameObject.SetActive(!_showingFailure);
			_closeButton.gameObject.SetActive(_showingFailure);

			UIEscapeStack.Remove(_showingFailure ? HandleCancel : Close);
			UIEscapeStack.Push(_showingFailure ? Close : HandleCancel);

			var waiting = !_showingFailure && _matchmaking.State == MatchmakingState.Waiting;
			_count.gameObject.SetActive(waiting);

			if (_showingFailure)
			{
				_title.text = Localizer.Get(LocalizationKeys.Matchmaking.Failed);
				return;
			}

			_title.text = Localizer.Get(LocalizationKeys.Matchmaking.Title);
			_status.text = Localizer.Get(LocalizationKeys.Matchmaking.Searching);

			if (waiting)
			{
				_count.text = $"<color=#{ColorUtility.ToHtmlStringRGB(_joinedColor)}>{_matchmaking.MemberCount}</color>/{_matchmaking.TargetPlayers}";
			}
		}

		private static string FailureKey(MatchmakingFailure failure) => failure switch
		{
			MatchmakingFailure.Unavailable => LocalizationKeys.Matchmaking.Error.Unavailable,
			MatchmakingFailure.CreateFailed => LocalizationKeys.Matchmaking.Error.Create,
			MatchmakingFailure.LobbyLost => LocalizationKeys.Matchmaking.Error.Lost,
			_ => LocalizationKeys.Matchmaking.Error.Connect
		};
	}
}
