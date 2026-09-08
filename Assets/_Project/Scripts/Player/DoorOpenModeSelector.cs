using KillOrDead.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KillOrDead.Player
{
    /// <summary>
    /// 문을 <b>손으로 열지 / 발로 찰지</b>를 마우스 휠로 고르는 컴포넌트. 플레이어 루트에 붙인다.
    ///
    /// <para><b>휠은 이미 수류탄 거리 조절에 쓰이고 있다.</b> 그래서 서로 안 겹치게
    /// <b>문을 바라보고 있을 때만</b> 휠을 문 방식 전환으로 가져온다.
    /// 수류탄을 들고 조준하는 중이면(<see cref="GrenadeThrower.IsAiming"/>) 수류탄 쪽에 양보한다.</para>
    ///
    /// 고른 방식은 <see cref="Door"/>가 F키를 받을 때 읽어 간다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Player/Door Open Mode Selector")]
    public class DoorOpenModeSelector : MonoBehaviour
    {
        [Tooltip("게임을 시작할 때의 기본 방식.")]
        [SerializeField] private DoorOpenMode defaultMode = DoorOpenMode.Normal;

        [Tooltip("휠을 굴렸다고 인정하는 최소 크기. 너무 작으면 살짝만 건드려도 방식이 바뀐다.")]
        [SerializeField, Min(0.01f)] private float wheelThreshold = 0.1f;

        private PlayerInteractor _interactor;
        private GrenadeThrower _grenadeThrower;

        /// <summary>지금 고른 방식.</summary>
        public DoorOpenMode Mode { get; private set; }

        private void Awake()
        {
            Mode = defaultMode;
            _interactor = GetComponent<PlayerInteractor>();
            _grenadeThrower = GetComponent<GrenadeThrower>();
        }

        private void Update()
        {
            if (!IsLookingAtDoor()) return;
            if (_grenadeThrower != null && _grenadeThrower.IsAiming) return;
            if (Mouse.current == null) return;

            float wheel = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < wheelThreshold) return;

            // 방식이 두 개뿐이라 방향과 상관없이 그냥 번갈아 바꾼다.
            Mode = Mode == DoorOpenMode.Normal ? DoorOpenMode.Kick : DoorOpenMode.Normal;
        }

        private bool IsLookingAtDoor()
        {
            return _interactor != null && _interactor.Current is Door;
        }
    }
}
