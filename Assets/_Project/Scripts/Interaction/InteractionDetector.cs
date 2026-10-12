using KillOrDead.Player;
using UnityEngine;

[AddComponentMenu("KILL OR DEAD/Interaction/Interaction Detector")]
public class InteractionDetector : MonoBehaviour
{
    [Header("감지")]
    [Tooltip("상호작용 반경(m). 플레이어 몸통 중심 기준. 한 곳에서 조절한다.")]
    [SerializeField]
    private float interactRange = 1f;

    [Tooltip("카메라 중앙 전방(V1)과 플레이어 시점→포인트(V2)가 이루는 허용 각도(도).")]
    [SerializeField]
    private float maxAngle = 30f;

    [Tooltip("상호작용 포인트가 있는 레이어 이름. 물리 쿼리 마스크로 쓴다.")]
    [SerializeField]
    private string interactableLayerName = "Interactable";

    [Header("디버그")]
    [Tooltip("대상이 바뀔 때 콘솔에 로그를 찍는다.")]
    [SerializeField]
    private bool logSelection;

    // 지금 표시 중인 대상.
    private InteractionPointController _current;

    private Camera _camera;
    private CharacterController _characterController;
    private InteractionUIController _ui;
    private PlayerTapHoldInput _tapHold;

    // OverlapSphere 결과를 담는 재사용 버퍼(alloc 0).
    private readonly Collider[] _hits = new Collider[64];
    private int _pointMask;

    private void Awake()
    {
        _camera = GetComponentInChildren<Camera>(true);
        _characterController = GetComponent<CharacterController>();
        _tapHold = GetComponent<PlayerTapHoldInput>();

        int layer = LayerMask.NameToLayer(interactableLayerName);
        _pointMask = layer >= 0 ? 1 << layer : ~0;
    }

    private void OnEnable()
    {
        if (_tapHold == null) return;
        _tapHold.Press += OnPress;
        _tapHold.Tap += OnTap;
        _tapHold.Hold += OnHold;
        _tapHold.Cancelled += OnCancelled;
    }

    private void OnDisable()
    {
        if (_tapHold == null) return;
        _tapHold.Press -= OnPress;
        _tapHold.Tap -= OnTap;
        _tapHold.Hold -= OnHold;
        _tapHold.Cancelled -= OnCancelled;
    }

    private void Update()
    {
        Evaluate();
        UpdateHoldProgress();
    }

    // 홀드 대상이 있을 때 홀드 구간 진행도((경과 - 경계) / 요구 시간)를 UI로 전달한다.
    // 경계 전에는 0, 요구 시간 도달 시 1.
    private void UpdateHoldProgress()
    {
        if (_ui == null || _tapHold == null) return;
        if (!_ui.HasTarget || !_ui.Target.HasHold) return;

        float progress = 0f;
        if (_tapHold.IsKeyHeld)
        {
            float holdTime = _tapHold.KeyHeldTime - _tapHold.TapHoldBoundaryTime;
            if (holdTime > 0f)
                progress = holdTime / _tapHold.HoldRequiredTime;
        }

        _ui.SetHoldProgress(progress);
    }

    // 플레이어 몸통 중심 반경 안의 포인트를 물리 쿼리로 모아온다. 통과한 후보만 각도(카메라)를 계산한다.
    private void Evaluate()
    {
        if (_camera == null)
        {
            SetTarget(null);
            return;
        }

        // 반경 기준: 플레이어 몸통 중심.
        Vector3 bodyCenter = _characterController != null
            ? _characterController.transform.TransformPoint(_characterController.center)
            : transform.position;

        // 각도 기준: 카메라(눈).
        Vector3 eyePos = _camera.transform.position;
        Vector3 forward = _camera.transform.forward;

        float rangeSqr = interactRange * interactRange;
        float cosLimit = Mathf.Cos(maxAngle * Mathf.Deg2Rad);

        int count = Physics.OverlapSphereNonAlloc(bodyCenter, interactRange, _hits, _pointMask,
            QueryTriggerInteraction.Collide);

        InteractionPointController best = null;
        float bestDot = cosLimit;

        for (int i = 0; i < count; i++)
        {
            var col = _hits[i];
            if (col == null) continue;

            var point = col.GetComponentInParent<InteractionPointController>();
            if (point == null || !point.IsInteractable) continue;

            // 콜라이더 반경이 더해지는 것을 보정해 정확한 거리로 다시 거른다.
            if ((point.Position - bodyCenter).sqrMagnitude > rangeSqr) continue;

            // 카메라 중앙 전방과의 각도가 maxAngle 이내인지(dot 최대 = 각도 최소).
            float dot = Vector3.Dot(forward, (point.Position - eyePos).normalized);
            if (dot < cosLimit) continue;

            if (dot > bestDot)
            {
                bestDot = dot;
                best = point;
            }
        }

        SetTarget(best);
    }

    private void SetTarget(InteractionPointController point)
    {
        if (_current == point) return;
        _current = point;

        if (logSelection)
            Debug.Log($"InteractionDetector: 대상 = {(point != null ? point.name : "없음")}", this);

        if (_ui == null)
        {
            // UI는 표시를 끌 때 자기 루트를 꺼두므로 비활성 포함으로 찾는다.
            _ui = FindFirstObjectByType<InteractionUIController>(FindObjectsInactive.Include);
        }

        if (_ui == null) return;

        if (point == null) _ui.ClearTarget();
        else _ui.SetTarget(point);
    }

    // 탭/홀드 판정 결과를 UI로 전달한다. 실제 동작 실행은 UI가 담당한다.
    private void OnPress() => _ui?.OnPress();
    private void OnTap() => _ui?.OnTapPerformed();
    private void OnHold() => _ui?.OnHoldCompleted();
    private void OnCancelled() => _ui?.OnCancelled();
}
