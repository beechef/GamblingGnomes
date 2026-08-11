using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.UI.Button
{
	[RequireComponent(typeof(UnityEngine.UI.Button))]
	public class UIButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		public event Action OnClick;
		public event Action OnHover;
		public event Action OnUnHover;

		private UnityEngine.UI.Button _button;
		private bool _initialized;

		public bool IsInteractable
		{
			get
			{
				if (!_initialized) Initialize();
				return _button.interactable;
			}
			set
			{
				if (!_initialized) Initialize();
				_button.interactable = value;
			}
		}

		private void Awake()
		{
			Initialize();
		}

		private void OnDestroy()
		{
			DeInitialize();
		}

		private void Initialize()
		{
			if (_initialized) return;

			_button = GetComponent<UnityEngine.UI.Button>();
			_button.onClick.AddListener(Click);
			_initialized = true;
		}

		private void DeInitialize()
		{
			if (!_initialized) return;

			_initialized = false;
			_button.onClick.RemoveListener(Click);
		}

		private void Click()
		{
			if (!IsInteractable) return;
			OnClick?.Invoke();
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			OnHover?.Invoke();
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			OnUnHover?.Invoke();
		}
	}
}
