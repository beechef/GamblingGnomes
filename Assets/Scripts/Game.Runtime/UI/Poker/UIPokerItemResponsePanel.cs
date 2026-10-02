using Game.Runtime.GameMode.Poker.Items;
using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Progress;
using TMPro;
using UnityEngine;

namespace Game.Runtime.UI.Poker
{
	// An item asking this player to answer: they read who asks and what to point at, over the time left,
	// on the same plank as every notice. Everyone else already read the item's own notice. Read straight off
	// the module's replicated PendingResponse.
	public class UIPokerItemResponsePanel : UIPokerView
	{
		[Tooltip("Shown while an answer is pending. A child, so this view keeps listening while it is hidden.")]
		[SerializeField] private GameObject _content;

		[SerializeField] private TMP_Text _label;
		[SerializeField] private UITimerBar _timerBar;

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
			PokerItem item = null;
			var asked = pending.IsPending && pending.ResponderClientId == LocalClientId && _module.TryGetItem(pending.Item, out item);

			if (_content) _content.SetActive(asked);
			if (!asked || !_label) return;

			// An answer with no clock shows no bar: a bar that never moves reads as a hung timer.
			if (_timerBar) _timerBar.gameObject.SetActive(pending.IsTimed);

			_label.text = UIPokerActionNotice.Capitalize($"{PokerPlayer.ColoredNameOf(pending.RequesterClientId)} {item.GetResponsePrompt()}");

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
