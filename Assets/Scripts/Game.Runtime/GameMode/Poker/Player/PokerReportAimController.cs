using System.Text;
using Game.Runtime.GameMode.Poker.Abilities;
using Game.Runtime.GameMode.Poker.Modules;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// Turns the accuser looking round the table into a name. Only their own client can see through their
	// eyes, so the aim is read here and sent up as it changes — never per frame, and never at all outside
	// the few seconds the accusation is open. The server has the last word on whether whoever they landed
	// on can be accused, and is the one that lights them up for everybody else.
	[DefaultExecutionOrder(10)]
	public class PokerReportAimController : NetworkBehaviour
	{
		[Header("Detection")]
		[Tooltip("How far across the table the finger reaches. Everyone at it should be inside this — the aim is a social act, not a shot.")]
		[SerializeField] private float _aimRange = 12f;

		[Tooltip("Radius of the sphere cast. Faces are small at the far end of a table, so the aim gets some tolerance rather than needing a pixel.")]
		[SerializeField] private float _aimRadius = 0.35f;

		[SerializeField] private LayerMask _playerLayers = ~0;

		[Header("Debug")]
		[Tooltip("Draws the cast and every collider it turned up — green for the player being aimed at, yellow for a player that lost, magenta for our own body, red for scenery. Scene view, play mode.")]
		[SerializeField] private bool _drawDebugRays;

		[Tooltip("Writes what the cast turned up to the console, once per change rather than once per frame. On for a session that cannot find a target, off otherwise.")]
		[SerializeField] private bool _logAimChanges;

		[Header("References")]
		[Tooltip("The eye the accuser is looking through. Same bone the camera hangs off, so the finger goes where the view does.")]
		[SerializeField] private Transform _lookOrigin;

		private const ulong NoAim = ulong.MaxValue;

		private readonly RaycastHit[] _hits = new RaycastHit[16];

		private PokerGameMode _gameMode;
		private PokerAbilityModule _module;
		private ulong _sentAim = NoAim;
		private bool _wasAiming;

		// Tracked apart from the aim itself so the hit list still gets written the first time a scan finds
		// nobody. Gating it on the aim changing meant the one case worth reading — no hits at all, from the
		// first frame to the last — was the one case that printed nothing.
		private ulong _loggedAim = NoAim;
		private bool _hasLogged;

		public override void OnNetworkSpawn()
		{
			if (!IsOwner) return;

			PokerGameMode.OnInstanceChanged += HandleGameModeChanged;
			HandleGameModeChanged(PokerGameMode.Instance);
		}

		public override void OnNetworkDespawn()
		{
			PokerGameMode.OnInstanceChanged -= HandleGameModeChanged;

			_gameMode = null;
			_module = null;
		}

		private void HandleGameModeChanged(PokerGameMode gameMode)
		{
			_gameMode = gameMode;
			_module = gameMode ? gameMode.FindModule<PokerAbilityModule>() : null;
			_sentAim = NoAim;
		}

		// Casting in LateUpdate, after PlayerController has written the look onto the bone the camera hangs
		// off. In Update that bone still holds whatever pose the Animator gave it, so the ray would leave
		// the eye pointing somewhere the player isn't looking.
		private void LateUpdate()
		{
			if (!IsOwner || !IsSpawned) return;

			var aiming = IsAiming();

			// The edge, not the state: "we are now being asked to aim" is the one thing whose absence
			// explains every other silence downstream, and it is worth exactly one line.
			if (aiming != _wasAiming)
			{
				_wasAiming = aiming;
				_hasLogged = false;

				if (_logAimChanges) Debug.Log($"[PokerReportAimController] aiming={aiming} phase={(_module == null ? "no module" : _module.ReportPhase.Value.ToString())} origin={(_lookOrigin ? _lookOrigin.name : "NULL")}");
			}

			if (!aiming) return;

			var aim = Scan();
			if (aim == _sentAim) return;

			_sentAim = aim;
			_module.AimReportRPC(aim);
		}

		// The turn is what says whose move it is, here as everywhere else at this table.
		private bool IsAiming()
		{
			if (_module == null || !_gameMode || !_gameMode.Data) return false;
			if (_module.ReportPhase.Value != PokerReportPhase.Aiming) return false;

			return _gameMode.Data.CurrentTurnClientId.Value == OwnerClientId;
		}

		private ulong Scan()
		{
			if (!_lookOrigin) return NoAim;

			var origin = _lookOrigin.position;
			var ray = new Ray(origin, _lookOrigin.forward);

			var count = Physics.SphereCastNonAlloc(ray, _aimRadius, _hits, _aimRange, _playerLayers,
				QueryTriggerInteraction.Collide);

			var bestDistance = float.MaxValue;
			var best = NoAim;
			var log = _logAimChanges ? new StringBuilder() : null;

			for (var i = 0; i < count; i++)
			{
				var hit = _hits[i];

				// A cast that starts already overlapping a collider reports distance 0 and no usable hit
				// point, so the collider's own centre stands in for drawing and for the near-far ordering.
				var point = hit.distance > 0f ? hit.point : hit.collider.bounds.center;
				var player = hit.collider.GetComponentInParent<PokerPlayer>();

				// Our own body is the collider the eye is sitting inside, so it is always in the list and
				// never the answer.
				var isSelf = player && player.ClientId == OwnerClientId;
				var eligible = player && !isSelf && hit.distance < bestDistance;

				log?.Append("\n  ").Append(hit.collider.name)
					.Append(" d=").Append(hit.distance.ToString("F2"))
					.Append(" player=").Append(player ? player.DisplayName : "none")
					.Append(isSelf ? " (self)" : eligible ? " (taken)" : " (skipped)");

				if (_drawDebugRays)
				{
					var color = !player ? Color.red : isSelf ? Color.magenta : Color.yellow;
					Debug.DrawLine(origin, point, color);
				}

				if (!eligible) continue;

				bestDistance = hit.distance;
				best = player.ClientId;
			}

			if (_drawDebugRays)
			{
				Debug.DrawRay(origin, ray.direction * _aimRange, best == NoAim ? Color.red : Color.green);

				// Where the sphere ends up, so a radius too small to reach anybody is visible rather than
				// something to be inferred from nothing being hit.
				DrawSphere(origin + ray.direction * Mathf.Min(bestDistance, _aimRange), _aimRadius,
					best == NoAim ? Color.red : Color.green);
			}

			if (log != null && (!_hasLogged || best != _loggedAim))
			{
				_hasLogged = true;
				_loggedAim = best;

				Debug.Log($"[PokerReportAimController] {count} hit(s) over {_aimRange}m r={_aimRadius} mask={_playerLayers.value}, from {origin} facing {ray.direction}, aim={(best == NoAim ? "none" : best.ToString())}{(count == 0 ? "\n  (the cast reached nothing at all)" : log.ToString())}");
			}

			return best;
		}

		private static void DrawSphere(Vector3 centre, float radius, Color color)
		{
			Debug.DrawLine(centre - Vector3.right * radius, centre + Vector3.right * radius, color);
			Debug.DrawLine(centre - Vector3.up * radius, centre + Vector3.up * radius, color);
			Debug.DrawLine(centre - Vector3.forward * radius, centre + Vector3.forward * radius, color);
		}
	}
}
