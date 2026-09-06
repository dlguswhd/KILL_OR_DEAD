using System.Reflection;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using KillOrDead.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KillOrDead.Player
{
    /// <summary>
    /// 우리가 정한 키 배치를 벤더 위에 덮어쓴다.
    ///
    /// 벤더 입력 애셋(`Inputs_TacticalShooterPack.inputactions`)은 구매한 파일이라 고치지 않는다.
    /// 대신 <b>해당 액션만 런타임에 꺼두고</b> 키를 직접 읽어서 처리한다.
    ///
    /// <list type="bullet">
    /// <item><b>무기 교체: F → C</b> — F는 문 여는 상호작용에 쓸 예정이라 비워둔다.</item>
    /// <item><b>기울이기(Q/E): 누르고 있기 → 한 번 누르면 고정</b> — 누른 채로 움직이기 불편했다.</item>
    /// <item><b>마우스 휠</b> — 무기 교체에서 떼어내 수류탄 던지는 거리 조절 전용으로 넘긴다.</item>
    /// </list>
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Player/Player Key Overrides")]
    public class PlayerKeyOverrides : MonoBehaviour
    {
        [Header("Weapon Change")]
        [Tooltip("무기 교체 키. 벤더 기본값 F는 문 상호작용용으로 비워둔다.")]
        [SerializeField] private Key weaponChangeKey = Key.C;

        [Tooltip("벤더의 F키 무기 교체 액션을 끈다.")]
        [SerializeField] private bool disableVendorWeaponChange = true;

        [Header("Lean (기울이기)")]
        [Tooltip("왼쪽으로 기울이기. 한 번 누르면 고정되고, 다시 누르면 원위치.")]
        [SerializeField] private Key leanLeftKey = Key.Q;

        [Tooltip("오른쪽으로 기울이기. 한 번 누르면 고정되고, 다시 누르면 원위치.")]
        [SerializeField] private Key leanRightKey = Key.E;

        [Tooltip("기울이는 각도(도). 벤더 기본값도 30이다.")]
        [SerializeField, Min(0f)] private float leanAngle = 30f;

        [Tooltip("벤더의 '누르고 있는 동안만 기울이기' 액션을 끈다.")]
        [SerializeField] private bool disableVendorLean = true;

        [Header("Mouse Wheel")]
        [Tooltip("마우스 휠에서 무기 교체를 떼어낸다. 휠은 수류탄 던지는 거리 조절에 쓴다.")]
        [SerializeField] private bool disableVendorMouseWheel = true;

        private TacticalShooterPlayer _player;
        private PlayerInput _playerInput;
        private PlayerInteractor _interactor;

        private InputAction _weaponChangeAction;
        private InputAction _leanAction;
        private InputAction _scrollAction;

        // 벤더의 기울이기 값은 protected 필드라 리플렉션으로 쓴다.
        // 액션을 꺼두었기 때문에 벤더가 이 값을 덮어쓰지 않아, 바뀔 때만 한 번 써주면 된다.
        private FieldInfo _leanField;

        /// <summary>-1 = 왼쪽, 0 = 가운데, 1 = 오른쪽.</summary>
        private int _leanDirection;

        public bool InputEnabled { get; set; } = true;

        /// <summary>지금 어느 쪽으로 기울여 있는지. -1 왼쪽 / 0 없음 / 1 오른쪽.</summary>
        public int LeanDirection => _leanDirection;

        private void Awake()
        {
            _player = GetComponent<TacticalShooterPlayer>();
            _playerInput = GetComponent<PlayerInput>();
            _interactor = GetComponent<PlayerInteractor>();

            _leanField = typeof(TacticalShooterPlayer)
                .GetField("_leanInput", BindingFlags.NonPublic | BindingFlags.Instance);

            if (_playerInput != null && _playerInput.actions != null)
            {
                _weaponChangeAction = _playerInput.actions.FindAction("ChangeWeapon");
                _leanAction = _playerInput.actions.FindAction("Lean");
                _scrollAction = _playerInput.actions.FindAction("MouseScroll");
            }
        }

        private void OnEnable()
        {
            if (disableVendorWeaponChange) _weaponChangeAction?.Disable();
            if (disableVendorLean) _leanAction?.Disable();
            if (disableVendorMouseWheel) _scrollAction?.Disable();
        }

        private void OnDisable()
        {
            // 이 컴포넌트가 빠지면 벤더 기본 조작으로 되돌려 놓는다.
            SetLean(0);

            _weaponChangeAction?.Enable();
            _leanAction?.Enable();
            _scrollAction?.Enable();
        }

        private void Update()
        {
            EnforceDisabledActions();

            if (Keyboard.current == null) return;

            if (!CanTakeInput())
            {
                // 작업대 UI 등으로 조작이 잠기면 기울인 자세를 풀어둔다.
                if (_leanDirection != 0) SetLean(0);
                return;
            }

            if (Keyboard.current[weaponChangeKey].wasPressedThisFrame && _player != null)
            {
                _player.OnChangeWeapon();
            }

            // 같은 키를 다시 누르면 원위치, 반대쪽 키를 누르면 그쪽으로 넘어간다.
            if (Keyboard.current[leanLeftKey].wasPressedThisFrame) SetLean(_leanDirection == -1 ? 0 : -1);
            else if (Keyboard.current[leanRightKey].wasPressedThisFrame) SetLean(_leanDirection == 1 ? 0 : 1);
        }

        /// <summary>
        /// 꺼둔 벤더 액션이 다시 켜졌으면 도로 끈다.
        ///
        /// 작업대에 들어갔다 나오면 <see cref="PlayerInput"/> 컴포넌트가 꺼졌다 켜지는데,
        /// 그때 <b>액션이 전부 다시 활성화된다.</b> 그러면 벤더 기울이기가 살아나서
        /// 키를 뗄 때 값을 0으로 덮어써 버려 "고정"이 풀린다.
        /// </summary>
        private void EnforceDisabledActions()
        {
            if (disableVendorWeaponChange && _weaponChangeAction is { enabled: true })
            {
                _weaponChangeAction.Disable();
            }

            if (disableVendorMouseWheel && _scrollAction is { enabled: true })
            {
                _scrollAction.Disable();
            }

            if (!disableVendorLean || _leanAction is not { enabled: true }) return;

            _leanAction.Disable();

            // 벤더가 이미 값을 건드렸을 수 있으니 지금 기울인 방향을 다시 써준다.
            if (_leanDirection != 0) SetLean(_leanDirection);
        }

        private bool CanTakeInput()
        {
            if (!InputEnabled) return false;
            if (_playerInput != null && !_playerInput.enabled) return false;
            if (_interactor != null && !_interactor.InputEnabled) return false;

            return true;
        }

        private void SetLean(int direction)
        {
            _leanDirection = direction;

            if (_player == null || _leanField == null) return;

            // 벤더는 Q일 때 +, E일 때 - 값을 쓴다(`OnLean`에서 부호를 뒤집는다).
            // 실제로 몸이 기울어지는 건 플레이어 Update가 이 값으로 부드럽게 보간해 준다.
            _leanField.SetValue(_player, -direction * leanAngle);
        }
    }
}
