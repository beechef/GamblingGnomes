using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.UI.Poker;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// How far gone somebody is, over their head where the table can read it: the same meter the owner has
	// on their HUD, handed the body it hangs on. Rides the name tag's own canvas rather than bringing a
	// second one — that canvas already turns to face the viewer, already hides at range and already hides
	// for the owner, and none of that is worth writing twice.
	public class PokerHallucinationTagVisual : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private PokerPlayer _player;
		[SerializeField] private UIPokerHallucinationMeter _meter;

		private bool _started;

		private void Awake()
		{
			if (!_player) _player = GetComponentInParent<PokerPlayer>();
		}

		// From Start, not Awake or the first OnEnable: a spawned player is enabled before the values it
		// arrives with are applied, and the meter draws what it reads when it binds.
		private void Start()
		{
			_started = true;
			Bind();
		}

		private void OnEnable()
		{
			if (_started) Bind();
		}

		private void OnDisable()
		{
			if (_meter) _meter.Unbind();
		}

		private void Bind()
		{
			if (_meter && _player) _meter.Bind(_player);
		}
	}
}
