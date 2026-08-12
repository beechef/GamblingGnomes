using Game.Runtime.Controller;
using TMPro;
using UnityEngine;

namespace Game.Runtime.Player
{
	// A name that reads the same from anywhere: it turns to face the camera but never tips with it, so
	// the text stays upright however the viewer is looking up or down.
	public class PlayerNameTagVisual : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private PlayerData _data;
		[SerializeField] private Canvas _canvas;
		[SerializeField] private TextMeshProUGUI _label;

		[Header("Visibility")]
		[Tooltip("Hidden past this distance. Zero or less keeps it readable at any range.")]
		[SerializeField] private float _maxDistance = 30f;

		[Tooltip("On, the owner never sees their own tag — it would hang in the middle of a first person view.")]
		[SerializeField] private bool _hideForOwner = true;

		private void Awake()
		{
			if (!_data) _data = GetComponentInParent<PlayerData>();
			if (_canvas) _canvas.enabled = false;
		}

		private void OnEnable()
		{
			if (_data) _data.OnIdentityChanged += Refresh;

			Refresh();
		}

		private void OnDisable()
		{
			if (_data) _data.OnIdentityChanged -= Refresh;
		}

		private void Refresh()
		{
			if (!_data || !_label) return;

			_label.text = _data.DisplayName.Value.ToString();
		}

		private void LateUpdate()
		{
			if (!_canvas || !_data) return;

			if (_hideForOwner && _data.IsSpawned && _data.IsOwner)
			{
				if (_canvas.enabled) _canvas.enabled = false;
				return;
			}

			// Read fresh rather than cached: leaving a table and joining another swaps the camera out
			// underneath a tag that outlives neither.
			var view = GameCamera.View;
			if (!view) return;

			var toCamera = transform.position - view.position;
			var visible = _maxDistance <= 0f || toCamera.sqrMagnitude <= _maxDistance * _maxDistance;

			if (_canvas.enabled != visible) _canvas.enabled = visible;
			if (!visible) return;

			// Flattened before it becomes a rotation: following the camera's pitch would tilt the text
			// away from the horizon every time somebody looked down at it.
			var flat = new Vector3(toCamera.x, 0f, toCamera.z);
			if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
		}
	}
}
