using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Interaction
{
	public abstract class InteractableBase : NetworkBehaviour, IInteractable
	{
		[Header("Interaction")]
		[SerializeField] private string _actionName = "Use";
		[SerializeField] private bool _interactableOnSpawn = true;

		[Header("References")]
		[SerializeField] private InteractableVisual _visual;

		[HideInInspector] public NetworkVariable<bool> Interactable = new(true,
			readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

		public virtual string ActionName => _actionName;
		public Transform Transform => transform;

		public override void OnNetworkSpawn()
		{
			if (IsServer) Interactable.Value = _interactableOnSpawn;
		}

		public override void OnNetworkDespawn()
		{
			SetFocused(false);
		}

		public virtual bool CanInteract(NetworkBehaviourReference interactor) => Interactable.Value;

		public void Interact(NetworkBehaviourReference interactor)
		{
			if (!IsServer) return;
			if (!CanInteract(interactor)) return;

			OnInteractServer(interactor);
		}

		public void SetFocused(bool focused)
		{
			if (_visual) _visual.SetOutline(focused);
			OnFocusChanged(focused);
		}

		protected abstract void OnInteractServer(NetworkBehaviourReference interactor);

		protected virtual void OnFocusChanged(bool focused) { }
	}
}
