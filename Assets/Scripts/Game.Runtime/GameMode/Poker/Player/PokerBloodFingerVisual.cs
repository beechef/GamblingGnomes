using Game.Runtime.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Player
{
	// Blood drawn as a body: one point of it is one finger, so the table reads how badly somebody has been
	// caught off their hands rather than off a number. Nothing new is replicated — health already is, and a
	// second value saying the same thing is a second value to fall out of step with it.
	public class PokerBloodFingerVisual : NetworkBehaviour
	{
		[Header("References")]
		[Required]
		[SerializeField] private PokerPlayerData _data;

		[Required]
		[SerializeField] private PlayerFingerVisual _fingers;

		public override void OnNetworkSpawn()
		{
			if (!_data || !_fingers)
			{
				Debug.LogWarning($"{nameof(PokerBloodFingerVisual)} on {name} has nothing to draw with: blood will cost no fingers.", this);
				return;
			}

			_data.OnHealthChanged += HandleHealthChanged;

			// Late join: whatever they have already lost is already lost, and no change is coming to say so.
			Redraw(_data.Health.Value);
		}

		public override void OnNetworkDespawn()
		{
			if (_data) _data.OnHealthChanged -= HandleHealthChanged;
		}

		private void HandleHealthChanged(int previous, int current) => Redraw(current);

		// Counted off the damage rather than off the blood left, so a player at full health is whole whatever
		// the two numbers are authored to. Author MaxHealth to the finger count — as Player_Poker does, eight
		// each — and the last finger and the last point of blood go together.
		private void Redraw(int health) => _fingers.SetLostFingerCount(_data.MaxHealth - health);
	}
}
