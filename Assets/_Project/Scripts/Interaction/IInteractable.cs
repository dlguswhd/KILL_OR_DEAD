using UnityEngine;

namespace KillOrDead.Interaction
{
    public interface IInteractable
    {
        string GetPrompt();
        void Interact(GameObject interactor);
    }
}
