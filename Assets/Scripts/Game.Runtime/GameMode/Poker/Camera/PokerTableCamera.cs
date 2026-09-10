using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Camera
{
	// The one shot in this round that is not somebody's own eyes: the whole table at once, so everybody is
	// looking at the same picture while a victim is being chosen and while the hands are read. It lives in
	// the scene beside the table it frames — a camera in the player prefab would be one copy per player,
	// each starting from a different chair, and nothing to lay it out against.
	//
	// A prefab cannot serialize a reference into the scene it lands in, so this registers itself the way
	// PokerSeat, PokerScenery and PokerRoomController already do.
	//
	// Requests are counted rather than a bool: every client at the table asks for the same camera, and one
	// player standing up must not take the shot away from the room.
	public class PokerTableCamera : MonoBehaviour
	{
		public static PokerTableCamera Instance { get; private set; }

		[Header("Shot")]
		[Required]
		[SerializeField] private CinemachineCamera _camera;

		[Tooltip("What it is raised to while live. It only has to beat the rig's own camera, which sits at the default.")]
		[SerializeField] private int _priority = 30;

		private readonly List<object> _holders = new();

		private void Awake()
		{
			if (Instance && Instance != this)
			{
				Debug.LogWarning($"[{name}] A second {nameof(PokerTableCamera)} is in this scene; the table has one wide shot.", this);
				enabled = false;
				return;
			}

			Instance = this;

			// Off until it is asked for. Every enabled virtual camera is a candidate and the rig's own sits
			// at the default priority, so a shot left live at zero is a tie the brain may break either way.
			Apply();
		}

		private void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		public void Request(object holder)
		{
			if (holder == null || _holders.Contains(holder)) return;

			_holders.Add(holder);
			Apply();
		}

		public void Release(object holder)
		{
			if (holder == null || !_holders.Remove(holder)) return;

			Apply();
		}

		private void Apply()
		{
			if (!_camera) return;

			var live = _holders.Count > 0;

			_camera.Priority.Enabled = live;
			_camera.Priority.Value = live ? _priority : 0;
			_camera.enabled = live;
		}
	}
}
