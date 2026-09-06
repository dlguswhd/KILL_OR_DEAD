using System;
using System.Collections.Generic;
using System.Linq;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using KillOrDead.Attachments;
using KillOrDead.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace KillOrDead.Interaction
{
    [AddComponentMenu("KILL OR DEAD/Interaction/Weapon Customization Table")]
    public class WeaponCustomizationTable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform previewAnchor;
        [Tooltip("모딩 중 카메라가 바라볼 방향(회전)을 정한다. 거리는 무기 크기에 맞춰 자동 계산되므로 위치는 참고용이다.")]
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private float cameraTransitionDuration = 0.4f;
        [Tooltip("무기 주변에 남길 여백(m). 작을수록 카메라가 바짝 붙지만, 너무 줄이면 총구 앞 UI 박스가 화면 밖으로 나간다.")]
        // 무기 둘레에 남기는 여백(m). 슬롯 UI 박스가 화면 밖으로 잘리지 않게 하는 완충 공간이라
        // 너무 줄이면 총구 앞 MUZZLE 박스가 잘린다.
        [SerializeField] private float framingMargin = 0.14f;
        [Tooltip("모딩 화면 화각. 넓힐수록 같은 크기로 보이면서 카메라가 작업대에 더 가까이 붙는다.")]
        [SerializeField] private float moddingFieldOfView = 95f;
        [Tooltip("카메라가 무기에서 떨어질 수 있는 최대 거리(m). 계산이 틀어져도 카메라가 날아가지 않게 막는 상한선.")]
        [SerializeField] private float maxCameraDistance = 3f;
        [Header("Zoom (마우스 휠)")]
        [Tooltip("휠을 올렸을 때 가장 가까이 붙는 배율. 1보다 작을수록 가깝다.")]
        [SerializeField, Range(0.2f, 1f)] private float minZoom = 0.45f;

        [Tooltip("휠을 내렸을 때 가장 멀어지는 배율.")]
        [SerializeField, Range(1f, 3f)] private float maxZoom = 1.8f;

        [Tooltip("휠 한 칸에 바뀌는 배율.")]
        [SerializeField, Min(0.01f)] private float zoomStep = 0.09f;

        [Tooltip("줌이 목표 배율까지 따라가는 속도. 클수록 빠릿하다.")]
        [SerializeField, Min(0.1f)] private float zoomSmoothing = 12f;

        [Header("Flip (반대쪽 옆면 보기)")]
        [Tooltip("이 키를 누르면 총을 가로로 180도 돌려서 반대쪽 옆면을 본다. 다시 누르면 원래대로.")]
        [SerializeField] private Key flipKey = Key.Y;

        [Tooltip("총이 반 바퀴 도는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.05f)] private float flipDuration = 0.45f;

        [SerializeField] private GameObject[] weaponPreviewPrefabs;
        [SerializeField] private AttachmentDefinition[] availableAttachments;

        private sealed class SlotBox
        {
            public AttachmentSlotType Slot;
            public Transform Socket;
            public RectTransform Rect;
            public Image Background;
            public Text ValueText;
            public Vector2 ScreenOffset;
        }

        private Dictionary<AttachmentSlotType, List<AttachmentDefinition>> _bySlot;

        private bool _active;
        private bool _closing;
        private float _transition;

        private GameObject _previewInstance;
        private WeaponAttachmentController _previewAttachments;
        private WeaponAttachmentController _realAttachments;
        private List<AttachmentSlotType> _availableSlots = new();
        private readonly List<SlotBox> _slotBoxes = new();
        private int _selectedSlotIndex;

        // PlayerInteractor가 T를 써서 이 작업대를 연 프레임. 같은 프레임에 닫기로 다시 쓰이는 걸 막는다.
        private int _inputConsumedFrame = -1;

        // 자동 계산된 기본 구도. 줌은 이 거리에 배율을 곱해서 만든다.
        private Vector3 _framingCenter;
        private float _framingDistance;
        private float _framingMinDistance;

        // 마우스 휠 줌 배율. 1 = 기본 구도.
        private float _zoom = 1f;
        private float _zoomTarget = 1f;

        // 총을 가로로 돌려 반대쪽 옆면을 보는 상태. 0도 = 처음 보이는 면, 180도 = 반대쪽 면.
        private float _flipAngle;
        private float _flipTargetAngle;

        // 돌리는 기준점과 원래 자세. 총 피벗이 중심이 아닐 수 있어서, 바운즈 중심을 축으로 돌린다.
        private Vector3 _previewPivot;
        private Vector3 _previewBasePosition;
        private Quaternion _previewBaseRotation;

        private PlayerLocomotion _locomotion;
        private PlayerInteractor _interactor;
        private PlayerInput _playerInput;
        private Camera _playerCamera;
        private Camera _moddingCamera;
        private Vector3 _cameraTargetPos;
        private Quaternion _cameraTargetRot;
        private readonly List<Renderer> _hiddenRenderers = new();

        private GameObject _canvasGo;
        private RectTransform _canvasRect;

        private void Awake()
        {
            _bySlot = new Dictionary<AttachmentSlotType, List<AttachmentDefinition>>();

            BuildModdingCamera();
            BuildUI();
        }

        /// <summary>
        /// 지금 올려놓은 무기에 실제로 달 수 있는 부착물만 슬롯별로 추린다.
        /// 무기를 바꿔 들고 작업대를 열 때마다 다시 만든다 — 라이플을 올렸을 때 권총용 부착물이,
        /// 권총을 올렸을 때 라이플용 부착물이 목록에 뜨면 안 되기 때문이다.
        /// </summary>
        private void RebuildSlotOptions(WeaponAttachmentController weapon)
        {
            _bySlot.Clear();
            if (weapon == null || availableAttachments == null) return;

            // 부착물 하나가 여러 자리에 들어갈 수 있다(측면 레일 위/아래). 그래서 슬롯을 돌면서
            // "이 자리에 들어가는가"를 물어보는 방식으로 목록을 만든다.
            foreach (var slot in Enum.GetValues(typeof(AttachmentSlotType)).Cast<AttachmentSlotType>())
            {
                if (!weapon.HasSocket(slot)) continue;

                foreach (var def in availableAttachments)
                {
                    if (def == null || !weapon.Accepts(def)) continue;
                    if (!WeaponAttachmentController.FitsSlot(def, slot)) continue;

                    if (!_bySlot.TryGetValue(slot, out var list))
                    {
                        list = new List<AttachmentDefinition>();
                        _bySlot[slot] = list;
                    }

                    list.Add(def);
                }
            }
        }

        private void BuildModdingCamera()
        {
            var camGo = new GameObject("ModdingCamera");
            camGo.transform.SetParent(transform, false);
            _moddingCamera = camGo.AddComponent<Camera>();
            _moddingCamera.enabled = false;

            // 카메라 기본 근거리 클리핑은 0.3m다. 모딩 카메라는 무기 크기에 맞춰 바짝 붙기 때문에
            // 권총처럼 작은 무기(22cm)에서는 카메라가 0.21m까지 다가가 무기가 통째로 잘려 화면이 비어버린다.
            // 라이플(80cm)은 0.35~0.45m에 서기 때문에 이 문제가 안 드러났었다.
            _moddingCamera.nearClipPlane = 0.01f;
        }

        public string GetPrompt()
        {
            return _active ? "Close" : "Customize Weapon";
        }

        public void Interact(GameObject interactor)
        {
            if (_active) BeginClose();
            else Open(interactor);

            // PlayerInteractor와 이 스크립트는 같은 T키를 본다. 스크립트 실행 순서가 정해져 있지 않아서,
            // PlayerInteractor가 먼저 돌아 여는 프레임에 이 스크립트의 Update가 뒤이어 돌면
            // 똑같은 입력을 "닫기"로 받아 같은 프레임에 바로 닫혀버린다(화면이 깜빡이고 아무 일도 안 일어남).
            // 그래서 이 프레임의 T 입력은 이미 처리된 것으로 표시해 둔다.
            _inputConsumedFrame = Time.frameCount;
        }

        private void Open(GameObject interactor)
        {
            var player = interactor.GetComponent<TacticalShooterPlayer>();
            if (player == null) return;

            var realWeapon = player.GetActiveWeapon();
            var prefab = FindMatchingPrefab(realWeapon.gameObject.name);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"WeaponCustomizationTable: no preview prefab configured for '{realWeapon.gameObject.name}'.");
                return;
            }

            _realAttachments = realWeapon.GetComponent<WeaponAttachmentController>();

            _previewInstance = Instantiate(prefab, previewAnchor.position, previewAnchor.rotation);
            _previewAttachments = _previewInstance.GetComponentInChildren<WeaponAttachmentController>();

            // 돌리기 기준을 잡아둔다. 총 피벗이 총 중심이 아닐 수 있어서,
            // 그냥 자기 축으로 돌리면 총이 옆으로 휙 쓸려 나간다. 바운즈 중심을 축으로 삼는다.
            _previewBasePosition = previewAnchor.position;
            _previewBaseRotation = previewAnchor.rotation;
            _previewPivot = TryGetPreviewBounds(out var previewBounds)
                ? previewBounds.center
                : _previewBasePosition;
            _flipAngle = 0f;
            _flipTargetAngle = 0f;
            _zoom = 1f;
            _zoomTarget = 1f;

            // 이 무기 종류에 맞는 부착물만 추린 뒤, 실제로 고를 게 있는 슬롯만 화면에 띄운다.
            RebuildSlotOptions(_previewAttachments);

            _availableSlots = Enum.GetValues(typeof(AttachmentSlotType))
                .Cast<AttachmentSlotType>()
                .Where(slot => _previewAttachments.HasSocket(slot) && _bySlot.ContainsKey(slot))
                .ToList();

            // 모딩 테이블은 실제 총의 현재 부착물 상태를 그대로 비춰주는 거울이라, 프리뷰를 실제 총과 동기화한 채로 시작한다.
            foreach (var slot in _availableSlots)
            {
                var equipped = _realAttachments.GetEquipped(slot);
                if (equipped != null) _previewAttachments.EquipAttachment(equipped, slot);
            }

            // 프리뷰는 보여주기용 모형이라 장치는 꺼 둔다.
            // 켜져 있으면 레이저 빔이 작업실을 가로질러 100m를 뻗고, 후레쉬가 화면을 하얗게 태운다.
            _previewAttachments.SetLaser(false);
            _previewAttachments.SetFlashlight(false);

            _selectedSlotIndex = 0;

            _locomotion = interactor.GetComponent<PlayerLocomotion>();
            _interactor = interactor.GetComponent<PlayerInteractor>();
            _playerInput = interactor.GetComponent<PlayerInput>();

            if (_locomotion != null) _locomotion.InputEnabled = false;
            if (_interactor != null) _interactor.InputEnabled = false;
            if (_playerInput != null) _playerInput.enabled = false;

            // 1인칭 카메라를 끄고 작업대 위 전용 카메라로 전환한다. Kinemation의 카메라 애니메이터가
            // 매 프레임 1인칭 카메라 트랜스폼을 덮어쓰기 때문에, 트랜스폼을 옮기는 대신 카메라 자체를 갈아끼운다.
            _playerCamera = interactor.GetComponentInChildren<Camera>();
            if (_playerCamera != null)
            {
                _moddingCamera.transform.SetPositionAndRotation(_playerCamera.transform.position,
                    _playerCamera.transform.rotation);
                _moddingCamera.fieldOfView = _playerCamera.fieldOfView;
                _playerCamera.enabled = false;
            }

            _moddingCamera.enabled = true;
            ComputeCameraFraming();
            HidePlayerRenderers(interactor);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            BuildSlotBoxes();

            _active = true;
            _closing = false;
            _transition = 0f;
            _canvasGo.SetActive(true);
            RefreshSlotBoxes();
        }

        // 카메라를 무기 쪽으로 최대한 붙이되, 무기가 화면 밖으로 잘리지 않는 최소 거리를 구한다.
        // 화면 비율에 따라 필요한 거리가 달라지므로(세로로 긴 창일수록 멀어져야 함) 런타임에 계산한다.
        private void ComputeCameraFraming()
        {
            _cameraTargetRot = cameraAnchor.rotation;

            if (!TryGetPreviewBounds(out var bounds))
            {
                _cameraTargetPos = cameraAnchor.position;
                _framingCenter = cameraAnchor.position;
                _framingDistance = 0f;
                _framingMinDistance = 0f;
                return;
            }

            Vector3 right = _cameraTargetRot * Vector3.right;
            Vector3 up = _cameraTargetRot * Vector3.up;
            Vector3 forward = _cameraTargetRot * Vector3.forward;

            // 회전된 카메라 축 기준으로 무기 바운즈의 반지름을 구한다.
            Vector3 ext = bounds.extents;
            float halfWidth = Mathf.Abs(right.x) * ext.x + Mathf.Abs(right.y) * ext.y + Mathf.Abs(right.z) * ext.z;
            float halfHeight = Mathf.Abs(up.x) * ext.x + Mathf.Abs(up.y) * ext.y + Mathf.Abs(up.z) * ext.z;
            float halfDepth = Mathf.Abs(forward.x) * ext.x + Mathf.Abs(forward.y) * ext.y + Mathf.Abs(forward.z) * ext.z;

            float tanHalfFov = Mathf.Tan(moddingFieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = _moddingCamera.aspect;

            float distForHeight = (halfHeight + framingMargin) / tanHalfFov;
            float distForWidth = (halfWidth + framingMargin) / (tanHalfFov * Mathf.Max(aspect, 0.01f));
            float distance = Mathf.Max(distForHeight, distForWidth) + halfDepth;

            // 무기가 근거리 클리핑 안으로 들어오면 화면에서 통째로 사라진다.
            // 무기 앞면까지가 항상 near clip 바깥에 있도록 최소 거리를 보장한다.
            float minDistance = _moddingCamera.nearClipPlane + halfDepth + 0.02f;
            distance = Mathf.Max(distance, minDistance);

            // 안전장치: 총은 아무리 길어야 1m 남짓이라 이보다 멀어질 이유가 없다.
            // 예상 못 한 렌더러가 바운즈에 끼어들어도 카메라가 하늘로 날아가지 않게 막는다.
            distance = Mathf.Min(distance, maxCameraDistance);

            // 줌은 이 기본 거리에 배율을 곱해서 만든다. 최소 거리는 그대로 지켜야
            // 가까이 당겼을 때 총이 근거리 클리핑에 잘려 사라지지 않는다.
            _framingCenter = bounds.center;
            _framingDistance = distance;
            _framingMinDistance = minDistance;

            _cameraTargetPos = _framingCenter - forward * distance;
        }

        /// <summary>마우스 휠로 카메라를 무기 쪽으로 당기거나 민다.</summary>
        private void UpdateZoom()
        {
            if (Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    // 휠 값 크기는 플랫폼마다 달라서 방향만 쓴다. 올리면 가까이(배율 감소).
                    _zoomTarget = Mathf.Clamp(_zoomTarget - Mathf.Sign(wheel) * zoomStep, minZoom, maxZoom);
                }
            }

            _zoom = Mathf.Lerp(_zoom, _zoomTarget, 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime));

            if (_framingDistance <= 0f) return;

            float distance = Mathf.Max(_framingDistance * _zoom, _framingMinDistance);
            _cameraTargetPos = _framingCenter - _cameraTargetRot * Vector3.forward * distance;
        }

        // 작업대를 위에서 내려다보면 옆에 서 있는 캐릭터와 손에 든 총이 프레임에 걸치므로 모딩 중에는 전부 숨긴다.
        private void HidePlayerRenderers(GameObject player)
        {
            _hiddenRenderers.Clear();

            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;
                renderer.enabled = false;
                _hiddenRenderers.Add(renderer);
            }
        }

        private void RestorePlayerRenderers()
        {
            foreach (var renderer in _hiddenRenderers)
            {
                if (renderer != null) renderer.enabled = true;
            }

            _hiddenRenderers.Clear();
        }

        private bool TryGetPreviewBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;

            // 총 몸통 메시만 센다.
            //
            // 제외 대상을 나열하는 대신 셀 것만 고르는 이유: 레이저를 장착한 채로 작업대를 열면
            // 레이저 빔(LineRenderer)이 최대 100m까지 뻗은 채로 바운즈에 끼어든다.
            // 그러면 "무기 크기"가 100m로 잡혀 카메라가 그만큼 멀어지려고 하늘로 날아가 버린다.
            // (실제로 부착물을 끼우고 나갔다가 다시 들어가면 카메라가 저 위로 올라가는 버그였다)
            foreach (var renderer in _previewInstance.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;

                // 꺼져 있는 건 화면에 안 보이니 크기 계산에서도 빼야 한다.
                // 레이저를 꺼도 점 메시(AttachmentLaserDot)는 비활성 상태로 남아 있는데,
                // 그 바운즈가 1m라서 그냥 두면 총이 실제보다 크게 잡힌다.
                if (!renderer.gameObject.activeInHierarchy || !renderer.enabled) continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private void BeginClose()
        {
            // 카메라가 1인칭 자리로 되돌아가는 연출이 끝난 뒤에 실제 정리를 한다.
            // 캐릭터만 미리 되살려서, 되돌아가는 동안 1인칭 화면이 자연스럽게 채워지도록 한다.
            RestorePlayerRenderers();
            _closing = true;
        }

        private void FinishClose()
        {
            if (_previewInstance != null) Destroy(_previewInstance);
            _previewInstance = null;
            _previewAttachments = null;
            _realAttachments = null;

            ClearSlotBoxes();
            RestorePlayerRenderers();

            if (_locomotion != null) _locomotion.InputEnabled = true;
            if (_interactor != null) _interactor.InputEnabled = true;
            if (_playerInput != null) _playerInput.enabled = true;

            _moddingCamera.enabled = false;
            if (_playerCamera != null) _playerCamera.enabled = true;
            _playerCamera = null;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _active = false;
            _closing = false;
            _transition = 0f;
            _canvasGo.SetActive(false);
        }

        private GameObject FindMatchingPrefab(string weaponMeshName)
        {
            foreach (var prefab in weaponPreviewPrefabs)
            {
                if (prefab == null) continue;
                var weapon = prefab.GetComponentInChildren<TacticalShooterWeapon>(true);
                if (weapon != null && weapon.gameObject.name == weaponMeshName) return prefab;
            }

            return null;
        }

        private void Update()
        {
            if (!_active) return;

            // 줌은 카메라 보간보다 먼저 계산해야 같은 프레임에 반영된다.
            // 닫는 중에는 1인칭으로 돌아가는 연출이라 줌을 건드리지 않는다.
            if (!_closing) UpdateZoom();

            UpdateCameraTransition();

            if (_closing) return;

            // 이번 프레임의 T는 PlayerInteractor가 이미 "열기"로 써버린 입력이다. 여기서 또 쓰면 안 된다.
            bool inputAlreadyConsumed = Time.frameCount == _inputConsumedFrame;

            if (!inputAlreadyConsumed && Keyboard.current != null &&
                (Keyboard.current.tKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
            {
                BeginClose();
                return;
            }

            HandleSlotSelectionInput();
            HandleAttachmentCycleInput();
            HandleFlipInput();
            UpdatePreviewFlip();
            UpdateSlotBoxPositions();
        }

        private void HandleFlipInput()
        {
            if (Keyboard.current == null) return;
            if (!Keyboard.current[flipKey].wasPressedThisFrame) return;

            // 보고 있던 면과 반대쪽으로 목표를 바꾼다. 도는 도중에 눌러도 그 자리에서 되돌아간다.
            _flipTargetAngle = Mathf.Approximately(_flipTargetAngle, 0f) ? 180f : 0f;
        }

        /// <summary>총을 세운 채로 가로(세로축 기준)로 부드럽게 돌린다.</summary>
        private void UpdatePreviewFlip()
        {
            if (_previewInstance == null) return;
            if (Mathf.Approximately(_flipAngle, _flipTargetAngle)) return;

            float step = flipDuration > 0f ? 180f * Time.deltaTime / flipDuration : 180f;
            _flipAngle = Mathf.MoveTowards(_flipAngle, _flipTargetAngle, step);

            // 세로축(월드 up) 기준 회전. 바운즈 중심을 축으로 삼아 총이 제자리에서 돌게 한다.
            var spin = Quaternion.AngleAxis(_flipAngle, Vector3.up);
            _previewInstance.transform.SetPositionAndRotation(
                _previewPivot + spin * (_previewBasePosition - _previewPivot),
                spin * _previewBaseRotation);
        }

        private void UpdateCameraTransition()
        {
            float step = cameraTransitionDuration > 0f ? Time.deltaTime / cameraTransitionDuration : 1f;
            _transition = Mathf.Clamp01(_transition + (_closing ? -step : step));

            // 시작/끝을 부드럽게 하는 스무스스텝. 플레이어 카메라는 계속 살아있는 자리를 참조해서
            // 닫을 때 정확히 1인칭 시점 위로 되돌아오게 한다.
            float t = _transition * _transition * (3f - 2f * _transition);

            Vector3 fromPos = _playerCamera != null ? _playerCamera.transform.position : _cameraTargetPos;
            Quaternion fromRot = _playerCamera != null ? _playerCamera.transform.rotation : _cameraTargetRot;
            float fromFov = _playerCamera != null ? _playerCamera.fieldOfView : moddingFieldOfView;

            _moddingCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(fromPos, _cameraTargetPos, t),
                Quaternion.Slerp(fromRot, _cameraTargetRot, t));
            _moddingCamera.fieldOfView = Mathf.Lerp(fromFov, moddingFieldOfView, t);

            // 전환 중에는 UI를 숨겨서 화면이 어수선해지지 않게 한다.
            bool showUi = !_closing && _transition > 0.85f;
            if (_canvasGo.activeSelf != showUi) _canvasGo.SetActive(showUi);

            if (_closing && _transition <= 0f) FinishClose();
        }

        private void HandleSlotSelectionInput()
        {
            if (Keyboard.current == null) return;

            for (int i = 0; i < _availableSlots.Count && i < 5; i++)
            {
                KeyControl key = i == 0 ? Keyboard.current.digit1Key
                    : i == 1 ? Keyboard.current.digit2Key
                    : i == 2 ? Keyboard.current.digit3Key
                    : i == 3 ? Keyboard.current.digit4Key
                    : Keyboard.current.digit5Key;

                if (!key.wasPressedThisFrame) continue;

                _selectedSlotIndex = i;
                RefreshSlotBoxes();
            }
        }

        private void HandleAttachmentCycleInput()
        {
            if (Keyboard.current == null || _availableSlots.Count == 0) return;

            int direction = 0;
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
                direction = 1;
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
                direction = -1;

            if (direction == 0) return;

            CycleAttachment(_availableSlots[_selectedSlotIndex], direction);
            RefreshSlotBoxes();
        }

        private void CycleAttachment(AttachmentSlotType slot, int direction)
        {
            _bySlot.TryGetValue(slot, out var options);
            options ??= new List<AttachmentDefinition>();

            var current = _previewAttachments.GetEquipped(slot);

            // 인덱스 0은 "아무것도 안 낀 상태"를 나타내는 가상 항목.
            int currentIndex = current == null ? 0 : options.IndexOf(current) + 1;
            int count = options.Count + 1;
            int nextIndex = ((currentIndex + direction) % count + count) % count;

            if (nextIndex == 0)
            {
                _previewAttachments.RemoveAttachment(slot);
                _realAttachments.RemoveAttachment(slot);
            }
            else
            {
                var def = options[nextIndex - 1];
                _previewAttachments.EquipAttachment(def, slot);
                _realAttachments.EquipAttachment(def, slot);

                // 새로 낀 장치가 켜진 채로 나오면 프리뷰에서 빔이 작업실을 가로지른다.
                _previewAttachments.SetLaser(false);
                _previewAttachments.SetFlashlight(false);
            }
        }

        private void BuildUI()
        {
            _canvasGo = new GameObject("ModdingCanvas");
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvasGo.AddComponent<CanvasScaler>();
            _canvasRect = _canvasGo.GetComponent<RectTransform>();

            var hint = CreateText(_canvasGo.transform, 16, TextAnchor.LowerCenter);
            hint.text = "[1-5] Select Slot    [A/D] Change    [Y] Flip    [Wheel] Zoom    [T] Close";
            hint.color = new Color(1f, 1f, 1f, 0.7f);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 30f);
            hintRect.sizeDelta = new Vector2(700f, 30f);

            _canvasGo.SetActive(false);
        }

        private void BuildSlotBoxes()
        {
            ClearSlotBoxes();

            var sockets = new Dictionary<AttachmentSlotType, Transform>();
            foreach (var socket in _previewInstance.GetComponentsInChildren<AttachmentSocket>(true))
            {
                sockets[socket.slotType] = socket.transform;
            }

            foreach (var slot in _availableSlots)
            {
                if (!sockets.TryGetValue(slot, out var socket)) continue;

                var boxGo = new GameObject($"SlotBox_{slot}");
                boxGo.transform.SetParent(_canvasGo.transform, false);

                var bg = boxGo.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.6f);

                var rect = bg.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(155f, 44f);

                var label = CreateText(boxGo.transform, 13, TextAnchor.UpperCenter);
                label.text = slot.ToString().ToUpperInvariant();
                label.color = new Color(1f, 1f, 1f, 0.55f);
                StretchToParent(label.rectTransform, new Vector2(0f, 6f));

                var value = CreateText(boxGo.transform, 17, TextAnchor.LowerCenter);
                StretchToParent(value.rectTransform, new Vector2(0f, -8f));

                _slotBoxes.Add(new SlotBox
                {
                    Slot = slot,
                    Socket = socket,
                    Rect = rect,
                    Background = bg,
                    ValueText = value,
                    // 부착 위치 바로 옆에 붙인다. 머즐은 총구 앞(오른쪽), 조준경은 총 위,
                    // 언더배럴/사이드레일은 소켓 위치가 거의 겹치므로 총열 아래에 좌우로 나란히 놓는다.
                    ScreenOffset = slot switch
                    {
                        AttachmentSlotType.Optic => new Vector2(0f, 58f),
                        AttachmentSlotType.Muzzle => new Vector2(95f, 0f),
                        AttachmentSlotType.UnderBarrel => new Vector2(-85f, -62f),
                        // 측면 레일 좌우 두 칸은 화면에서 거의 같은 자리에 겹쳐 보이므로
                        // (옆에서 보면 앞뒤로 겹친다) 박스를 위아래로 벌려 구분한다.
                        AttachmentSlotType.SideRailRight => new Vector2(85f, -62f),
                        _ => new Vector2(85f, -110f)
                    }
                });
            }
        }

        private void ClearSlotBoxes()
        {
            foreach (var box in _slotBoxes)
            {
                if (box.Rect != null) Destroy(box.Rect.gameObject);
            }

            _slotBoxes.Clear();
        }

        private void UpdateSlotBoxPositions()
        {
            if (_canvasRect == null) return;

            // 총을 반대로 돌리면 총구도 화면 반대쪽을 향한다. 박스 좌우 오프셋도 같이 뒤집어야
            // "총구 앞 박스"가 계속 총구 쪽에 붙어 있다.
            float mirror = _flipAngle > 90f ? -1f : 1f;

            foreach (var box in _slotBoxes)
            {
                if (box.Socket == null || box.Rect == null) continue;

                Vector3 screenPoint = _moddingCamera.WorldToScreenPoint(box.Socket.position);
                // 스크린 좌표를 캔버스 중앙 기준 좌표로 바꾼다 (ScreenSpaceOverlay라 카메라 인자는 필요 없음).
                Vector2 canvasPoint = new Vector2(screenPoint.x - Screen.width * 0.5f,
                    screenPoint.y - Screen.height * 0.5f);

                box.Rect.anchoredPosition = canvasPoint
                                            + new Vector2(box.ScreenOffset.x * mirror, box.ScreenOffset.y);
            }
        }

        private void RefreshSlotBoxes()
        {
            for (int i = 0; i < _slotBoxes.Count; i++)
            {
                var box = _slotBoxes[i];
                var equipped = _previewAttachments.GetEquipped(box.Slot);
                box.ValueText.text = equipped != null ? equipped.displayName : "(empty)";

                bool selected = i == _selectedSlotIndex;
                box.Background.color = selected
                    ? new Color(0.85f, 0.55f, 0.1f, 0.75f)
                    : new Color(0f, 0f, 0f, 0.6f);
                box.ValueText.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.85f);
            }

            UpdateSlotBoxPositions();
        }

        private static void StretchToParent(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 6f);
            rect.offsetMax = new Vector2(-6f, -6f);
            rect.anchoredPosition += offset;
        }

        private static Text CreateText(Transform parent, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
