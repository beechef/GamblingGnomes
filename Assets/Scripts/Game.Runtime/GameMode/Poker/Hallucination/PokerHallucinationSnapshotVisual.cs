using Game.Runtime.Audio;
using Game.Runtime.GameMode.Poker.Player;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// How high this machine's own player is, heard as mix snapshots over everything: the mild one past the
	// first threshold, the strong one stacked on top of it past the second, each let go as the rate falls
	// back under its own. Owner only — the mix is about the player listening, not whoever else is high.
	public class PokerHallucinationSnapshotVisual : NetworkBehaviour
	{
		[SerializeField] private AudioEvent _mildSnapshot;

		[MinValue(1)]
		[SerializeField] private int _mildThreshold = 20;

		[SerializeField] private AudioEvent _strongSnapshot;

		[MinValue(1)]
		[SerializeField] private int _strongThreshold = 41;

		private PokerPlayerData _data;
		private AudioHandle _mild;
		private AudioHandle _strong;

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

			Hold(ref _mild, _mildSnapshot, false);
			Hold(ref _strong, _strongSnapshot, false);
		}

		private void HandleRateChanged(int previous, int current) => Apply(current);

		private void Apply(int rate)
		{
			Hold(ref _mild, _mildSnapshot, rate >= _mildThreshold);
			Hold(ref _strong, _strongSnapshot, rate >= _strongThreshold);
		}

		// Started once when wanted and stopped once when not, so a rate moving within a band never restarts it.
		private static void Hold(ref AudioHandle handle, AudioEvent snapshot, bool wanted)
		{
			if (wanted == handle.IsValid || !AudioManager.Instance) return;

			if (wanted)
			{
				if (snapshot) handle = AudioManager.Instance.Play(snapshot);
				return;
			}

			AudioManager.Instance.Stop(handle);
			handle = default;
		}
	}
}
