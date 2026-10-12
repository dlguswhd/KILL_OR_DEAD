using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(SphereCollider))]
public class InteractionPointController : MonoBehaviour
{
    [Header("동작 슬롯 (탭 / 홀드)")]
    [Tooltip("탭 입력으로 실행할 동작. 하나만 배정할 수 있다. 비워둘 수 있다.")]
    [SerializeField]
    private UnityEvent tapAction = new UnityEvent();

    [Tooltip("홀드 입력으로 실행할 동작. 하나만 배정할 수 있다. 비워둘 수 있다.")]
    [SerializeField]
    private UnityEvent holdAction = new UnityEvent();

    [Header("표시 문구")]
    [Tooltip("탭 동작을 안내하는 문구. 상호작용 UI에 표시된다.")]
    [SerializeField]
    private string tapMessage = "상호작용";

    [Tooltip("홀드 동작을 안내하는 문구. 상호작용 UI에 표시된다.")]
    [SerializeField]
    private string holdMessage = "상호작용";

    [Header("콜라이더 관련 설정들")]
    [Tooltip("플레이어가 반경 내에 있는지 확인하기 위한 콜라이더. 동일한 게임 오브젝트 내에 있는 구형 콜라이더를 넣어주거나, 비워두세요.")]
    [SerializeField]
    private SphereCollider col;

    [Tooltip("콜라이더 반경")]
    [SerializeField]
    private float colliderRadius;

    // 탭 동작 슬롯. UI가 입력을 받아 이 이벤트를 실행한다.
    public UnityEvent TapAction => tapAction;

    // 홀드 동작 슬롯. UI가 입력을 받아 이 이벤트를 실행한다.
    public UnityEvent HoldAction => holdAction;

    // 인스펙터에서 탭 동작이 실제로 배정됐는지.
    public bool HasTap => tapAction != null && tapAction.GetPersistentEventCount() > 0;

    // 인스펙터에서 홀드 동작이 실제로 배정됐는지.
    public bool HasHold => holdAction != null && holdAction.GetPersistentEventCount() > 0;

    // 탭·홀드 중 하나라도 배정됐는지. 둘 다 비면 상호작용 대상이 아니다.
    public bool IsInteractable => HasTap || HasHold;

    // 탭 동작 안내 문구.
    public string TapMessage => tapMessage;

    // 홀드 동작 안내 문구.
    public string HoldMessage => holdMessage;

    // 탭 동작을 실행한다.
    public void InvokeTap() => tapAction?.Invoke();

    // 홀드 동작을 실행한다.
    public void InvokeHold() => holdAction?.Invoke();

    private void Start()
    {
        ComponentSet();
        CheckComponentSet();
    }

    private void Update()
    {
        
    }
    
    private void ComponentSet()
    {
        if(col == null) col = GetComponent<SphereCollider>();
        col.radius = colliderRadius;
        if(!col.isTrigger) col.isTrigger = true;
    }
    
    private void CheckComponentSet()
    {
        if (col == null)
        {
            Debug.LogError($"플레이어 인식용 콜라이더 비어있음.");
            return;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (tapAction != null && tapAction.GetPersistentEventCount() > 1)
            Debug.LogWarning($"{name}: 탭 동작은 하나만 배정할 수 있다.", this);

        if (holdAction != null && holdAction.GetPersistentEventCount() > 1)
            Debug.LogWarning($"{name}: 홀드 동작은 하나만 배정할 수 있다.", this);
    }
#endif
}
