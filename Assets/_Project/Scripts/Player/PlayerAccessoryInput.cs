using KINEMATION.TacticalShooterPack.Scripts.Player;
using KillOrDead.Attachments;
using KillOrDead.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KillOrDead.Player
{
    /// <summary>
    /// 들고 있는 총에 달린 장치(레이저 / 전술 후레쉬)를 켜고 끈다.
    ///
    /// T키는 작업대 상호작용과 같이 쓴다. 조준선 안에 상호작용할 물체가 있으면
    /// <see cref="PlayerInteractor"/>가 가져가고, 없을 때만 레이저가 토글된다.
    /// (T를 쓰는 데가 작업대뿐이라 이렇게 나눠도 겹칠 일이 없다.)
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Player/Player Accessory Input")]
    public class PlayerAccessoryInput : MonoBehaviour
    {
        [SerializeField] private Key laserKey = Key.T;
        [SerializeField] private Key flashlightKey = Key.Y;

        private TacticalShooterPlayer _player;
        private PlayerInteractor _interactor;

        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            _player = GetComponent<TacticalShooterPlayer>();
            if (_player == null) _player = GetComponentInParent<TacticalShooterPlayer>();

            _interactor = GetComponent<PlayerInteractor>();
            if (_interactor == null) _interactor = GetComponentInParent<PlayerInteractor>();
        }

        private void Update()
        {
            if (!InputEnabled || Keyboard.current == null) return;

            // 작업대 UI가 열려 있으면(상호작용 입력이 꺼진 상태) 장치 토글도 막는다.
            if (_interactor != null && !_interactor.InputEnabled) return;

            var attachments = GetActiveAttachments();
            if (attachments == null) return;

            // 상호작용 대상이 있으면 T는 상호작용 쪽이 가져간다.
            bool interactorWantsKey = _interactor != null && _interactor.HasTarget;

            if (!interactorWantsKey && Keyboard.current[laserKey].wasPressedThisFrame)
            {
                attachments.ToggleLaser();
            }

            if (Keyboard.current[flashlightKey].wasPressedThisFrame)
            {
                attachments.ToggleFlashlight();
            }
        }

        private WeaponAttachmentController GetActiveAttachments()
        {
            if (_player == null) return null;

            var weapon = _player.GetActiveWeapon();
            return weapon != null ? weapon.GetComponent<WeaponAttachmentController>() : null;
        }
    }
}
