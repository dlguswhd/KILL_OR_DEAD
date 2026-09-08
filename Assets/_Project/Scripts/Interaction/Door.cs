using KillOrDead.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KillOrDead.Interaction
{
    /// <summary>
    /// F키로 여는 문. FreeOpenBuilding 문 프리팹에 붙여서 쓴다.
    ///
    /// <para><b>여는 방식은 두 가지</b>이고 플레이어가 마우스 휠로 고른다
    /// (<see cref="DoorOpenModeSelector"/>).</para>
    /// <list type="bullet">
    /// <item>손으로 열기 — 천천히 열린다. F를 다시 누르면 닫힌다.</item>
    /// <item>발로 차기 — 왼발 발차기 동작(<see cref="PlayerDoorKick"/>)을 재생하고,
    /// <b>발이 문에 닿는 순간</b> 벌컥 열린다. 살짝 튕겼다가 제자리를 잡는다.</item>
    /// </list>
    ///
    /// <para>문은 <b>항상 플레이어 반대쪽으로</b> 밀려서 열린다. 얼굴 쪽으로 열려서 끼이는 일이 없다.</para>
    ///
    /// <para>벤더 에셋의 <c>DoorMech</c>는 쓰지 않는다. 그쪽은 구형 입력(E키)과 트리거 방식이라
    /// 이 프로젝트의 상호작용 시스템·Input System과 맞지 않는다. 벤더 파일은 그대로 두고 이 스크립트를 새로 붙인다.</para>
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Interaction/Door")]
    public class Door : MonoBehaviour, IInteractable
    {
        [Header("열림 각도")]
        [Tooltip("활짝 열렸을 때 돌아가는 각도(도).")]
        [SerializeField, Range(30f, 130f)] private float openAngle = 95f;

        [Header("속도 (초당 도)")]
        [Tooltip("손으로 열 때의 속도.")]
        [SerializeField, Min(1f)] private float normalOpenSpeed = 140f;

        [Tooltip("닫을 때의 속도.")]
        [SerializeField, Min(1f)] private float closeSpeed = 160f;

        [Tooltip("발로 찼을 때의 속도. 확 열려야 하므로 훨씬 빠르다.")]
        [SerializeField, Min(1f)] private float kickOpenSpeed = 900f;

        [Header("발차기 반동")]
        [Tooltip("발로 차면 열림 각도보다 이만큼 더 밀렸다가 되돌아온다. 0이면 반동 없음.")]
        [SerializeField, Range(0f, 40f)] private float kickOvershootAngle = 18f;

        [Tooltip("더 밀린 만큼 되돌아오는 속도.")]
        [SerializeField, Min(1f)] private float kickSettleSpeed = 220f;

        [Header("안내 문구")]
        [Tooltip("화면에 뜨는 이름. '문', '현관문'처럼 적는다.")]
        [SerializeField] private string doorName = "문";

        private Quaternion _closedRotation;

        /// <summary>지금 열린 각도(0이면 닫힘). 항상 0 이상이고, 어느 쪽으로 열렸는지는 <see cref="_openSign"/>가 따로 들고 있다.</summary>
        private float _angle;
        private float _targetAngle;
        private float _speed;

        /// <summary>열리는 방향. +1 또는 -1. 문을 열기 시작할 때 플레이어 위치를 보고 정한다.</summary>
        private float _openSign = 1f;

        /// <summary>발차기로 더 밀린 상태에서 제자리로 돌아오는 중인지.</summary>
        private bool _overshooting;

        /// <summary>문짝이 뻗어 있는 방향(로컬). 경첩에서 손잡이 쪽을 가리킨다.</summary>
        private Vector3 _localWidthDir = Vector3.forward;

        /// <summary>문의 앞뒤 방향(로컬). 문짝 평면의 법선이고, 플레이어가 어느 쪽에 서 있는지 판단하는 데 쓴다.</summary>
        private Vector3 _localFaceDir = Vector3.right;

        /// <summary>발차기 동작을 재생 중인 플레이어. 발이 닿는 순간을 알려주면 문을 연다.</summary>
        private PlayerDoorKick _pendingKick;
        private float _pendingKickSign = 1f;

        /// <summary>여는 방식을 고르는 플레이어 컴포넌트. 매 프레임 씬을 뒤지지 않으려고 들고 있는다.</summary>
        private DoorOpenModeSelector _modeSelector;

        /// <summary>문이 (조금이라도) 열려 있는지.</summary>
        public bool IsOpen => _targetAngle > 0.01f;

        public Key InteractKey => Key.F;

        private void Awake()
        {
            _closedRotation = transform.localRotation;
            ResolveDoorAxes();
        }

        private void OnDisable()
        {
            UnsubscribeKick();
        }

        /// <summary>
        /// 문짝이 어느 쪽으로 뻗어 있고 어느 쪽이 앞면인지 알아낸다.
        ///
        /// 이 프리팹들은 <b>피벗(회전 중심)이 경첩</b>에 있어서, 문짝의 무게중심은 항상 손잡이 쪽으로 치우쳐 있다.
        /// 그래서 <b>치우친 쪽이 문짝이 뻗은 방향</b>이고, 나머지 수평 방향이 문의 앞뒤가 된다.
        /// (예: Door_Room_LOD는 중심이 로컬 z로 +0.67 → 폭 방향은 z, 앞뒤는 x)
        /// </summary>
        private void ResolveDoorAxes()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            var local = transform.InverseTransformPoint(bounds.center);

            if (Mathf.Abs(local.x) >= Mathf.Abs(local.z))
            {
                _localWidthDir = new Vector3(Mathf.Sign(local.x), 0f, 0f);
                _localFaceDir = Vector3.forward;
            }
            else
            {
                _localWidthDir = new Vector3(0f, 0f, Mathf.Sign(local.z));
                _localFaceDir = Vector3.right;
            }
        }

        public string GetPrompt()
        {
            if (IsOpen) return $"{doorName} 닫기";

            var mode = CurrentMode();
            return mode == DoorOpenMode.Kick
                ? $"발로 차서 {doorName} 열기   (휠: 손으로 열기)"
                : $"{doorName} 열기   (휠: 발로 차기)";
        }

        public void Interact(GameObject interactor)
        {
            if (IsOpen)
            {
                Close();
                return;
            }

            if (CurrentMode() == DoorOpenMode.Kick)
            {
                var kick = interactor.GetComponentInChildren<PlayerDoorKick>();
                if (kick != null)
                {
                    // 이미 차는 중이거나 연타 방지 시간이면 아무 일도 일어나지 않는다.
                    // 여기서 손으로 여는 쪽으로 넘어가면 발차기를 고른 채로 문이 스르륵 열려서 어색하다.
                    if (!kick.CanKick) return;
                    if (TryKickOpen(interactor)) return;
                }
            }

            OpenNormally(interactor.transform.position);
        }

        /// <summary>
        /// 플레이어가 지금 고른 여는 방식. 고르는 컴포넌트가 없으면 손으로 여는 것으로 본다.
        ///
        /// 안내 문구를 매 프레임 만들기 때문에 씬 전체 검색은 <b>한 번만</b> 하고 결과를 들고 있는다.
        /// </summary>
        private DoorOpenMode CurrentMode()
        {
            if (_modeSelector == null)
            {
                _modeSelector = FindFirstObjectByType<DoorOpenModeSelector>();
            }

            return _modeSelector != null ? _modeSelector.Mode : DoorOpenMode.Normal;
        }

        private void OpenNormally(Vector3 interactorPosition)
        {
            _openSign = ComputeOpenSign(interactorPosition);
            _targetAngle = openAngle;
            _speed = normalOpenSpeed;
            _overshooting = false;
        }

        private void Close()
        {
            _targetAngle = 0f;
            _speed = closeSpeed;
            _overshooting = false;
        }

        /// <summary>
        /// 발차기 동작을 시작시킨다. 문은 지금 열리지 않고, <b>발이 닿는 순간</b>(<see cref="PlayerDoorKick.KickImpact"/>)에 열린다.
        /// 쿨다운 등으로 못 차면 false를 돌려주고, 그때는 손으로 여는 방식으로 넘어간다.
        /// </summary>
        private bool TryKickOpen(GameObject interactor)
        {
            var kick = interactor.GetComponentInChildren<PlayerDoorKick>();
            if (kick == null || !kick.CanKick) return false;

            UnsubscribeKick();

            _pendingKickSign = ComputeOpenSign(interactor.transform.position);
            _pendingKick = kick;
            kick.KickImpact += OnKickImpact;

            if (kick.TryKick()) return true;

            // 못 찼으면 구독을 되돌린다.
            UnsubscribeKick();
            return false;
        }

        private void OnKickImpact()
        {
            UnsubscribeKick();

            _openSign = _pendingKickSign;
            _targetAngle = openAngle + kickOvershootAngle;
            _speed = kickOpenSpeed;
            _overshooting = kickOvershootAngle > 0f;
        }

        private void UnsubscribeKick()
        {
            if (_pendingKick == null) return;
            _pendingKick.KickImpact -= OnKickImpact;
            _pendingKick = null;
        }

        /// <summary>
        /// 어느 쪽으로 열지 정한다. 문은 <b>플레이어 반대쪽</b>으로 밀린다.
        /// </summary>
        private float ComputeOpenSign(Vector3 interactorPosition)
        {
            var localInteractor = transform.InverseTransformPoint(interactorPosition);

            // 플레이어가 앞면(+face) 쪽에 있으면 문은 뒷면(-face) 쪽으로 밀려야 한다.
            float pushDirection = Vector3.Dot(localInteractor, _localFaceDir) >= 0f ? -1f : 1f;

            // +각도로 돌렸을 때 문짝 끝이 향하는 쪽이 앞면인지 뒷면인지 확인한다.
            var rotatedWidth = Quaternion.Euler(0f, 90f, 0f) * _localWidthDir;
            float positiveGoesToFace = Vector3.Dot(rotatedWidth, _localFaceDir) >= 0f ? 1f : -1f;

            return pushDirection * positiveGoesToFace;
        }

        private void Update()
        {
            if (Mathf.Approximately(_angle, _targetAngle))
            {
                // 발차기로 더 밀린 상태에 도달했으면, 이제 원래 열림 각도로 되돌아온다.
                if (_overshooting)
                {
                    _overshooting = false;
                    _targetAngle = openAngle;
                    _speed = kickSettleSpeed;
                }

                return;
            }

            _angle = Mathf.MoveTowards(_angle, _targetAngle, _speed * Time.deltaTime);
            transform.localRotation = _closedRotation * Quaternion.Euler(0f, _angle * _openSign, 0f);
        }
    }
}
