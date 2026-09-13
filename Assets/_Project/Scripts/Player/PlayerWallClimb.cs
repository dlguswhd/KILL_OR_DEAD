using System.Collections.Generic;
using System.Reflection;
using KillOrDead.Interaction;
using KINEMATION.TacticalShooterPack.Scripts.Animation;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace KillOrDead.Player
{
    /// <summary>
    /// 벽타기 (H키). 애니메이션 3개를 한 동작으로 이어서 벽을 넘는다.
    ///
    /// <list type="number">
    /// <item><b>1단계 올라가기</b> — 1번 클립(<c>Climbing Up Wall</c>). 시작·끝 포즈가 같은 순환 동작이라
    /// 반복 재생으로 <b>어떤 높이의 벽이든</b> 올라갈 수 있다.</item>
    /// <item><b>2단계 넘어가기</b> — 2번 클립(<c>Climbing</c>)의 <b>2.12초~3.03초 구간만</b> 잘라 쓴다.
    /// 앞부분(달려오기·벽 오르기)과 뒷부분(벽 위에서 일어서기)은 버린다.</item>
    /// <item><b>3단계 착지</b> — 3번 클립(<c>Hard Landing</c>). 0.6초에 발이 닿고 그 뒤는 충격 흡수·기립이다.</item>
    /// </list>
    ///
    /// <b>왜 애니메이션의 이동량을 그대로 안 쓰고 직접 옮기는가</b><br/>
    /// 세 클립은 서로 다른 촬영본이라 각자 정해진 높이(합쳐서 약 2.2m 벽)만큼만 움직인다.
    /// 그대로 쓰면 벽 높이가 조금만 달라도 캐릭터가 벽을 뚫거나 공중에 뜬다.
    /// 그래서 <b>애니메이션에게는 자세(포즈)만 맡기고, 실제 이동은 이 스크립트가 계산한 경로로 처리한다.</b>
    /// (게임 업계에서 모션 워핑(motion warping)이라고 부르는 방식이다.)
    ///
    /// 구체적으로는 두 가지를 겹쳐서 쓴다.
    /// <list type="number">
    /// <item><b>루트 모션 추출</b> — 클립이 <c>root</c> 뼈를 움직인 만큼을 매 프레임 플레이어 본체로 옮기고,
    /// 뼈는 제자리로 되돌린다. 화면에 보이는 동작은 원본 그대로면서 <b>충돌 캡슐이 몸을 따라온다.</b></item>
    /// <item><b>경로 보정(워핑)</b> — 그것만으로는 클립이 정해 놓은 높이(약 2.2m)까지밖에 못 가므로,
    /// 목표 지점까지 남은 오차를 <b>남은 시간에 걸쳐 조금씩 나눠서</b> 더한다.
    /// 벽이 딱 맞으면 보정이 0에 가깝고, 높으면 그만큼 부드럽게 늘어난다.</item>
    /// </list>
    ///
    /// <b>왜 뼈를 그냥 고정하면 안 되는가</b> — 처음엔 <c>root</c> 뼈를 완전히 고정하는 방식으로 만들었는데,
    /// 이 클립들은 <c>root</c>가 앞뒤로 0.4m씩 흔들리고 골반이 그걸 반대로 상쇄하는 구조였다.
    /// 뼈를 고정하면 상쇄가 깨져서 <b>손이 벽에서 0.55m나 왔다 갔다 했다</b>(실측).
    /// 그래서 고정이 아니라 "옮기고 되돌리기"(추출)로 바꿨다.
    ///
    /// <b>1인칭 시점이 자연스러운 이유</b><br/>
    /// 카메라는 <c>head</c> 뼈의 자식이라 <b>위치</b>는 애니메이션을 따라 벽을 타고 올라간다.
    /// 반면 <b>회전</b>은 벤더 <c>FPSCameraAnimator</c>가 "몸통 회전 × 마우스 입력"으로 매 프레임 다시 쓰기 때문에
    /// 등반 동작이 화면을 마구 돌리지 않는다. 즉 오르는 느낌은 나면서 멀미는 안 난다.
    /// </summary>
    // 벤더 FPSCameraAnimator(실행 순서 0)보다 뒤에서 뼈를 정리하고, LeanCameraTilt(100)보다는 앞이다.
    [DefaultExecutionOrder(90)]
    [AddComponentMenu("KILL OR DEAD/Player/Player Wall Climb")]
    public class PlayerWallClimb : MonoBehaviour
    {
        private enum Phase
        {
            None,
            Up,     // 1번 — 벽면 기어오르기(반복)
            Over,   // 2번 — 벽 위로 넘어가기
            Land    // 3번 — 떨어져서 착지
        }

        private const string LayerName = "Climb";
        private const string StateEmpty = "Empty";
        private const string StateUp = "ClimbUp";
        private const string StateOver = "ClimbOver";
        private const string StateLand = "ClimbLand";
        private const string SpeedParam = "ClimbSpeed";

        // 벤더 TacticalProceduralAnimation은 총을 손에 붙이는 IK 출력을 protected로 감춰 둔다.
        // 구매 에셋을 고치지 않고 등반 중에만 그 출력을 꺼야 해서 리플렉션으로 읽어 온다.
        // (수류탄 투척 GrenadeThrower도 같은 방식으로 벤더 내부 상태를 읽는다.)
        private static readonly FieldInfo TacOutputField = typeof(TacticalProceduralAnimation)
            .GetField("_tacOutput", BindingFlags.NonPublic | BindingFlags.Instance);

        [Header("입력")]
        [Tooltip("벽타기 키. 기본 H.")]
        [SerializeField] private Key climbKey = Key.H;

        [Header("벽 찾기")]
        [Tooltip("이 거리 안에 있는 벽만 탈 수 있다(m).")]
        [SerializeField, Min(0.2f)] private float detectDistance = 1.1f;

        [Tooltip("벽을 찾을 때 레이를 쏘는 높이(발밑 기준 m). 가슴 높이가 적당하다.")]
        [SerializeField, Min(0.1f)] private float probeHeight = 1.15f;

        [Tooltip("벽을 이 각도 안으로 마주 보고 있어야 탈 수 있다(도). 옆이나 뒤를 보면 안 붙는다.")]
        [SerializeField, Range(5f, 90f)] private float maxFacingAngle = 55f;

        [Tooltip("이보다 낮은 벽은 그냥 넘을 수 있으니 등반 동작을 쓰지 않는다(m).")]
        [SerializeField, Min(0.3f)] private float minWallHeight = 1f;

        [Tooltip("이보다 높은 벽은 못 탄다(m). 벽마다 Climbable Wall에서 따로 지정할 수도 있다.")]
        [SerializeField, Min(0.5f)] private float maxWallHeight = 5f;

        [Tooltip("벽 두께를 재는 최대 범위(m). 이보다 두꺼운 벽은 윗면 끝을 못 찾는다.")]
        [SerializeField, Min(0.3f)] private float maxWallDepth = 2.5f;

        [Tooltip("벽·바닥을 찾을 때 검사할 레이어. 기본은 전부.")]
        [SerializeField] private LayerMask blockingMask = ~0;

        [Header("경로")]
        [Tooltip("등반 중 골반이 벽면에서 떨어져 있는 거리(m).\n" +
                 "손은 골반보다 0.28m쯤 앞에 있으므로 0.30이면 손이 벽에 거의 닿는다(실측).")]
        [SerializeField, Min(0f)] private float standoffDistance = 0.3f;

        [Tooltip("1단계가 끝날 때 캐릭터 기준점이 벽 윗면보다 얼마나 아래인지(m).\n" +
                 "손이 윗면 모서리를 딱 잡는 높이가 되도록 플레이 모드에서 실측해 맞춘 값이다.")]
        [SerializeField] private float hangDrop = 1.85f;

        [Tooltip("2단계가 끝날 때(벽 위에 올라섰을 때) 기준점이 벽 윗면보다 얼마나 아래인지(m).\n" +
                 "발바닥이 윗면에 닿도록 실측해 맞춘 값이다.")]
        [SerializeField] private float topDrop = 0.26f;

        [Tooltip("벽 반대편 바닥에 착지할 때 벽 끝에서 얼마나 더 나아가는지(m).")]
        [SerializeField, Min(0f)] private float landingForward = 0.7f;

        [Header("경로 보정")]
        [Tooltip("한 프레임에 root 뼈가 이만큼 넘게 움직이면 '이동'이 아니라 '클립 되감기'로 본다(m).\n" +
                 "1번 클립은 반복될 때 1.27m 아래로 순간이동하므로 그 사이 값으로 잡는다.")]
        [SerializeField, Min(0.05f)] private float rootJumpThreshold = 0.4f;

        [Tooltip("단계가 끝나갈 때 보정이 확 세지지 않게 잡는 최소 여유 시간(초).\n" +
                 "작을수록 목표에 정확히 붙지만 끝에서 툭 튈 수 있다.")]
        [SerializeField, Min(0.05f)] private float minWarpWindow = 0.3f;

        [Tooltip("벽까지의 거리를 맞추는 속도(1/초). 높이 보정과 달리 수평은 빨리 붙어야 한다.\n" +
                 "등반 내내 손이 벽에 닿아 있어야 하는데, 단계가 끝날 때쯤 도착하면 그동안 허공을 긁는다.\n" +
                 "너무 높이면 몸이 벽에 딱 고정돼서 좌우로 흔들리는 맛이 사라진다.")]
        [SerializeField, Min(0.1f)] private float flatWarpRate = 3.5f;

        [Header("속도")]
        [Tooltip("전체 등반 속도 배율. 1이면 클립 원래 속도, 2면 두 배로 빠르다.")]
        [SerializeField, Range(0.3f, 3f)] private float climbSpeed = 1f;

        [Tooltip("1번 클립이 실제로 올라가는 속도(m/초). 실측값 1.266m 나누기 2.0초 = 0.633.\n" +
                 "이 값으로 '벽 높이 → 등반 시간'을 계산한다. 건드리면 손발이 미끄러져 보인다.")]
        [SerializeField, Min(0.05f)] private float climbRiseRate = 0.633f;

        [Header("클립 자르기 (초 단위, 실측값)")]
        [Tooltip("2번 클립에서 벽을 넘는 부분이 시작하는 시간(초).")]
        [SerializeField, Min(0f)] private float overStartTime = 2.12f;

        [Tooltip("2번 클립에서 벽을 넘는 부분이 끝나는 시간(초).")]
        [SerializeField, Min(0f)] private float overEndTime = 3.03f;

        [Tooltip("3번 클립에서 발이 바닥에 닿는 시간(초). 이 시점까지가 낙하 구간이다.")]
        [SerializeField, Min(0.05f)] private float landTouchdownTime = 0.6f;

        [Tooltip("3번 클립을 여기까지만 쓴다(초). 뒤쪽은 가만히 서 있는 구간이라 잘라낸다.")]
        [SerializeField, Min(0.1f)] private float landEndTime = 2.1f;

        [Header("섞기")]
        [Tooltip("등반 레이어가 켜지는 시간(초).")]
        [SerializeField, Min(0f)] private float blendInTime = 0.15f;

        [Tooltip("등반 레이어가 꺼지는 시간(초).")]
        [SerializeField, Min(0f)] private float blendOutTime = 0.25f;

        [Tooltip("1단계에서 2단계, 2단계에서 3단계로 넘어갈 때 자세를 섞는 시간(초).")]
        [SerializeField, Min(0.01f)] private float phaseBlendTime = 0.22f;

        [Header("기타")]
        [Tooltip("등반 중에는 총을 감춘다. 두 손으로 벽을 잡는데 총이 손에 붙어 같이 휘둘리면 이상하다.")]
        [SerializeField] private bool hideWeaponWhileClimbing = true;

        // --- 참조 ---
        private CharacterController _controller;
        private Animator _animator;
        private Camera _camera;
        private Transform _rootBone;
        private Transform _pelvisBone;
        private TacticalProceduralAnimation _procedural;
        private TacticalShooterPlayer _player;
        private PlayerLocomotion _locomotion;
        private PlayerInteractor _interactor;
        private GrenadeThrower _grenade;
        private PlayerDoorKick _doorKick;

        // --- 진행 상태 ---
        private Phase _phase = Phase.None;
        private int _layerIndex = -1;
        private float _weight;
        private float _phaseElapsed;
        private float _phaseDuration;
        private Vector3 _phaseFrom;
        private Vector3 _phaseTo;

        // 경로. 시작점 → 매달린 곳 → 벽 위 → 착지점.
        private Vector3 _pStart, _pHang, _pTop, _pLand;

        // 루트 모션 추출용. 지난 프레임의 root 뼈 위치를 기억해 뒀다가 그 차이만큼 본체를 옮긴다.
        private Vector3 _prevRoot;
        private bool _hasPrevRoot;

        // 원래대로 되돌리기 위해 기억해 두는 값들.
        private Vector3 _restRootLocal;
        private bool _restRootCaptured;
        private bool _controllerWasEnabled;
        private bool _weaponInputsLocked;
        private bool _tacOutputMuted;
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();

        public bool InputEnabled { get; set; } = true;

        /// <summary>지금 벽을 타는 중인지. 다른 입력을 막고 싶을 때 확인한다.</summary>
        public bool IsClimbing => _phase != Phase.None;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>(true);
            _camera = GetComponentInChildren<Camera>(true);
            _procedural = GetComponent<TacticalProceduralAnimation>();
            if (_procedural == null) _procedural = GetComponentInChildren<TacticalProceduralAnimation>(true);
            _player = GetComponent<TacticalShooterPlayer>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _interactor = GetComponent<PlayerInteractor>();
            _grenade = GetComponent<GrenadeThrower>();
            _doorKick = GetComponent<PlayerDoorKick>();

            if (_animator != null)
            {
                foreach (var t in _animator.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "root") _rootBone = t;
                    else if (t.name == "pelvis") _pelvisBone = t;
                    if (_rootBone != null && _pelvisBone != null) break;
                }
            }
        }

        private void OnEnable()
        {
            // 무기를 바꾸면 컨트롤러가 통째로 갈리므로 레이어 번호를 그때그때 다시 찾는다.
            _layerIndex = -1;
        }

        private void OnDisable()
        {
            if (IsClimbing) Abort();
        }

        private void Update()
        {
            if (_animator == null) return;

            ResolveLayerIndex();
            if (_layerIndex < 0) return;

            if (IsClimbing) UpdateClimb();
            else if (WantsToClimb()) TryStartClimb();

            _animator.SetLayerWeight(_layerIndex, _weight);
        }

        /// <summary>
        /// 애니메이터가 자세를 다 잡은 뒤에 실제 이동을 처리한다.
        ///
        /// <b>여기가 이 시스템의 핵심</b>이다. 순서는 이렇다.
        /// <list type="number">
        /// <item>클립이 <c>root</c> 뼈를 이번 프레임에 움직인 만큼(delta)을 <b>플레이어 본체로 옮긴다.</b></item>
        /// <item>목표 지점까지 남은 오차를 <b>남은 시간에 나눠서</b> 조금 더한다(경로 보정).</item>
        /// <item><c>root</c> 뼈는 원래 자리로 되돌린다. 안 그러면 같은 이동이 두 번 적용된다.</item>
        /// </list>
        /// 이렇게 하면 화면에 보이는 몸의 움직임은 원본 애니메이션 그대로이면서,
        /// 도착 지점만 벽 높이에 맞게 늘었다 줄었다 한다.
        ///
        /// <c>Update</c>가 아니라 <c>LateUpdate</c>여야 한다. 애니메이터는 Update가 다 끝난 뒤에
        /// 자세를 쓰기 때문에, Update에서 뼈를 읽으면 <b>한 프레임 전 값</b>을 읽게 된다.
        /// </summary>
        private void LateUpdate()
        {
            if (!IsClimbing || _rootBone == null || !_restRootCaptured) return;

            // 클립이 이번 프레임에 써넣은 root 위치를 먼저 읽고, 곧바로 제자리로 되돌린다.
            // 되돌리기를 마지막에 하면 안 된다 — 아래에서 골반 위치를 읽을 때 클립의 root 오프셋
            // (2번 클립은 3.8m나 된다)이 그대로 섞여 들어와서 목표 지점이 엉뚱하게 계산된다.
            Vector3 clipRoot = _rootBone.localPosition;
            _rootBone.localPosition = _restRootLocal;

            ApplyClimbMovement(clipRoot);
        }

        private void ApplyClimbMovement(Vector3 cur)
        {
            Vector3 pos = transform.position;

            // ① 루트 모션 추출 — 클립이 움직인 만큼 본체를 옮긴다.
            //
            // 단, 아래 두 경우는 "캐릭터가 이동한 것"이 아니라 "클립이 갈아끼워진 것"이라 빼야 한다.
            //  · 단계가 바뀌며 클립을 섞는 동안(IsInTransition)
            //    세 클립은 촬영 당시 서 있던 자리가 제각각이라 root 위치가 서로 몇 m씩 차이 난다
            //    (1번 z=0.57 / 2번 2.12초 z=2.87 / 3번 z=0.35 — 2번엔 달려오는 구간이 들어 있어서다).
            //    섞이는 동안 그 차이가 그대로 이동으로 읽히면 캐릭터가 몇 m씩 날아간다(실제로 4m 날아갔다).
            //    화면에 보이는 자세는 어차피 부드럽게 섞이므로, 이 구간은 경로 보정에만 맡긴다.
            //  · 1번 클립이 되감기는 순간(반복 재생). root가 1.27m 아래로 순간이동한다.
            bool switchingClip = _animator.IsInTransition(_layerIndex);

            if (_hasPrevRoot && !switchingClip)
            {
                Vector3 d = cur - _prevRoot;
                if (d.sqrMagnitude < rootJumpThreshold * rootJumpThreshold)
                {
                    pos += _animator.transform.TransformVector(d);
                }
            }
            _prevRoot = cur;
            _hasPrevRoot = true;

            // 보정 세기. 남은 시간이 짧을수록 세게 당기되, 마지막에 확 튀지 않게 하한을 둔다.
            float remain = Mathf.Max(_phaseDuration - _phaseElapsed, minWarpWindow);
            float k = Mathf.Clamp01(Time.deltaTime / remain);

            // ② 수평 보정 — 목표는 "골반이 있어야 할 자리"다.
            // 본체(캡슐) 기준이 아니라 골반 기준으로 잡아야 하는 이유: 클립마다 root 뼈와 몸 사이의
            // 간격이 제각각이라(1번은 0.55m나 떨어져 있다) 본체 기준으로 맞추면 몸이 엉뚱한 데 가 있는다.
            Vector3 bodyOffset = _pelvisBone != null
                ? _pelvisBone.position - transform.position
                : Vector3.zero;
            bodyOffset.y = 0f;

            // 수평은 높이와 달리 '단계가 끝날 때 도착'하면 늦다. 등반 내내 손이 벽에 닿아 있어야 하므로
            // 남은 시간과 무관하게 일정한 속도로 빨리 붙인다(프레임률과 무관한 지수 감쇠).
            float kFlat = 1f - Mathf.Exp(-flatWarpRate * Time.deltaTime);
            Vector3 flatTarget = _phaseTo - bodyOffset;
            pos.x += (flatTarget.x - pos.x) * kFlat;
            pos.z += (flatTarget.z - pos.z) * kFlat;

            // ③ 수직 보정 — 착지 단계만 중력 곡선을 따로 쓴다.
            pos.y = _phase == Phase.Land ? LandingHeight() : pos.y + (_phaseTo.y - pos.y) * k;

            transform.position = pos;
        }

        private bool WantsToClimb()
        {
            if (!InputEnabled || Keyboard.current == null) return false;
            if (!Keyboard.current[climbKey].wasPressedThisFrame) return false;

            // 작업대 UI 등으로 조작이 잠겨 있으면 무시한다.
            if (_locomotion != null && !_locomotion.InputEnabled) return false;
            if (_interactor != null && !_interactor.InputEnabled) return false;

            // 수류탄을 들었거나 문을 차는 중이면 동작이 겹치니 막는다.
            if (_grenade != null && (_grenade.IsAiming || _grenade.IsThrowing)) return false;
            if (_doorKick != null && _doorKick.IsKicking) return false;

            return true;
        }

        // ───────────────────────── 벽 찾기 ─────────────────────────

        /// <summary>앞에 탈 수 있는 벽이 있으면 경로를 계산하고 등반을 시작한다.</summary>
        private void TryStartClimb()
        {
            if (!FindWall(out var hit, out var wall)) return;

            // 벽 바깥쪽(플레이어 쪽) 수평 법선과, 넘어갈 방향.
            Vector3 wallNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            if (wallNormal.sqrMagnitude < 0.01f) return;
            Vector3 forward = -wallNormal;

            // 벽을 어느 정도 마주 보고 있어야 한다.
            Vector3 looking = _camera != null ? _camera.transform.forward : transform.forward;
            looking = Vector3.ProjectOnPlane(looking, Vector3.up).normalized;
            if (Vector3.Angle(looking, forward) > maxFacingAngle) return;

            float feetY = transform.position.y;
            float limit = wall.MaxHeightOverride > 0f ? wall.MaxHeightOverride : maxWallHeight;

            if (!FindWallTop(hit.point, wallNormal, feetY, limit, out float wallTopY)) return;

            float height = wallTopY - feetY;
            if (height < minWallHeight || height > limit) return;

            float farEdge = MeasureWallDepth(hit.point, forward, wallTopY);

            // 경로 네 지점을 만든다. XZ는 벽에 맞은 지점 기준이다.
            Vector3 faceXZ = new Vector3(hit.point.x, 0f, hit.point.z);

            _pStart = faceXZ + wallNormal * standoffDistance;
            _pStart.y = feetY;

            _pHang = _pStart;
            _pHang.y = wallTopY - hangDrop;

            // 벽 위에 올라선 지점. 두께의 절반쯤 안으로 들어간 자리다(얇은 벽이면 거의 모서리).
            _pTop = faceXZ + forward * Mathf.Max(farEdge * 0.5f, 0.05f);
            _pTop.y = wallTopY - topDrop;

            if (!FindLanding(faceXZ, forward, farEdge, wallTopY, out _pLand)) return;

            // 매달린 지점이 시작 지점보다 낮으면(=너무 낮은 턱) 동작이 거꾸로 간다. 막는다.
            if (_pHang.y <= _pStart.y + 0.05f) return;

            BeginClimb(forward);
        }

        /// <summary>가슴 높이에서 앞으로 레이를 쏴서 탈 수 있는 벽을 찾는다.</summary>
        private bool FindWall(out RaycastHit hit, out ClimbableWall wall)
        {
            hit = default;
            wall = null;

            Vector3 origin = transform.position + Vector3.up * probeHeight;
            Vector3 dir = Vector3.ProjectOnPlane(
                _camera != null ? _camera.transform.forward : transform.forward, Vector3.up).normalized;
            if (dir.sqrMagnitude < 0.01f) return false;

            if (!Physics.Raycast(origin, dir, out hit, detectDistance, blockingMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // 콜라이더 자신이나 부모 중에 마커가 있는지 본다.
            wall = hit.collider.GetComponentInParent<ClimbableWall>();
            return wall != null && wall.Climbable;
        }

        /// <summary>벽 위에서 아래로 레이를 쏴서 윗면 높이를 잰다.</summary>
        private bool FindWallTop(Vector3 facePoint, Vector3 wallNormal, float feetY, float limit,
            out float wallTopY)
        {
            wallTopY = 0f;

            // 벽 표면에서 5cm 안쪽으로 들어간 자리에서 아래로 쏴야 윗면에 맞는다.
            Vector3 inside = facePoint - wallNormal * 0.05f;
            Vector3 from = new Vector3(inside.x, feetY + limit + 0.5f, inside.z);

            if (!Physics.Raycast(from, Vector3.down, out var top, limit + 1f, blockingMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            wallTopY = top.point.y;
            return true;
        }

        /// <summary>
        /// 벽 윗면을 앞으로 훑어서 반대쪽 모서리까지의 거리를 잰다(=벽 두께).
        /// 콜라이더 모양에 상관없이 통하는 방식이라 회전한 벽이나 여러 조각으로 된 벽에도 쓸 수 있다.
        /// </summary>
        private float MeasureWallDepth(Vector3 facePoint, Vector3 forward, float wallTopY)
        {
            const float step = 0.05f;
            float depth = step;

            for (float d = step; d <= maxWallDepth; d += step)
            {
                Vector3 probe = new Vector3(facePoint.x, wallTopY + 0.3f, facePoint.z) + forward * d;
                bool onTop = Physics.Raycast(probe, Vector3.down, out var h, 0.6f, blockingMask,
                                 QueryTriggerInteraction.Ignore)
                             && Mathf.Abs(h.point.y - wallTopY) < 0.15f;

                if (!onTop) break;
                depth = d;
            }

            return depth;
        }

        /// <summary>벽 너머에 내려설 바닥이 있는지 찾고, 설 자리가 비어 있는지 확인한다.</summary>
        private bool FindLanding(Vector3 faceXZ, Vector3 forward, float farEdge, float wallTopY,
            out Vector3 landing)
        {
            landing = default;

            Vector3 landXZ = faceXZ + forward * (farEdge + landingForward);
            Vector3 from = new Vector3(landXZ.x, wallTopY + 0.5f, landXZ.z);

            if (!Physics.Raycast(from, Vector3.down, out var ground, wallTopY + maxWallHeight + 5f,
                    blockingMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            landing = ground.point;

            // 착지 지점에 사람이 설 공간이 있는지 캡슐로 확인한다. 상자나 다른 벽이 있으면 취소.
            float radius = _controller != null ? _controller.radius : 0.3f;
            float height = _controller != null ? _controller.height : 1.8f;
            Vector3 bottom = landing + Vector3.up * (radius + 0.05f);
            Vector3 top = landing + Vector3.up * Mathf.Max(height - radius, radius + 0.1f);

            return !Physics.CheckCapsule(bottom, top, radius * 0.9f, blockingMask,
                QueryTriggerInteraction.Ignore);
        }

        // ───────────────────────── 등반 진행 ─────────────────────────

        private void BeginClimb(Vector3 forward)
        {
            // 몸을 벽 정면으로 돌린다. 카메라 각도는 "몸통 회전 + 곁눈질(yawInput)"이라
            // 돌린 만큼 곁눈질에서 빼 주면 화면은 그대로 있는다(PlayerLocomotion과 같은 방식이다).
            float targetYaw = Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y;
            float delta = Mathf.DeltaAngle(transform.eulerAngles.y, targetYaw);
            transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
            if (_procedural != null)
            {
                _procedural.yawInput = Mathf.Clamp(_procedural.yawInput - delta, -90f, 90f);
            }

            transform.position = _pStart;

            _hasPrevRoot = false;   // 루트 모션 추출 기준을 새로 잡는다
            CaptureRestRoot();
            LockPlayer();
            MuteVendorProceduralOutput(true);
            if (hideWeaponWhileClimbing) HideWeapon();

            _weight = 0f;
            if (HasParameter(SpeedParam)) _animator.SetFloat(SpeedParam, climbSpeed);

            // 1단계는 벽 높이만큼 반복 재생한다. 클립이 실제로 올라가는 속도로 시간을 잡아야
            // 손발이 벽 위를 미끄러지지 않는다.
            float rise = Mathf.Max(_pHang.y - _pStart.y, 0.05f);
            EnterPhase(Phase.Up, _pHang, rise / Mathf.Max(climbRiseRate * climbSpeed, 0.01f));
            _animator.CrossFadeInFixedTime(StateUp, blendInTime, _layerIndex, 0f);
        }

        /// <summary>
        /// 단계를 시작한다. 시작점은 계획된 값이 아니라 <b>지금 실제로 있는 자리</b>를 쓴다.
        /// 앞 단계에서 오차가 조금 남아 있어도 다음 단계가 거기서부터 이어받아 스스로 메우게 하려는 것이다.
        /// </summary>
        private void EnterPhase(Phase phase, Vector3 to, float duration)
        {
            _phase = phase;
            _phaseFrom = transform.position;
            _phaseTo = to;
            _phaseDuration = Mathf.Max(duration, 0.05f);
            _phaseElapsed = 0f;
        }

        /// <summary>
        /// 단계 진행만 담당한다. 실제 이동은 <see cref="ApplyClimbMovement"/>(LateUpdate)가 한다.
        /// </summary>
        private void UpdateClimb()
        {
            _phaseElapsed += Time.deltaTime;
            bool done = _phaseElapsed >= _phaseDuration;

            switch (_phase)
            {
                case Phase.Up:
                    _weight = Approach(_weight, 1f, blendInTime);
                    if (done) StartOverPhase();
                    break;

                case Phase.Over:
                    _weight = 1f;
                    if (done) StartLandPhase();
                    break;

                case Phase.Land:
                    if (done) FinishClimb();
                    break;
            }
        }

        private void StartOverPhase()
        {
            float duration = Mathf.Max(overEndTime - overStartTime, 0.1f) / climbSpeed;
            EnterPhase(Phase.Over, _pTop, duration);

            // 2번 클립의 벽 넘는 구간만 쓰기 위해, 그 시작 시간부터 재생시킨다.
            _animator.CrossFadeInFixedTime(StateOver, phaseBlendTime, _layerIndex, overStartTime);
        }

        private void StartLandPhase()
        {
            EnterPhase(Phase.Land, _pLand, Mathf.Max(landEndTime, 0.2f) / climbSpeed);
            _animator.CrossFadeInFixedTime(StateLand, phaseBlendTime, _layerIndex, 0f);
        }

        /// <summary>
        /// 착지 단계의 <b>높이</b>. 낙하 구간과 착지 후 구간을 나눠서 계산한다.
        ///
        /// 떨어지는 동안에는 중력처럼 점점 빨라지고(제곱 곡선), 발이 닿은 뒤로는 높이를 고정한다.
        /// 이렇게 나누지 않으면 발이 바닥에 닿은 뒤에도 몸이 계속 내려가 바닥에 파묻힌다.
        ///
        /// 이 단계만 루트 모션 추출을 쓰지 않는 이유: 3번 클립은 <b>1.7m 낙하 기준</b>으로 만들어졌는데
        /// 실제 벽 높이는 그보다 훨씬 클 수 있어서, 클립의 낙하량을 그대로 쓰면 발이 땅에 안 닿는다.
        /// 착지 타이밍은 어긋나면 바로 티가 나는 부분이라 높이만 직접 계산한다.
        /// </summary>
        private float LandingHeight()
        {
            float touchdown = Mathf.Max(landTouchdownTime, 0.05f) / climbSpeed;
            float f = Mathf.Clamp01(_phaseElapsed / touchdown);
            return Mathf.Lerp(_phaseFrom.y, _phaseTo.y, f * f);
        }

        private void FinishClimb()
        {
            // 목표 착지점으로 순간이동시키지 않는다. 보정이 이미 10cm 안쪽까지 데려다 놓았는데
            // 거기서 딱 맞추려고 옮기면 서 있는 캐릭터가 옆으로 툭 미끄러지는 게 눈에 보인다.
            // 높이만 바닥에 맞춰서 공중에 뜬 채로 끝나지 않게 한다.
            var pos = transform.position;
            pos.y = _pLand.y;
            transform.position = pos;

            _animator.CrossFadeInFixedTime(StateEmpty, blendOutTime, _layerIndex, 0f);

            _phase = Phase.None;
            _weight = 0f;

            MuteVendorProceduralOutput(false);
            RestoreWeapon();
            UnlockPlayer();
        }

        /// <summary>중간에 끊겼을 때(컴포넌트 비활성화 등) 무조건 원래대로 돌려놓는다.</summary>
        private void Abort()
        {
            _phase = Phase.None;
            _weight = 0f;

            if (_animator != null && _layerIndex >= 0)
            {
                _animator.SetLayerWeight(_layerIndex, 0f);
                _animator.Play(StateEmpty, _layerIndex, 0f);
            }

            MuteVendorProceduralOutput(false);
            RestoreWeapon();
            UnlockPlayer();
        }

        // ───────────────────────── 켜고 끄기 ─────────────────────────

        /// <summary>등반 시작 전의 <c>root</c> 뼈 위치를 기억해 둔다. 매 프레임 여기로 되돌린다.</summary>
        private void CaptureRestRoot()
        {
            if (_rootBone == null) return;
            _restRootLocal = _rootBone.localPosition;
            _restRootCaptured = true;
        }

        private void LockPlayer()
        {
            _controllerWasEnabled = _controller != null && _controller.enabled;
            if (_controller != null) _controller.enabled = false;

            if (_locomotion != null) _locomotion.InputEnabled = false;
            if (_interactor != null) _interactor.InputEnabled = false;
            if (_grenade != null) _grenade.InputEnabled = false;
            if (_doorKick != null) _doorKick.InputEnabled = false;

            // TSP의 기존 동작 잠금을 재사용하면 R/X/I/M/C와 마우스 왼쪽 클릭이 함께 막힌다.
            if (_player != null && !_weaponInputsLocked)
            {
                _weaponInputsLocked = true;
                var weapon = _player.GetPrimaryWeapon();
                if (weapon != null && weapon.IsFiring) weapon.StopFiring();
                _player.OnActionStarted();
            }
        }

        private void UnlockPlayer()
        {
            if (_controller != null) _controller.enabled = _controllerWasEnabled;

            if (_locomotion != null) _locomotion.InputEnabled = true;
            if (_interactor != null) _interactor.InputEnabled = true;
            if (_grenade != null) _grenade.InputEnabled = true;
            if (_doorKick != null) _doorKick.InputEnabled = true;

            if (_weaponInputsLocked)
            {
                _weaponInputsLocked = false;
                if (_player != null) _player.OnActionEnded();
            }
        }

        /// <summary>
        /// 벤더의 총기 IK 출력을 잠시 끈다.
        ///
        /// <c>TacticalShooterPlayerJob</c>은 매 프레임 <b>두 손을 총 위치로 강제로 끌어다 붙이고</b>
        /// 척추를 조준 방향으로 다시 돌린다. 이걸 켜둔 채 등반 클립을 틀면 팔이 벽이 아니라
        /// 총을 잡은 자세로 되돌아가서 동작이 완전히 망가진다.
        /// 그래서 등반하는 동안만 그 출력의 가중치를 0으로 내렸다가 끝나면 되돌린다.
        /// </summary>
        private void MuteVendorProceduralOutput(bool mute)
        {
            if (mute == _tacOutputMuted) return;
            if (_procedural == null || TacOutputField == null) return;

            var boxed = TacOutputField.GetValue(_procedural);
            if (boxed is not AnimationPlayableOutput output) return;
            if (!output.IsOutputValid()) return;

            output.SetWeight(mute ? 0f : 1f);
            _tacOutputMuted = mute;
        }

        private void HideWeapon()
        {
            _hiddenRenderers.Clear();
            if (_player == null) return;

            var weapon = _player.GetActiveWeapon();
            if (weapon == null) return;

            foreach (var renderer in weapon.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                renderer.enabled = false;
                _hiddenRenderers.Add(renderer);
            }
        }

        private void RestoreWeapon()
        {
            foreach (var renderer in _hiddenRenderers)
            {
                if (renderer != null) renderer.enabled = true;
            }
            _hiddenRenderers.Clear();
        }

        private void ResolveLayerIndex()
        {
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

        private bool HasParameter(string name)
        {
            foreach (var p in _animator.parameters)
            {
                if (p.name == name) return true;
            }
            return false;
        }

        /// <summary>지정한 시간에 걸쳐 목표값까지 가는 선형 보간 한 스텝.</summary>
        private static float Approach(float current, float target, float duration)
        {
            if (duration <= 0f) return target;
            return Mathf.MoveTowards(current, target, Time.deltaTime / duration);
        }
    }
}
