using System;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Interaction
{
	public class InteractableToggle : InteractableBase
	{
		[Header("Toggle")]
		[SerializeField] private string _actionNameWhenOn = "Turn Off";
		[SerializeField] private string _actionNameWhenOff = "Turn On";

		[HideInInspector] public NetworkVariable<bool> IsOn = new(false,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public event Action<bool> OnToggled;

		public override string ActionName => IsOn.Value ? _actionNameWhenOn : _actionNameWhenOff;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			IsOn.OnValueChanged += HandleIsOnChanged;
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
			IsOn.OnValueChanged -= HandleIsOnChanged;
		}

		protected override void OnInteractServer(NetworkBehaviourReference interactor)
		{
			IsOn.Value = !IsOn.Value;
		}

		private void HandleIsOnChanged(bool previous, bool current) => OnToggled?.Invoke(current);
	}
}
