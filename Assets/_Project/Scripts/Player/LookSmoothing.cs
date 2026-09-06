using KINEMATION.KShooterCore.Runtime.Camera;
using KINEMATION.TacticalShooterPack.Scripts.Animation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KillOrDead.Player
{
    /// <summary>
    /// 마우스 시점을 부드럽게 만든다.
    ///
    /// 핵심은 "각도가 아니라 입력을 부드럽게 한다"는 것이다.
    /// 각도(pitchInput)를 목표값으로 보간하면 반동이 밀어올린 각도까지 목표로 끌려가서
    /// 자동 복귀가 생겨버린다(배틀그라운드식 반동 기획이 깨진다).
    /// 그래서 여기서는 마우스 델타를 잠시 쌓아뒀다가 매 프레임 일부만 흘려보내고,
    /// 각도에는 항상 더하기(+=)만 한다. 반동이 더해놓은 값은 절대 건드리지 않는다.
    ///
    /// 벤더(<see cref="KINEMATION.TacticalShooterPack.Scripts.Player.TacticalShooterPlayer"/>)의
    /// lookSensitivity는 0으로 꺼두고, 시점 입력은 전부 이 컴포넌트가 처리한다.
    /// PlayerInput의 Send Messages는 같은 오브젝트의 모든 컴포넌트에 OnLook을 보내므로
    /// 별도 배선 없이 이 컴포넌트도 같은 입력을 받는다.
    /// </summary>
    // 플레이어의 Update가 pitchInput을 카메라로 복사하기 전에 값을 갱신해야
    // 시점이 한 프레임 밀리지 않는다.
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("KILL OR DEAD/Player/Look Smoothing")]
    public class LookSmoothing : MonoBehaviour
    {
        [Header("Sensitivity")]
        [Tooltip("마우스 1픽셀당 회전 각도(도). 기존 벤더 설정값은 0.2였고, 너무 빠르다는 피드백을 받아 낮춰 잡았다.")]
        [SerializeField, Min(0f)] private float sensitivity = 0.15f;

        [Header("Smoothing")]
        [Tooltip("클수록 빠릿하고 작을수록 부드럽다. 대략 3/smoothing 초에 걸쳐 입력이 소진된다.")]
        [SerializeField, Min(0.01f)] private float smoothing = 16f;

        [Tooltip("남은 입력이 이 각도보다 작아지면 한 번에 흘려보낸다(잔여값이 질질 끌리는 것 방지).")]
        [SerializeField, Min(0f)] private float snapThreshold = 0.002f;

        private TacticalProceduralAnimation _proceduralAnimation;
        private FPSCameraAnimator _fpsCamera;
        private PlayerInput _playerInput;

        /// <summary>아직 시점에 반영되지 않고 쌓여 있는 마우스 입력(도 단위).</summary>
        private Vector2 _pending;

        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            _proceduralAnimation = GetComponent<TacticalProceduralAnimation>();
            if (_proceduralAnimation == null)
            {
                _proceduralAnimation = GetComponentInChildren<TacticalProceduralAnimation>();
            }

            _fpsCamera = GetComponentInChildren<FPSCameraAnimator>(true);
            _playerInput = GetComponent<PlayerInput>();
        }

        // PlayerInput(Send Messages)이 호출한다.
        public void OnLook(InputValue value)
        {
            if (!InputEnabled) return;

            // 마우스 델타는 이미 "이번 프레임에 움직인 양"이라 Time.deltaTime을 곱하면 안 된다.
            _pending += value.Get<Vector2>() * sensitivity;
        }

        private void Update()
        {
            // 작업대 UI 등으로 조작이 잠기면(PlayerInput 자체가 꺼진다) 쌓인 입력을 버린다.
            // 안 버리면 다시 켰을 때 그동안의 입력이 한꺼번에 밀려들어온다.
            if (!InputEnabled || (_playerInput != null && !_playerInput.enabled))
            {
                _pending = Vector2.zero;
                return;
            }

            if (_pending.sqrMagnitude <= 0f)
            {
                if (_proceduralAnimation != null) _proceduralAnimation.deltaLookInput = Vector2.zero;
                return;
            }

            // 프레임률이 달라져도 같은 속도로 소진되는 지수 감쇠.
            float alpha = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            Vector2 step = _pending * alpha;

            if (_pending.magnitude <= snapThreshold)
            {
                step = _pending;
            }

            _pending -= step;

            ApplyLook(step);
        }

        private void ApplyLook(Vector2 delta)
        {
            if (_fpsCamera != null && _fpsCamera.UseFreeLook)
            {
                // 프리룩(LCtrl) 중에는 몸/카메라 각도가 아니라 곁눈질 오프셋에 넣는다.
                _fpsCamera.AddFreeLookInput(delta);
                return;
            }

            if (_proceduralAnimation == null) return;

            // 총 흔들림(sway) 연출용. 벤더가 채우던 값을 대신 채워준다.
            _proceduralAnimation.deltaLookInput = delta;

            // 반동과 공존하려면 반드시 "현재값에 더하기"만 해야 한다.
            // FPSCameraAnimator가 Quaternion.Euler(lookInput.y, ...)로 쓰기 때문에
            // pitchInput은 음수일 때 위쪽이다(그래서 y를 뺀다).
            _proceduralAnimation.pitchInput =
                Mathf.Clamp(_proceduralAnimation.pitchInput - delta.y, -90f, 90f);
            _proceduralAnimation.yawInput =
                Mathf.Clamp(_proceduralAnimation.yawInput + delta.x, -90f, 90f);
        }
    }
}
