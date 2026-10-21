using System;
using System.Threading;
using Game.Runtime.Controller;
using Game.Runtime.UI.Button;
using Game.Runtime.UI.MainMenu;
using Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Matchmaking
{
	// What the player looks at for quick match: the mode first, then Play starts the search, the clock and the
	// count until the room is full, with Cancel to step back to the mode (Escape presses whichever cross is up). A failure stays up with its reason until
	// closed. The root stays active and toggles _content, because a failure can arrive after the popup has stepped aside for the
	// loading screen. Whoever opened it hears OnClosed when the player is back to choosing.
	public class UIMatchmakingPopup : MonoBehaviour
	{
		[Required]
		[SerializeField] private GameObject _content;

		[Required]
		[SerializeField] private TMP_Text _title;

		[Header("Mode")]
		[Tooltip("The mode picker and Play, shown before the search starts.")]
		[Required]
		[SerializeField] private GameObject _pickView;

		[Required]
		[SerializeField] private UIGameModePicker _picker;

		[Required]
		[SerializeField] private UIButton _playButton;

		[Header("Search")]
		[Tooltip("How long the search has run, mm:ss.")]
		[Required]
		[SerializeField] private TMP_Text _elapsed;

		[Tooltip("Stops the search and goes back to the mode.")]
		[Required]
		[SerializeField] private UIButton _stopButton;

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
		private bool _picking;
		private CancellationTokenSource _clock;

		public event Action OnClosed;

		// Raised whenever the popup comes up, including a failure arriving after the menu stepped aside, so
		// whoever holds it can bring the screen around it back.
		public event Action OnOpened;

		public bool IsOpen { get; private set; }

		// The content's own fade, if it has one; the content is switched off only once it has played.
		private UIPopInVisual _transition;

		private void Awake()
		{
			_transition = _content.GetComponentInChildren<UIPopInVisual>(true);
			_content.SetActive(false);
		}

		private void OnEnable()
		{
			_cancelButton.OnClick += HandleCancel;
			_closeButton.OnClick += Close;
			_playButton.OnClick += HandlePlay;
			_stopButton.OnClick += HandleStop;
		}

		private void OnDisable()
		{
			_stopButton.OnClick -= HandleStop;
			_playButton.OnClick -= HandlePlay;
			_closeButton.OnClick -= Close;
			_cancelButton.OnClick -= HandleCancel;
			StopClock();
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
			_picking = true;
			Show();
		}

		private void HandlePlay()
		{
			if (!_picking || !_picker.HasMode || !CanOpen) return;

			_picking = false;
			_matchmaking.StartMatchmaking(_picker.Selected);
			StartClock();
			Redraw();
		}

		private void HandleStop()
		{
			if (_matchmaking) _matchmaking.CancelMatchmaking();
			StopClock();
			_picking = true;
			Redraw();
		}

		private void StartClock()
		{
			StopClock();
			_clock = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
			_ = RunClockAsync(_clock.Token);
		}

		private void StopClock()
		{
			if (_clock == null) return;

			_clock.Cancel();
			_clock.Dispose();
			_clock = null;
		}

		private async Awaitable RunClockAsync(CancellationToken ct)
		{
			var started = Time.unscaledTime;

			try
			{
				while (!ct.IsCancellationRequested)
				{
					var seconds = Mathf.FloorToInt(Time.unscaledTime - started);
					_elapsed.text = $"{seconds / 60:00}:{seconds % 60:00}";
					await Awaitable.WaitForSecondsAsync(1f - (Time.unscaledTime - started) % 1f, ct);
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		private void HandleStateChanged(MatchmakingState state)
		{
			// Handing over to the session: the loading screen covers what comes next, and the menu has hidden
			// itself, so there is nothing to go back to.
			if (state == MatchmakingState.Starting)
			{
				StopClock();
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
			_picking = false;
			StopClock();
			_status.text = Localizer.Get(FailureKey(failure));
			Show();
		}

		private void Close()
		{
			_showingFailure = false;
			StopClock();
			Hide();
			OnClosed?.Invoke();
		}

		private void Show()
		{
			IsOpen = true;

			// Brought back mid-fade: switching off first cancels the fade and lets the pop-in play again.
			if (_content.activeSelf && _transition && _transition.IsHiding) _content.SetActive(false);
			_content.SetActive(true);

			OnOpened?.Invoke();
			Redraw();
		}

		private void Hide()
		{
			if (!IsOpen) return;
			IsOpen = false;

			if (_transition) _transition.Hide(SwitchOffIfStillHidden);
			else _content.SetActive(false);
		}

		private void SwitchOffIfStillHidden()
		{
			if (!IsOpen) _content.SetActive(false);
		}

		private void Redraw()
		{
			if (!IsOpen || !_matchmaking) return;

			var searching = !_picking && !_showingFailure;

			_pickView.SetActive(_picking);
			_cancelButton.gameObject.SetActive(searching);
			_closeButton.gameObject.SetActive(!searching);
			_elapsed.gameObject.SetActive(searching);
			_stopButton.gameObject.SetActive(searching);
			_status.gameObject.SetActive(!_picking);

			var waiting = searching && _matchmaking.State == MatchmakingState.Waiting;
			_count.gameObject.SetActive(waiting);

			if (_showingFailure)
			{
				_title.text = Localizer.Get(LocalizationKeys.Matchmaking.Failed);
				return;
			}

			_title.text = Localizer.Get(LocalizationKeys.Matchmaking.Title);
			_status.text = Localizer.Get(waiting ? LocalizationKeys.Matchmaking.LookingForPlayers : LocalizationKeys.Matchmaking.Searching);

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
