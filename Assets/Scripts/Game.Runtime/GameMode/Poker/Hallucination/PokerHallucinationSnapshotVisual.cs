using Game.Runtime.Audio;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// How high this machine's own player is, heard as a mix snapshot over everything: a milder one past the
	// first threshold, the stronger one in its place past the second, none below. Owner only — the mix is
	// about the player listening, not about whoever else is high.
	public class PokerHallucinationSnapshotVisual : NetworkBehaviour
	{
		[SerializeField] private AudioEvent _mildSnapshot;

		[MinValue(1)]
		[SerializeField] private int _mildThreshold = 20;

		[SerializeField] private AudioEvent _strongSnapshot;

		[MinValue(1)]
		[SerializeField] private int _strongThreshold = 41;

		private PokerPlayerData _data;
		private AudioHandle _playing;
		private int _level;

		private void Awake() => _data = GetComponentInParent<PokerPlayerData>();

		public override void OnNetworkSpawn()
		{
			if (!IsOwner || !_data) return;

			_data.HallucinationRate.OnValueChanged += HandleRateChanged;
			Apply(_data.HallucinationRate.Value);
		}

		public override void OnNetworkDespawn()
		{
			if (_data) _data.HallucinationRate.OnValueChanged -= HandleRateChanged;

			StopSnapshot();
			_level = 0;
		}

		private void HandleRateChanged(int previous, int current) => Apply(current);

		private void Apply(int rate)
		{
			var level = rate >= _strongThreshold ? 2 : rate >= _mildThreshold ? 1 : 0;
			if (level == _level) return;

			_level = level;
			StopSnapshot();

			var snapshot = level == 2 ? _strongSnapshot : level == 1 ? _mildSnapshot : null;
			if (snapshot && AudioManager.Instance) _playing = AudioManager.Instance.Play(snapshot);
		}

		private void StopSnapshot()
		{
			if (!_playing.IsValid) return;

			if (AudioManager.Instance) AudioManager.Instance.Stop(_playing);
			_playing = default;
		}
	}
}
