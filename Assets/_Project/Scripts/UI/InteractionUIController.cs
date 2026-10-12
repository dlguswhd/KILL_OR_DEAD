using TMPro;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("KILL OR DEAD/Interaction/Interaction UI Controller")]
public class InteractionUIController : MonoBehaviour
{
    [Header("탭 UI")]
    [Tooltip("탭 동작이 배정됐을 때만 켤 UI 묶음.")]
    [SerializeField]
    private GameObject tapElement;

    [Tooltip("탭 동작 안내 문구를 표시할 텍스트.")]
    [SerializeField]
    private TMP_Text tapMessageText;

    [Header("홀드 UI")]
    [Tooltip("홀드 동작이 배정됐을 때만 켤 UI 묶음(게이지 포함).")]
    [SerializeField]
    private GameObject holdElement;

    [Tooltip("홀드 동작 안내 문구를 표시할 텍스트.")]
    [SerializeField]
    private TMP_Text holdMessageText;

    [Tooltip("홀드 진행 게이지(0~1). 게이지 링의 Fill Image를 연결한다.")]
    [SerializeField]
    private Image holdProgressFill;

    [Header("키")]
    [Tooltip("상호작용 키를 표시할 텍스트. 비워둘 수 있다.")]
    [SerializeField]
    private TMP_Text keyText;

    // 지금 표시 중인 상호작용 대상. 없으면 null.
    private InteractionPointController _target;

    // 이 UI의 RectTransform. 화면 좌표를 붙일 때 이 루트의 정중앙(로컬 0,0,0)을 쓴다.
    private RectTransform _rect;

    // 이 UI가 붙어 있는 캔버스. 화면 중앙 기준 좌표를 캔버스 스케일로 환산할 때 쓴다.
    private Canvas _canvas;

    // 이번 누름에서 이미 탭 동작을 실행했는지. 누름 즉시 실행(탭만 배정)과 뗌 탭 판정의 중복 실행을 막는다.
    private bool _tapInvokedThisPress;

    public bool HasTarget => _target != null;

    public InteractionPointController Target => _target;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();

        // 대상이 잡히기 전에는 숨겨둔다.
        Hide();
    }

    // 선택 대상이 바뀌면 호출한다. 슬롯 구성에 따라 요소를 켜고 끄고 문구를 갱신한다.
    // 대상이 없거나 동작이 하나도 배정되지 않았으면 UI를 숨긴다.
    public void SetTarget(InteractionPointController point)
    {
        _target = point;
        _tapInvokedThisPress = false;
        SetHoldProgress(0f);

        if (point == null || !point.IsInteractable)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);

        if (tapElement != null) tapElement.SetActive(point.HasTap);
        if (tapMessageText != null) tapMessageText.text = point.TapMessage;

        if (holdElement != null) holdElement.SetActive(point.HasHold);
        if (holdMessageText != null) holdMessageText.text = point.HoldMessage;
    }

    // 표시를 끈다.
    public void ClearTarget()
    {
        _target = null;
        _tapInvokedThisPress = false;
        Hide();
    }

    // 상호작용 키 표시를 갱신한다. 게임패드 아이콘 등으로 바꿀 때 쓴다.
    public void SetKeyLabel(string label)
    {
        if (keyText != null) keyText.text = label;
    }

    // ---- 입력 진입점 ----
    // 탭/홀드 판정(#5, PlayerTapHoldInput)이 결과를 전달하면 UI가 대상의 슬롯을 실행한다.

    // 키를 누른 순간.
    public void OnPress()
    {
        _tapInvokedThisPress = false;
        if (_target == null) return;

        // 탭 동작만 배정: 입력 방식과 무관하게 누른 즉시 탭 동작을 실행한다.
        if (_target.HasTap && !_target.HasHold)
        {
            _tapInvokedThisPress = true;
            _target.InvokeTap();
        }
    }

    // 짧게 눌렀다 뗀 탭 판정.
    public void OnTapPerformed()
    {
        if (_target == null) return;

        // 탭 동작이 있고, 누름에서 아직 실행하지 않았을 때만 실행한다(탭만 배정은 누름에서 이미 실행됨).
        if (_target.HasTap && !_tapInvokedThisPress)
        {
            _tapInvokedThisPress = true;
            _target.InvokeTap();
        }
    }

    // 홀드 요구 시간을 채운 홀드 판정.
    public void OnHoldCompleted()
    {
        if (_target == null || !_target.HasHold) return;

        _target.InvokeHold();
    }

    // 홀드 도중 요구 시간 전에 뗀 취소.
    public void OnCancelled()
    {
        SetHoldProgress(0f);
    }

    // 홀드 진행 게이지를 갱신한다(0~1). 홀드 구간(경계 통과 이후)만 반영한다.
    public void SetHoldProgress(float progress)
    {
        if (holdProgressFill != null)
            holdProgressFill.fillAmount = Mathf.Clamp01(progress);
    }

    // 화면 좌표(px)를 이 UI의 정중앙(로컬 0,0,0)에 붙인다.
    // 이 UI의 앵커가 화면 중앙(0.5,0.5)이므로, 화면 중앙 기준 오프셋을 캔버스 스케일로 나누면
    // 캔버스 피벗·렌더 모드와 무관하게 중앙이 화면 좌표에 정렬된다.
    public void SetScreenPosition(Vector2 screenPoint)
    {
        if (_rect == null) return;

        float scale = _canvas != null ? _canvas.scaleFactor : 1f;
        if (scale <= 0f) scale = 1f;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        _rect.anchoredPosition = (screenPoint - screenCenter) / scale;
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}
