using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.UI.Wheel
{
	// A strip of items that scrolls past a fixed middle. As many views exist as there are anchors and no
	// more — stepping recycles the one leaving off the far end and re-binds it at the other, so a list of
	// any length costs the same handful of objects. The two anchors at the ends are meant to sit off
	// screen: that is where a slot appears from and disappears to, which is what makes the movement read
	// as a wheel turning rather than items popping in place.
	public class UIWheelController<T> : MonoBehaviour
	{
		// Previous and current, so a listener can animate the change rather than only the destination.
		public event Action<T, T> OnSelectionChanged;

		[Header("Anchors")]
		[InfoBox("List order is the wheel's order. The first and last are the off-screen slots a recycled item is snapped to; everything between them is what the player sees.")]
		[SerializeField] private List<RectTransform> _anchors = new();

		[Header("Items")]
		[Required]
		[SerializeField] private UIWheelItemView<T> _itemPrefab;

		[Tooltip("Where the views are parented. Empty uses this object, which is normally what you want — the anchors are only positions.")]
		[SerializeField] private RectTransform _itemParent;

		[Header("Motion")]
		[MinValue(0f)]
		[SerializeField] private float _stepDuration = 0.25f;

		[SerializeField] private Ease _stepEase = Ease.OutCubic;

		[Header("Input")]
		[Tooltip("Optional. Anything implementing IUIWheelInput; leave empty to drive the wheel from code.")]
		[SerializeField] private MonoBehaviour _inputBehaviour;

		private readonly List<T> _items = new();
		private readonly List<UIWheelItemView<T>> _views = new();

		private int _index;
		private bool _stepping;
		private UIWheelDirection? _queued;

		private IUIWheelInput Input => _inputBehaviour as IUIWheelInput;

		public int Count => _items.Count;
		public bool HasItems => _items.Count > 0;
		public T Selected => HasItems ? _items[_index] : default;

		private int MiddleSlot => _anchors.Count / 2;

		private void OnEnable()
		{
			if (Input != null) Input.OnStepped += HandleStepped;
		}

		private void OnDisable()
		{
			if (Input != null) Input.OnStepped -= HandleStepped;

			KillTweens();
		}

		private void OnDestroy()
		{
			KillTweens();
		}

		// Rebuilt rather than diffed: the wheel is a handful of slots over a list that changes as a whole
		// when a hand is dealt or an ability is spent, and a diff would have to answer where the selection
		// went when the item under it disappeared.
		public void SetItems(IReadOnlyList<T> items, int startIndex = 0)
		{
			_items.Clear();
			if (items != null) _items.AddRange(items);

			if (_items.Count == 0 || _anchors.Count == 0)
			{
				Clear();
				return;
			}

			// Rebound rather than rebuilt: there is exactly one view per anchor whatever the list holds, and
			// destroying them first would leave the old set alive for a frame — Destroy runs at the end of it
			// — with a fresh set already drawn on top.
			EnsureViews();

			KillTweens();
			_stepping = false;
			_queued = null;

			_index = Wrap(startIndex);

			for (var slot = 0; slot < _views.Count; slot++)
			{
				var view = _views[slot];

				view.Bind(_items[ItemIndexForSlot(slot)]);
				view.SnapTo(_anchors[slot]);
				view.SetSelected(false, true);
			}

			_views[MiddleSlot].SetSelected(true, true);

			OnSelectionChanged?.Invoke(default, Selected);
		}

		private void EnsureViews()
		{
			if (_views.Count == _anchors.Count) return;

			Clear();

			var parent = _itemParent ? _itemParent : (RectTransform)transform;

			for (var slot = 0; slot < _anchors.Count; slot++)
				_views.Add(Instantiate(_itemPrefab, parent));
		}

		public void Clear()
		{
			KillTweens();

			foreach (var view in _views)
			{
				if (view) Destroy(view.gameObject);
			}

			_views.Clear();
			_stepping = false;
			_queued = null;
		}

		public void Step(UIWheelDirection direction)
		{
			if (_items.Count == 0 || _views.Count < 2) return;

			// Only the latest queued turn survives: spinning the wheel faster than it animates should skip
			// ahead, not build a backlog that keeps moving after the player stops.
			if (_stepping)
			{
				_queued = direction;
				return;
			}

			BeginStep(direction);
		}

		private void HandleStepped(UIWheelDirection direction) => Step(direction);

		private void BeginStep(UIWheelDirection direction)
		{
			_stepping = true;

			var previous = Selected;
			var forward = direction == UIWheelDirection.Forward;

			_index = Wrap(_index + (forward ? 1 : -1));

			_views[MiddleSlot].SetSelected(false);
			_views[MiddleSlot + (forward ? 1 : -1)].SetSelected(true);

			var recycled = forward ? RecycleToEnd() : RecycleToStart();
			var travelling = 0;

			for (var slot = 0; slot < _views.Count; slot++)
			{
				var view = _views[slot];
				if (view == recycled) continue;

				travelling++;
				view.TweenTo(_anchors[slot], _stepDuration, _stepEase).OnComplete(() =>
				{
					travelling--;
					if (travelling <= 0) EndStep(previous);
				});
			}

			// Nothing to wait for when every slot was the recycled one, which only happens on a wheel too
			// small to turn — the selection still changed, so it is still announced.
			if (travelling == 0) EndStep(previous);
		}

		private UIWheelItemView<T> RecycleToEnd()
		{
			var recycled = _views[0];
			_views.RemoveAt(0);
			_views.Add(recycled);

			var last = _anchors.Count - 1;
			recycled.Bind(_items[ItemIndexForSlot(last)]);
			recycled.SnapTo(_anchors[last]);

			return recycled;
		}

		private UIWheelItemView<T> RecycleToStart()
		{
			var recycled = _views[^1];
			_views.RemoveAt(_views.Count - 1);
			_views.Insert(0, recycled);

			recycled.Bind(_items[ItemIndexForSlot(0)]);
			recycled.SnapTo(_anchors[0]);

			return recycled;
		}

		private void EndStep(T previous)
		{
			_stepping = false;

			OnSelectionChanged?.Invoke(previous, Selected);

			if (!_queued.HasValue) return;

			var next = _queued.Value;
			_queued = null;
			BeginStep(next);
		}

		private int Wrap(int index)
		{
			var count = _items.Count;
			if (count == 0) return 0;

			return ((index % count) + count) % count;
		}

		private int ItemIndexForSlot(int slot) => Wrap(_index + (slot - MiddleSlot));

		private void KillTweens()
		{
			foreach (var view in _views)
			{
				if (view) view.KillTween();
			}
		}
	}
}
