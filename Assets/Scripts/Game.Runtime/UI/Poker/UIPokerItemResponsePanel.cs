using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Progress;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// What the table is waiting on when an item asks somebody to answer: the one asked reads what to point
	// at, everyone else who they are waiting on, both over the time left. Read straight off the module's
	// replicated PendingResponse, so it is up on every screen at the same moment.
	public class UIPokerItemResponsePanel : UIPokerView
	{
		[Tooltip("Shown while an answer is pending. A child, so this view keeps listening while it is hidden.")]
		[SerializeField] private GameObject _content;

		[SerializeField] private TMP_Text _label;
		[SerializeField] private UITimerBar _timerBar;

		[Tooltip("Read by everyone but the player being asked. {0} is their name.")]
		[SerializeField] private string _waitingText = "{0} IS CHOOSING A CARD";

		private PokerItemModule _module;

		protected override bool WantsTick => _module && _module.PendingResponse.Value.IsPending && _module.PendingResponse.Value.IsTimed;

		private void Awake()
		{
			if (_content) _content.SetActive(false);
		}

		protected override void OnBind()
		{
			_module = GameMode.FindModule<PokerItemModule>();
			if (_module) _module.OnPendingResponseChanged += Refresh;

			Refresh();
		}

		protected override void OnUnbind()
		{
			if (_module) _module.OnPendingResponseChanged -= Refresh;
			_module = null;

			if (_content) _content.SetActive(false);
		}

		private void Refresh()
		{
			var pending = _module ? _module.PendingResponse.Value : PokerItemResponse.None;

			if (_content) _content.SetActive(pending.IsPending);
			if (!pending.IsPending || !_label) return;

			// An answer with no clock shows no bar: a bar that never moves reads as a hung timer.
			if (_timerBar) _timerBar.gameObject.SetActive(pending.IsTimed);

			if (pending.ResponderClientId == LocalClientId && _module.TryGetItem(pending.Item, out var item))
				_label.text = $"{PokerPlayer.NameOf(pending.RequesterClientId)} {item.GetResponsePrompt()}";
			else
				_label.text = string.Format(_waitingText, PokerPlayer.NameOf(pending.ResponderClientId));

			OnTick();
		}

		protected override void OnTick()
		{
			if (!_timerBar || !_module) return;

			var pending = _module.PendingResponse.Value;
			var now = Unity.Netcode.NetworkManager.Singleton ? Unity.Netcode.NetworkManager.Singleton.ServerTime.Time : 0d;
			var remaining = Mathf.Max(0f, (float)(pending.EndTime - now));

			_timerBar.SetTime(remaining, pending.Duration > 0f ? remaining / pending.Duration : 0f);
		}
	}
}
