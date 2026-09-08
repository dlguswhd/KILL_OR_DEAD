using UnityEngine;
using UnityEngine.InputSystem;

namespace KillOrDead.Interaction
{
    public interface IInteractable
    {
        string GetPrompt();
        void Interact(GameObject interactor);

        /// <summary>
        /// 이 대상을 쓸 때 누르는 키. 작업대는 T, 문은 F처럼 물체마다 다르게 줄 수 있다.
        /// 아래 <c>=> Key.T</c>는 "기본값"이라서, 따로 적지 않은 물체는 전부 T키가 된다
        /// (기존 작업대 코드를 고치지 않아도 되게 하려는 것).
        /// </summary>
        Key InteractKey => Key.T;
    }
}
