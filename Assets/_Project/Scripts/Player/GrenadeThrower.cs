using KINEMATION.TacticalShooterPack.Scripts.Player;
using KillOrDead.Combat;
using KillOrDead.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KillOrDead.Player
{
    /// <summary>
    /// 수류탄 투척 (배틀그라운드 방식 — 두 번 누른다).
    ///
    /// <list type="number">
    /// <item>G 1번째 — 안전핀을 뽑고 <b>조준 상태</b>로 들어간다. 빨간 포물선이 뜬다.</item>
    /// <item>마우스 휠 — 던지는 거리를 조절한다. 위로 올리면 멀리, 내리면 짧게.</item>
    /// <item>G 2번째 — 던진다.</item>
    /// </list>
    ///
    /// <b>권총(X키)을 꺼내 든 동안은 던질 수 없다.</b> 오른손에 권총이 들려 있어 수류탄을 쥘 자리가 없다.
    /// 권총을 넣으면 다시 던질 수 있다.
    ///
    /// 애니메이션은 캐릭터 컨트롤러의 <b>GrenadeThrow 레이어</b>(오른팔 마스크, Override)로 재생한다.
    /// 하체는 마스크에서 빠져 있어서 기존 이동 동작이 그대로 유지된다.
    ///
    /// 레이어 가중치는 이 스크립트가 직접 0에서 1로 올렸다가 다시 내린다.
    /// (컨트롤러의 기본 가중치는 0이라 평소엔 투척 동작이 전혀 섞이지 않는다.)
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Player/Grenade Thrower")]
    public class GrenadeThrower : MonoBehaviour
    {
        private const string LayerName = "GrenadeThrow";
        private const string TriggerName = "ThrowGrenade";

        // 벤더가 권총 퀵드로우 포즈를 켤 때 쓰는 파라미터. 우리는 포즈만 빌려 쓴다.
        private const string QuickDrawBool = "UseQuickDraw";

        // 퀵드로우 포즈가 실제로 얼마나 적용되어 있는지(0~1). 클립의 커브가 굴리는 값이라
        // 불을 꺼도 곧바로 0이 되지 않고 벤더 전환 시간만큼 늦게 내려온다.
        private const string QuickDrawFloat = "PistolQuickDraw";

        [Header("Input")]
        [SerializeField] private Key throwKey = Key.G;

        [Header("Ammo")]
        [Tooltip("들고 다니는 수류탄 개수.")]
        [SerializeField, Min(0)] private int carriedGrenades = 4;

        [Header("Grenade")]
        [Tooltip("던질 수류탄 프리팹(Rigidbody + Grenade 컴포넌트). 비워두면 애니메이션만 재생한다.")]
        [SerializeField] private GameObject grenadePrefab;

        [Tooltip("손에 쥐고 있을 때 붙는 뼈 이름. 이 클립은 오른손으로 던진다.")]
        [SerializeField] private string handBoneName = "hand_r";

        [Tooltip("손 뼈 기준으로 수류탄이 놓이는 위치. hand_r 기준 +X가 손끝, -Y가 손바닥, +Z가 엄지 쪽이다.")]
        [SerializeField] private Vector3 handLocalPosition = new Vector3(0.075f, -0.03f, -0.047f);

        [Tooltip("수류탄 회전. (90,0,0)이면 몸통 긴 축이 엄지 방향으로 눕는다(주먹으로 쥔 모양).")]
        [SerializeField] private Vector3 handLocalEuler = new Vector3(90f, 0f, 0f);

        [Header("Throw")]
        [Tooltip("기본 던지는 속도(m/s). 마우스 휠로 조절하기 전의 시작값.")]
        [SerializeField, Min(0f)] private float throwSpeed = 14f;

        [Tooltip("마우스 휠을 끝까지 내렸을 때의 속도(m/s) — 가장 가깝게 던진다.")]
        [SerializeField, Min(0f)] private float minThrowSpeed = 6f;

        [Tooltip("마우스 휠을 끝까지 올렸을 때의 속도(m/s) — 가장 멀리 던진다.")]
        [SerializeField, Min(0f)] private float maxThrowSpeed = 24f;

        [Tooltip("휠 한 칸에 바뀌는 속도(m/s).")]
        [SerializeField, Min(0.1f)] private float throwSpeedStep = 1.5f;

        [Tooltip("포물선이 되도록 위로 얹어주는 각도(도).")]
        [SerializeField] private float throwUpAngle = 10f;

        [Tooltip("던진 사람 손에서 나가는 회전(스핀).")]
        [SerializeField] private float throwSpin = 6f;

        [Header("Timing")]
        [Tooltip("클립의 어느 지점에서 수류탄이 손을 떠나는지(0~1). 실측상 팔이 최대로 뻗는 지점이 0.72다.")]
        [SerializeField, Range(0f, 1f)] private float releaseNormalizedTime = 0.72f;

        [Tooltip("투척 레이어가 켜지는 데 걸리는 시간(초).")]
        [SerializeField, Min(0f)] private float blendInTime = 0.12f;

        [Tooltip("투척 레이어가 꺼지는 데 걸리는 시간(초).")]
        [SerializeField, Min(0f)] private float blendOutTime = 0.25f;

        [Tooltip("클립의 이 지점을 지나면 레이어를 다시 내리기 시작한다(0~1).")]
        [SerializeField, Range(0f, 1f)] private float blendOutNormalizedTime = 0.85f;

        [Tooltip("클립의 이 지점에서 소총을 다시 두 손으로 드는 자세로 돌아가기 시작한다(0~1).\n" +
                 "던지는 팔이 내려가기 전에 시작해야 두 전환이 겹쳐서 부드럽다. " +
                 "이 값이 blendOutNormalizedTime보다 늦으면 '빈 권총을 쥔 오른손'이 잠깐 보인다.")]
        [SerializeField, Range(0f, 1f)] private float poseExitNormalizedTime = 0.78f;

        [Header("Weapon")]
        [Tooltip("던지는 동안 권총 퀵드로우(X키) 포즈를 켠다. 소총이 왼손으로 넘어가고 오른팔이 자유로워진다. " +
                 "끄면 총이 두 손에 그대로 있어서 던지는 팔을 따라 총이 같이 휘둘러진다.")]
        [SerializeField] private bool useQuickDrawPose = true;

        [Tooltip("권총을 꺼내 든 상태(X키)에서는 수류탄을 못 던지게 막는다.\n" +
                 "오른손에 권총이 들려 있어서 수류탄을 쥘 자리가 없기 때문이다. 권총을 넣으면(X) 다시 던질 수 있다.")]
        [SerializeField] private bool blockWhilePistolDrawn = true;

        [Header("Aiming")]
        [Tooltip("빨간 포물선을 그리는 컴포넌트. 비워두면 같은 오브젝트에서 찾고, 없으면 자동으로 만든다.")]
        [SerializeField] private GrenadeTrajectory trajectory;

        [Tooltip("포물선이 시작되는 지점(카메라 기준). 실제로 손을 떠나는 위치를 실측해서 넣은 값이라\n" +
                 "예측선과 실제 궤적이 거의 일치한다. 던지는 애니메이션을 바꾸면 다시 재야 한다.")]
        [SerializeField] private Vector3 releaseOffsetFromCamera = new Vector3(0.166f, 0.527f, -0.286f);


        private Animator _animator;
        private Camera _camera;
        private PlayerInput _playerInput;
        private PlayerInteractor _interactor;
        private TacticalShooterPlayer _player;
        private Transform _handBone;

        private int _layerIndex = -1;
        private bool _aiming;
        private bool _throwing;
        private bool _released;
        private float _currentThrowSpeed;
        private bool _stateEntered;
        private float _weight;
        private GameObject _heldGrenade;

        // 우리가 퀵드로우 포즈를 켠 경우에만 끝날 때 되돌린다.
        // (플레이어가 직접 X로 켜둔 상태라면 건드리면 안 된다.)
        private bool _restoreQuickDrawPose;

        // 소총 복귀를 이미 시작했는지. 던지기가 끝나기 전에 미리 시작하므로 따로 들고 있어야 한다.
        private bool _poseExited;

        // 이번 던지기에서 퀵드로우 포즈를 썼는지. 복귀 타이밍을 소총에 맞출지 판단하는 데 쓴다.
        // (_restoreQuickDrawPose는 포즈를 끄는 순간 false가 되므로 따로 필요하다.)
        private bool _usedQuickDrawPose;

        public bool InputEnabled { get; set; } = true;

        /// <summary>남은 수류탄 개수.</summary>
        public int CarriedGrenades => carriedGrenades;

        /// <summary>지금 던지는 동작 중인지. 다른 동작이 겹치지 않게 하는 데 쓴다.</summary>
        public bool IsThrowing => _throwing;

        /// <summary>핀을 뽑고 조준(거리 조절) 중인지.</summary>
        public bool IsAiming => _aiming;

        /// <summary>지금 설정된 던지는 속도(m/s). 마우스 휠로 바뀐다.</summary>
        public float CurrentThrowSpeed => _currentThrowSpeed;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>(true);
            _camera = GetComponentInChildren<Camera>(true);
            _playerInput = GetComponent<PlayerInput>();
            _interactor = GetComponent<PlayerInteractor>();
            _player = GetComponent<TacticalShooterPlayer>();

            if (_animator != null)
            {
                foreach (var t in _animator.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != handBoneName) continue;
                    _handBone = t;
                    break;
                }
            }

            if (trajectory == null) trajectory = GetComponent<GrenadeTrajectory>();
            if (trajectory == null) trajectory = gameObject.AddComponent<GrenadeTrajectory>();

            _currentThrowSpeed = Mathf.Clamp(throwSpeed, minThrowSpeed, maxThrowSpeed);
        }

        private void OnEnable()
        {
            // 무기를 바꾸면 컨트롤러가 통째로 교체되므로 레이어 번호를 그때그때 다시 찾는다.
            _layerIndex = -1;
        }

        private void OnDisable()
        {
            // 도중에 꺼지면 자세와 총이 어중간한 상태로 남는다. 반드시 되돌린다.
            if (_aiming) CancelAiming();
            if (_throwing) FinishThrow();
        }

        private void Update()
        {
            if (_animator == null) return;

            ResolveLayerIndex();
            if (_layerIndex < 0) return;

            if (_throwing) UpdateThrow();
            else if (_aiming) UpdateAiming();
            else if (CanStartThrow() && Keyboard.current[throwKey].wasPressedThisFrame) StartAiming();

            _animator.SetLayerWeight(_layerIndex, _weight);
        }

        private void ResolveLayerIndex()
        {
            // 컨트롤러가 바뀌면 인덱스가 달라질 수 있어서 이름으로 다시 찾는다.
            if (_layerIndex >= 0 && _layerIndex < _animator.layerCount
                && _animator.GetLayerName(_layerIndex) == LayerName)
            {
                return;
            }

            _layerIndex = -1;
            for (int i = 0; i < _animator.layerCount; i++)
            {
                if (_animator.GetLayerName(i) != LayerName) continue;
                _layerIndex = i;
                break;
            }
        }

        private bool CanStartThrow()
        {
            if (!InputEnabled || Keyboard.current == null) return false;
            if (carriedGrenades <= 0) return false;

            // 작업대 UI가 열려 있는 등 조작이 잠긴 상태면 던지지 않는다.
            if (_playerInput != null && !_playerInput.enabled) return false;
            if (_interactor != null && !_interactor.InputEnabled) return false;

            // 권총(X키)을 들고 있으면 오른손이 차 있어서 수류탄을 쥘 수 없다.
            if (blockWhilePistolDrawn && IsPistolDrawn()) return false;

            return true;
        }

        /// <summary>
        /// G 첫 번째 — 핀을 뽑고 조준 상태로 들어간다.
        ///
        /// 퀵드로우 자세가 켜지면서 소총이 왼손으로 넘어가고, 자유로워진 오른손에 수류탄을 든다.
        /// (권총을 들고 있는 동안은 <see cref="CanStartThrow"/>에서 막히므로 여기까지 오지 않는다.)
        /// </summary>
        private void StartAiming()
        {
            _aiming = true;
            _poseExited = false;
            _usedQuickDrawPose = false;
            _restoreQuickDrawPose = false;

            if (useQuickDrawPose && _animator != null)
            {
                _restoreQuickDrawPose = true;
                _usedQuickDrawPose = true;
                _animator.SetBool(QuickDrawBool, true);
            }

            TakeOutGrenade();

            if (trajectory != null) trajectory.Show();
        }

        /// <summary>수류탄을 손에 쥐고 핀을 뽑는다.</summary>
        private void TakeOutGrenade()
        {
            SpawnHeldGrenade();
            if (_heldGrenade == null) return;

            // 핀을 뽑는다. 신관은 손을 떠날 때부터 탄다(들고 있다가 자폭하지 않는다).
            var grenade = _heldGrenade.GetComponent<Grenade>();
            if (grenade != null) grenade.PullPin();
        }

        /// <summary>조준 중 — 마우스 휠로 거리를 조절하고 포물선을 갱신한다.</summary>
        private void UpdateAiming()
        {
            // 작업대에 들어가는 등 조작이 잠기면 조준을 취소한다.
            if (!CanStartThrow())
            {
                CancelAiming();
                return;
            }

            // 조준 중에 총을 쏘면(소총이 왼손에 있는 상태라) 그림이 깨진다. 격발만 막는다.
            var weapon = _player != null ? _player.GetPrimaryWeapon() : null;
            if (weapon != null && weapon.IsFiring) weapon.StopFiring();

            UpdateThrowSpeedFromWheel();
            UpdateTrajectory();

            if (Keyboard.current[throwKey].wasPressedThisFrame) StartThrow();
        }

        /// <summary>마우스 휠: 위로 올리면 멀리, 내리면 짧게.</summary>
        private void UpdateThrowSpeedFromWheel()
        {
            if (Mouse.current == null) return;

            float wheel = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f) return;

            // 휠 값은 플랫폼마다 크기가 다르다(보통 한 칸에 ±120). 방향만 쓴다.
            _currentThrowSpeed = Mathf.Clamp(
                _currentThrowSpeed + Mathf.Sign(wheel) * throwSpeedStep,
                minThrowSpeed, maxThrowSpeed);
        }

        private void UpdateTrajectory()
        {
            if (trajectory == null) return;

            GetThrowOriginAndVelocity(out Vector3 origin, out Vector3 velocity);
            trajectory.UpdateArc(origin, velocity);
        }

        /// <summary>
        /// 던지는 시작점과 속도. <b>예측선과 실제 던지기가 반드시 같은 값을 써야</b>
        /// 포물선이 실제 궤적과 일치한다.
        /// </summary>
        private void GetThrowOriginAndVelocity(out Vector3 origin, out Vector3 velocity)
        {
            Transform aim = _camera != null ? _camera.transform : transform;

            origin = aim.TransformPoint(releaseOffsetFromCamera);

            // 조준한 방향으로 던지되, 포물선이 되게 위로 살짝 얹는다.
            Vector3 direction = Quaternion.AngleAxis(-throwUpAngle, aim.right) * aim.forward;
            velocity = direction * _currentThrowSpeed;
        }

        /// <summary>조준을 그만둔다. 핀은 뽑았지만 아직 손에 있으므로 그냥 치운다.</summary>
        private void CancelAiming()
        {
            _aiming = false;

            if (trajectory != null) trajectory.Hide();
            ExitThrowPose();

            if (_heldGrenade != null)
            {
                Destroy(_heldGrenade);
                _heldGrenade = null;
            }
        }

        /// <summary>G 두 번째 — 실제로 던진다. 수류탄은 이미 손에 쥐고 있다.</summary>
        private void StartThrow()
        {
            _aiming = false;
            _throwing = true;
            _released = false;
            _stateEntered = false;
            _weight = 0f;

            if (trajectory != null) trajectory.Hide();

            _animator.SetTrigger(TriggerName);
        }

        /// <summary>
        /// 소총을 다시 두 손으로 드는 자세로 되돌리기 시작한다.
        ///
        /// <b>던지는 팔 레이어가 내려가기 전에</b> 불러야 한다. 팔이 먼저 내려가고 나서 이걸 부르면,
        /// 그 사이에 "빈 권총을 쥔 오른손" 자세가 그대로 노출된다.
        /// 두 번 불려도 안전하다(처음 한 번만 동작).
        /// </summary>
        private void ExitThrowPose()
        {
            if (_poseExited) return;
            _poseExited = true;

            if (_restoreQuickDrawPose && _animator != null)
            {
                // 다시 두 손으로 소총을 드는 기본 자세로 돌아간다.
                // 실제 복귀는 벤더의 퀵드로우 블렌드가 0.3초쯤에 걸쳐 부드럽게 처리한다.
                _animator.SetBool(QuickDrawBool, false);
            }

            _restoreQuickDrawPose = false;
        }

        /// <summary>플레이어가 X키로 권총을 뽑아 든 상태인지. 그때는 손에 든 무기와 활성 무기가 다르다.</summary>
        private bool IsPistolDrawn()
        {
            if (_player == null) return false;
            return _player.GetPrimaryWeapon() != _player.GetActiveWeapon();
        }

        private void UpdateThrow()
        {
            var state = _animator.GetCurrentAnimatorStateInfo(_layerIndex);
            bool inThrowState = state.IsName("Throw");

            // 트리거를 넣은 직후 몇 프레임은 아직 Empty 상태다. 그동안은 가중치만 올려둔다.
            if (!inThrowState && !_stateEntered)
            {
                _weight = Approach(_weight, 1f, blendInTime);
                return;
            }

            _stateEntered = true;

            if (!inThrowState)
            {
                // 클립이 끝나 Empty로 돌아왔다. 남은 가중치를 내리고 마무리한다.
                ExitThrowPose();

                // 여기서도 소총이 돌아오기 전에 팔을 내리면 빈손이 노출된다.
                if (_usedQuickDrawPose && _animator.GetFloat(QuickDrawFloat) > 0.99f) return;

                _weight = Approach(_weight, 0f, blendOutTime);
                if (_weight <= 0.001f) FinishThrow();
                return;
            }

            float t = Mathf.Clamp01(state.normalizedTime);

            if (!_released && t >= releaseNormalizedTime) ReleaseGrenade();

            // 소총 복귀를 팔이 내려가기 전에 미리 시작해서 두 전환이 겹치게 한다.
            if (t >= poseExitNormalizedTime) ExitThrowPose();

            // 불을 꺼도 벤더 전환 때문에 소총이 곧바로 돌아오지는 않는다.
            // 그동안 팔 레이어를 먼저 내려버리면 "빈 권총을 쥔 오른손"이 그대로 노출되므로,
            // 소총이 실제로 돌아오기 시작할 때까지 팔 동작을 붙잡고 있는다.
            bool waitingForRifle = _usedQuickDrawPose && _animator.GetFloat(QuickDrawFloat) > 0.99f;

            _weight = (t < blendOutNormalizedTime || waitingForRifle)
                ? Approach(_weight, 1f, blendInTime)
                : Approach(_weight, 0f, blendOutTime);
        }

        private void FinishThrow()
        {
            _throwing = false;
            _stateEntered = false;
            _weight = 0f;

            ExitThrowPose();

            // 어떤 이유로든 손에 남아 있으면 정리한다(중간에 끊긴 경우 대비).
            if (_heldGrenade != null)
            {
                Destroy(_heldGrenade);
                _heldGrenade = null;
            }
        }

        /// <summary>던지기 전, 수류탄을 손에 쥐여준다.</summary>
        private void SpawnHeldGrenade()
        {
            if (grenadePrefab == null || _handBone == null) return;

            _heldGrenade = Instantiate(grenadePrefab, _handBone);
            _heldGrenade.transform.localPosition = handLocalPosition;
            _heldGrenade.transform.localRotation = Quaternion.Euler(handLocalEuler);

            // 손에 붙어 있는 동안은 물리와 충돌을 꺼둔다.
            SetPhysicsActive(_heldGrenade, false);

            var grenade = _heldGrenade.GetComponent<Grenade>();
            if (grenade != null) grenade.HoldInHand();
        }

        /// <summary>손에서 놓고 물리 투사체로 전환한다.</summary>
        private void ReleaseGrenade()
        {
            _released = true;
            carriedGrenades = Mathf.Max(0, carriedGrenades - 1);

            if (_heldGrenade == null) return;

            var thrown = _heldGrenade;
            _heldGrenade = null;

            thrown.transform.SetParent(null, true);
            SetPhysicsActive(thrown, true);

            // 예측선과 완전히 같은 시작점·속도를 쓴다. 여기가 어긋나면 빨간 선이 거짓말을 한다.
            GetThrowOriginAndVelocity(out Vector3 origin, out Vector3 velocity);
            thrown.transform.position = origin;

            Transform aim = _camera != null ? _camera.transform : transform;
            var body = thrown.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = velocity;
                body.angularVelocity = aim.right * throwSpin;
            }

            var grenade = thrown.GetComponent<Grenade>();
            if (grenade != null) grenade.Release(gameObject);
        }

        private static void SetPhysicsActive(GameObject go, bool active)
        {
            var body = go.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = !active;
                body.detectCollisions = active;

                // 손에 붙어 있는 동안 보간을 켜두면 빠르게 움직이는 손을 따라오지 못하고
                // 한 프레임씩 뒤처져서 수류탄이 손에서 떨어져 떠 있는 것처럼 보인다.
                // 날아갈 때는 반대로 보간이 있어야 궤적이 부드럽다.
                body.interpolation = active ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            }

            foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = active;
        }

        /// <summary>지정한 시간에 걸쳐 목표값까지 가는 선형 보간 한 스텝.</summary>
        private static float Approach(float current, float target, float duration)
        {
            if (duration <= 0f) return target;
            return Mathf.MoveTowards(current, target, Time.deltaTime / duration);
        }

        /// <summary>보급 등으로 수류탄을 채운다.</summary>
        public void AddGrenades(int amount)
        {
            carriedGrenades = Mathf.Max(0, carriedGrenades + amount);
        }
    }
}
