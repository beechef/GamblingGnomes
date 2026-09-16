using Unity.Cinemachine;
using UnityEngine;

namespace Game.Runtime.Player
{
	// What one shake feels like: a 6D noise signal (position and rotation together) played for a set time
	// through an attack, a held stretch and a decay. Continuous rather than a single kick — the noise keeps
	// moving the view for as long as the envelope is open, which is what a hit that rattles you reads as.
	//
	// Config only. The impulse definition it fills in lives on whoever plays it, because a running impulse
	// reads its definition live every frame and an asset is not a thing that runs.
	[CreateAssetMenu(fileName = "CameraShake_", menuName = "Game/Player/Camera Shake Preset")]
	public class PlayerCameraShakePreset : ScriptableObject
	{
		[Header("Signal")]
		[Tooltip("6D noise the shake is made of. Its own position and rotation amplitudes set the balance between the two; strength scales both.")]
		[SerializeField] private SignalSourceAsset _signal;

		[Tooltip("Multiplier on the signal's amplitudes.")]
		[Min(0f)]
		[SerializeField] private float _strength = 1f;

		[Tooltip("Multiplier on the signal's frequencies. Higher rattles, lower sways.")]
		[Min(0f)]
		[SerializeField] private float _frequency = 1f;

		[Header("Envelope")]
		[Tooltip("Seconds to ramp up to full strength.")]
		[Min(0f)]
		[SerializeField] private float _attack = 0.05f;

		[Tooltip("Seconds held at full strength.")]
		[Min(0f)]
		[SerializeField] private float _duration = 0.5f;

		[Tooltip("Seconds to die away.")]
		[Min(0f)]
		[SerializeField] private float _decay = 0.3f;

		public float TotalDuration => _attack + _duration + _decay;

		public bool IsPlayable => _signal && _strength > 0f && TotalDuration > 0f;

		public float Strength => _strength;

		public void ApplyTo(CinemachineImpulseDefinition definition)
		{
			definition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Legacy;
			definition.RawSignal = _signal;
			definition.AmplitudeGain = 1f;
			definition.FrequencyGain = _frequency;
			definition.RepeatMode = CinemachineImpulseDefinition.RepeatModes.Loop;
			definition.Randomize = true;
			definition.TimeEnvelope = new CinemachineImpulseManager.EnvelopeDefinition
			{
				AttackTime = _attack,
				SustainTime = _duration,
				DecayTime = _decay
			};

			// Felt in full wherever the listener is: who feels it is decided by who plays it, not by distance.
			definition.ImpactRadius = 1000f;
			definition.DissipationDistance = 1000f;
			definition.DirectionMode = CinemachineImpulseManager.ImpulseEvent.DirectionModes.Fixed;
		}
	}
}
