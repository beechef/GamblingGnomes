using System;
using System.Collections.Generic;
using System.Threading;
using DG.Tweening;
using Game.Runtime.UI.Progress;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.UI.Loading
{
	// The screen shown while something the player cannot see is happening. It knows nothing about what
	// that something is — whoever starts the wait calls Show, reports progress and calls Hide, so the
	// same screen covers a scene load, a lobby join or a hand being dealt.
	[RequireComponent(typeof(CanvasGroup))]
	public class UILoadingScreen : MonoBehaviour
	{
		[Header("References")]
		[Required]
		[SerializeField] private CanvasGroup _group;

		[Required]
		[SerializeField] private UIProgressBar _bar;

		[Tooltip("Optional. Left empty, the screen keeps whatever the prefab says.")]
		[SerializeField] private TextMeshProUGUI _titleLabel;

		[Required]
		[Tooltip("Switched off while the screen is down. A child, never this object: whatever drives the screen sits here too, and a component on a disabled GameObject stops listening.")]
		[SerializeField] private GameObject _content;

		[Header("Fade")]
		[MinValue(0f)]
		[SerializeField] private float _fadeInDuration = 0.15f;

		[MinValue(0f)]
		[SerializeField] private float _fadeOutDuration = 0.35f;

		[SerializeField] private Ease _ease = Ease.OutQuad;

		[Header("Minimum Time")]
		[Tooltip("The shortest the screen stays up, however quickly the wait it covers turns out to end. A screen that flashes past reads as a glitch rather than as loading.")]
		[MinValue(0f)]
		[SerializeField] private float _minimumShownDuration = 1f;

		[Header("Input")]
		[Tooltip("Off, input keeps running behind the screen — the player walks around a table they cannot see.")]
		[SerializeField] private bool _blockInputWhileShown = true;

		[Tooltip("Maps switched off for as long as the screen is up, taken from the project-wide actions. Menu maps stay out of this list, or the screen would block itself.")]
		[ValueDropdown(nameof(GetActionMapNames), AppendNextDrawer = true)]
		[SerializeField] private string[] _blockedActionMaps = { "Player" };

		private readonly List<InputActionMap> _suppressedMaps = new();

		private Tween _fade;
		private CancellationTokenSource _hold;
		private float _shownAt;

		public bool IsShown { get; private set; }

		private void Reset()
		{
			_group = GetComponent<CanvasGroup>();
			_bar = GetComponentInChildren<UIProgressBar>(true);
			_content = transform.childCount > 0 ? transform.GetChild(0).gameObject : null;
		}

		private void Awake()
		{
			ApplyShown(false, true);
		}

		private void OnDestroy()
		{
			KillFade();
			CancelHold();

			// Torn down mid-wait, the devices it switched off would stay off with nothing left to switch
			// them back on — an unplayable game and no clue why.
			RestoreInput();
		}

		public void Show(string title = null, bool instant = false)
		{
			if (_titleLabel && !string.IsNullOrEmpty(title)) _titleLabel.text = title;

			CancelHold();

			// The clock starts on the first Show and keeps running across the ones after it: the minimum
			// covers the whole time the player is looking at the screen, not each phase that renames it.
			if (!IsShown)
			{
				_shownAt = Time.unscaledTime;

				// Reset before it is seen: a bar left at the end of the last wait reads as a load that
				// finished the moment it started.
				if (_bar) _bar.SetProgress(0f, true);
			}

			IsShown = true;
			ApplyShown(true, instant);

			SuppressInput();
		}

		public void SetProgress(float value, bool instant = false)
		{
			if (_bar) _bar.SetProgress(value, instant);
		}

		public void Hide(bool instant = false)
		{
			CancelHold();

			var remaining = instant ? 0f : Mathf.Max(0f, _shownAt + _minimumShownDuration - Time.unscaledTime);
			if (remaining <= 0f)
			{
				ApplyHidden(instant);
				return;
			}

			// The bar spends the hold arriving rather than sitting full — a finished bar waiting to
			// disappear reads as a stall, which is the opposite of what the hold is there for.
			if (_bar) _bar.SetProgress(1f, remaining);

			HoldThenHide(remaining).LogExceptionsAndForget();
		}

		// Counted in unscaled time down to the deadline rather than awaited as a span: a loading screen is
		// usually up while the game is stopped, and a scaled wait would never end there.
		private async Awaitable HoldThenHide(float seconds)
		{
			_hold = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

			var token = _hold.Token;
			var until = Time.unscaledTime + seconds;

			try
			{
				while (Time.unscaledTime < until) await Awaitable.NextFrameAsync(token);

				ApplyHidden(false);
			}
			catch (OperationCanceledException)
			{
			}
		}

		private void ApplyHidden(bool instant)
		{
			IsShown = false;
			ApplyShown(false, instant);

			RestoreInput();
		}

		// Named maps off the project-wide actions rather than the devices: the devices belong to the whole
		// application, and switching them off would take the menus and the cursor with them.
		private void SuppressInput()
		{
			if (!_blockInputWhileShown || _suppressedMaps.Count > 0) return;

			var actions = InputSystem.actions;
			if (!actions) return;

			foreach (var name in _blockedActionMaps)
			{
				var map = actions.FindActionMap(name);
				if (map == null || !map.enabled) continue;

				_suppressedMaps.Add(map);
				map.Disable();
			}
		}

		// Only what this screen switched off is switched back on, so a map something else disabled on
		// purpose does not come back to life because a loading screen happened to close over it.
		private void RestoreInput()
		{
			foreach (var map in _suppressedMaps) map?.Enable();

			_suppressedMaps.Clear();
		}

		private static IEnumerable<string> GetActionMapNames()
		{
			var actions = InputSystem.actions;
			if (!actions) yield break;

			foreach (var map in actions.actionMaps) yield return map.name;
		}

		private void CancelHold()
		{
			if (_hold == null) return;

			_hold.Cancel();
			_hold.Dispose();
			_hold = null;
		}

		private void ApplyShown(bool shown, bool instant)
		{
			KillFade();

			var duration = shown ? _fadeInDuration : _fadeOutDuration;

			_group.blocksRaycasts = shown;
			_group.interactable = shown;

			// Shown first so the fade has something to reveal; hidden only once it has finished.
			if (shown) SetContentActive(true);

			if (instant || duration <= 0f)
			{
				_group.alpha = shown ? 1f : 0f;
				SetContentActive(shown);
				return;
			}

			// DOTween.To rather than CanvasGroup.DOFade: that shortcut lives in DOTween's UI module,
			// which this project does not carry, and the core tween does the same job.
			_fade = DOTween.To(() => _group.alpha, alpha => _group.alpha = alpha, shown ? 1f : 0f, duration)
				.SetEase(_ease)
				.SetUpdate(true)
				.SetLink(gameObject)
				.OnComplete(() =>
				{
					// Left active at zero alpha it would keep eating the frame it no longer draws in.
					if (!shown) SetContentActive(false);
				});
		}

		// The content goes down, this object never does. Switching off the root would take the binder
		// down with it, and a binder that stopped listening is a loading screen that never comes back.
		private void SetContentActive(bool active)
		{
			if (_content && _content.activeSelf != active) _content.SetActive(active);
		}

		private void KillFade()
		{
			_fade?.Kill();
			_fade = null;
		}
	}
}
