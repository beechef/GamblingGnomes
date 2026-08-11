using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Interaction
{
	public interface IInteractable
	{
		string ActionName { get; }
		Transform Transform { get; }
		bool CanInteract(NetworkBehaviourReference interactor);
		void Interact(NetworkBehaviourReference interactor);
		void SetFocused(bool focused);
	}
}
