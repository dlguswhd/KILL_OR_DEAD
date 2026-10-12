using System.Collections.Generic;
using KillOrDead.Player;
using UnityEngine;

[AddComponentMenu("KILL OR DEAD/Interaction/Interaction Detector")]
public class InteractionDetector : MonoBehaviour
{
    [Header("감지")]
    [Tooltip("상호작용 반경(m). 플레이어 중심 공통 값이라 이 한 곳에서 조절한다.")]
    [SerializeField]
    private float interactRange = 1f;

    [Tooltip("카메라 중앙 전방(V1)과 플레이어→포인트(V2)가 이루는 허용 각도(도).")]
    [SerializeField]
    private float maxAngle = 30f;

    [Header("레이어")]
    [Tooltip("감지용 트리거를 둘 레이어 이름.")]
    [SerializeField]
    private string detectorLayerName = "InteractionDetector";

    [Tooltip("상호작용 포인트가 있는 레이어 이름.")]
    [SerializeField]
    private string interactableLayerName = "Interactable";

    // 반경 안에 들어온 포인트 후보들.
    private readonly HashSet<InteractionPointController> _candidates = new();

    // 지금 표시 중인 대상.
    private InteractionPointController _current;

    private Camera _camera;
    private InteractionUIController _ui;
    private PlayerTapHoldInput _tapHold;
    private int _interactableLayer = -1;

    private void Awake()
    {
        _camera = GetComponentInChildren<Camera>(true);
        _tapHold = GetComponent<PlayerTapHoldInput>();
        _interactableLayer = LayerMask.NameToLayer(interactableLayerName);
        BuildTrigger();
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

    // 플레이어 자식으로 감지용 트리거를 만든다. 포인트는 여기에 겹쳐서 후보가 된다.
    private void BuildTrigger()
    {
        var go = new GameObject("InteractionDetectorTrigger");
        go.transform.SetParent(transform, false);

        int layer = LayerMask.NameToLayer(detectorLayerName);
        if (layer >= 0) go.layer = layer;

        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = interactRange;

        // 트리거 이벤트가 발생하려면 한쪽에 리지드바디가 필요하다. 포인트는 리지드바디를 두지 않으므로 여기에 둔다.
        // kinematic이라 밀리지 않고, 무게/중력의 영향을 받지 않는다.
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var forwarder = go.AddComponent<InteractionDetectorTrigger>();
        forwarder.detector = this;
    }

    internal void OnPointEnter(Collider other)
    {
        var point = other.GetComponentInParent<InteractionPointController>();
        if (point == null) return;

        _candidates.Add(point);
        Evaluate();
    }

    internal void OnPointExit(Collider other)
    {
        var point = other.GetComponentInParent<InteractionPointController>();
        if (point == null) return;

        _candidates.Remove(point);
        Evaluate();
    }

    private void Update()
    {
        // 각도는 카메라를 돌리면 입력과 무관하게 계속 바뀌므로 매 프레임 다시 고른다.
        if (_candidates.Count > 0) Evaluate();
    }

    // 조건(근접 + 각도 + 동작 1개 이상)을 만족하는 후보 중 각도가 가장 작은 것을 고른다.
    private void Evaluate()
    {
        if (_camera == null)
        {
            SetTarget(null);
            return;
        }

        Vector3 origin = transform.position;
        Vector3 forward = _camera.transform.forward;
        float cosLimit = Mathf.Cos(maxAngle * Mathf.Deg2Rad);

        InteractionPointController best = null;
        float bestDot = cosLimit;

        foreach (var point in _candidates)
        {
            if (point == null || !point.IsInteractable) continue;
            if (_interactableLayer >= 0 && point.gameObject.layer != _interactableLayer) continue;

            Vector3 toPoint = point.transform.position - origin;
            if (toPoint.sqrMagnitude > interactRange * interactRange) continue;

            float dot = Vector3.Dot(forward, toPoint.normalized);
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
