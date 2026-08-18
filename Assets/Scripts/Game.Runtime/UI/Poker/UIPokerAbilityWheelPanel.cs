using System;
using System.Collections.Generic;
using System.Threading;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Runtime.UI.Poker
{
	// The corner where the local player's own tricks sit. It feeds the wheel and plays what the wheel has
	// turned up; the wheel itself knows nothing about poker. Up only while this client is holding something
	// and the table is accepting abilities — a wheel offering a move the server would refuse is worse than
	// no wheel at all.
	public class UIPokerAbilityWheelPanel : UIPokerView
	{
		[Header("Panel")]
		[SerializeField] private GameObject _panel;

		[Header("References")]
		[Required]
		[SerializeField] private UIPokerAbilityWheel _wheel;

		[Tooltip("Optional. Dimmed rather than hidden while the window is shut, so the player can still read what they hold.")]
		[SerializeField] private CanvasGroup _canvasGroup;

		[Tooltip("Optional. Says why the wheel cannot be played right now.")]
		[SerializeField] private TextMeshProUGUI _hintLabel;

		[Header("Input")]
		[Tooltip("Plays whichever ability the wheel has in the middle.")]
		[SerializeField] private InputActionReference _useAction;

		[Header("Look")]
		[PropertyRange(0f, 1f)]
		[SerializeField] private float _closedAlpha = 0.45f;

		[Tooltip("Held between the row being punched and the ability actually going off, so the press is seen landing rather than the effect arriving out of nowhere.")]
		[MinValue(0f)]
		[SerializeField] private float _useDelay = 0.12f;

		private PokerAbilityModule _module;
		private readonly List<PokerAbility> _abilities = new();

		// Released in finally, never on the success path alone: one throw between the punch and the send and
		// the wheel would be dead for the rest of the session.
		private bool _using;

		private void Awake()
		{
			if (_panel) _panel.SetActive(false);
		}

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerAbilityModule>();

			if (_useAction)
			{
				_useAction.action.performed += HandleUsePressed;
				_useAction.action.Enable();
			}

			LocalData.OnAbilitiesChanged += HandleAbilitiesChanged;

			if (_module)
			{
				_module.Enabled.OnValueChanged += HandleBoolChanged;
				_module.AbilityWindowOpen.OnValueChanged += HandleBoolChanged;
			}

			RebuildWheel();
			RefreshWindow();
		}

		protected override void OnUnbind()
		{
			if (_module)
			{
				_module.AbilityWindowOpen.OnValueChanged -= HandleBoolChanged;
				_module.Enabled.OnValueChanged -= HandleBoolChanged;
			}

			LocalData.OnAbilitiesChanged -= HandleAbilitiesChanged;

			if (_useAction) _useAction.action.performed -= HandleUsePressed;

			_module = null;

			if (_wheel) _wheel.Clear();
			if (_panel) _panel.SetActive(false);
		}

		private void HandleBoolChanged(bool previous, bool current) => RefreshWindow();
		private void HandleAbilitiesChanged() => RebuildWheel();

		// The ids replicate; the assets they name are looked up against the same pool the server dealt from,
		// so a client resolves its own hand without the server ever sending the abilities themselves.
		private void RebuildWheel()
		{
			_abilities.Clear();

			var pool = _module ? _module.Pool : null;

			if (pool)
			{
				foreach (var id in LocalData.AbilityIds)
				{
					var ability = pool.FindById(id.ToString());
					if (ability) _abilities.Add(ability);
				}
			}

			if (_wheel) _wheel.SetItems(_abilities);

			RefreshWindow();
		}

		private void RefreshWindow()
		{
			var enabled = _module && _module.Enabled.Value;
			var visible = enabled && _abilities.Count > 0;

			if (_panel && _panel.activeSelf != visible) _panel.SetActive(visible);
			if (!visible) return;

			var open = _module.AbilityWindowOpen.Value && LocalData.IsInHand;

			if (_canvasGroup) _canvasGroup.alpha = open ? 1f : _closedAlpha;
			if (_hintLabel) _hintLabel.text = open ? string.Empty : "Not now";
		}

		// async void is allowed here because this is the input handler itself, and it catches.
		private async void HandleUsePressed(InputAction.CallbackContext context)
		{
			try
			{
				await UseSelectedAsync(destroyCancellationToken);
			}
			catch (OperationCanceledException)
			{
				// The panel went away mid-press; nothing to undo, and nothing worth logging.
			}
		}

		private async Awaitable UseSelectedAsync(CancellationToken token)
		{
			if (_using || !IsBound || !_module || !_wheel) return;
			if (!_module.AbilityWindowOpen.Value || !LocalData.IsInHand) return;

			var ability = _wheel.Selected;
			if (!ability) return;

			_using = true;

			try
			{
				if (_wheel.SelectedView is UIPokerAbilityWheelItem item) item.PlayPress();

				// Unscaled, because the wait wants to be the length the press looks, and the press is a tween
				// that keeps running while the table is stopped.
				var until = Time.unscaledTime + _useDelay;
				while (Time.unscaledTime < until) await Awaitable.NextFrameAsync(token);

				// Re-checked after the wait: a street can close, or the hand can end, in the time the press
				// takes to land, and the ability must not go off into a window that has since shut.
				if (!IsBound || !_module || !_module.AbilityWindowOpen.Value || !LocalData.IsInHand) return;

				// The server is told which one by name. It holds the same hand and will refuse anything this
				// client does not actually have, so the wheel offering it is never the thing that decides.
				_module.UseAbilityRPC(ability.AbilityId);
			}
			finally
			{
				_using = false;
			}
		}
	}
}
