using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Runtime.Player.Camera
{
	// The one writer of the first-person lens's field of view. Anything that wants the view wider or narrower
	// adds a modifier and eases its weight; the lens is the authored angle plus every modifier's weighted pull
	// toward its own angle, so two running at once add up rather than the last one erasing the other.
	public class PlayerCameraFovController : MonoBehaviour
	{
		[Required]
		[SerializeField] private CinemachineCamera _camera;

		private readonly List<PlayerCameraFovModifier> _modifiers = new();

		private float _authoredFieldOfView;

		public float AuthoredFieldOfView => _authoredFieldOfView;

		private void Awake()
		{
			if (_camera) _authoredFieldOfView = _camera.Lens.FieldOfView;
		}

		public PlayerCameraFovModifier Add(float fieldOfView)
		{
			var modifier = new PlayerCameraFovModifier { FieldOfView = fieldOfView };
			_modifiers.Add(modifier);

			return modifier;
		}

		public void Remove(PlayerCameraFovModifier modifier)
		{
			if (!_modifiers.Remove(modifier) || _modifiers.Count > 0) return;

			Write(_authoredFieldOfView);
		}

		// Only while something is pulling: an untouched lens is left as authored.
		private void LateUpdate()
		{
			if (_modifiers.Count == 0) return;

			var fieldOfView = _authoredFieldOfView;
			foreach (var modifier in _modifiers)
			{
				fieldOfView += (modifier.FieldOfView - _authoredFieldOfView) * modifier.Weight;
			}

			Write(fieldOfView);
		}

		private void Write(float fieldOfView)
		{
			if (!_camera) return;

			var lens = _camera.Lens;
			lens.FieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
			_camera.Lens = lens;
		}
	}

	// A live request the caller writes into; Weight 0 is no pull at all, 1 is the whole angle.
	public class PlayerCameraFovModifier
	{
		public float FieldOfView;
		public float Weight;
	}
}
