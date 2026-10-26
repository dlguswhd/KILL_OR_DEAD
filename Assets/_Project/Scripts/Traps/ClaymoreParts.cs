using UnityEngine;

/// <summary>
/// 레이저 감지형 크레모어(Claymore_Modular) 부품 교체.
/// - 해체(Disarm): 선만 Cable_Intact -> Cable_Cut 으로 바뀐다.
/// - 센서 피격(DestroySensor): 센서만 Sensor_Intact -> Sensor_Destroyed 로 바뀐다.
/// Claymore_Modular 루트 오브젝트에 붙이면 자식 이름으로 자동 연결된다.
/// </summary>
public class ClaymoreParts : MonoBehaviour
{
    [Header("비워두면 자식 이름으로 자동 연결")]
    [SerializeField] private GameObject cableIntact;
    [SerializeField] private GameObject cableCut;
    [SerializeField] private GameObject sensorIntact;
    [SerializeField] private GameObject sensorDestroyed;

    public bool IsDisarmed { get; private set; }
    public bool IsSensorDestroyed { get; private set; }

    private void Awake()
    {
        Apply();
    }

    /// <summary>해체: 선만 끊어진 버전으로 교체</summary>
    [ContextMenu("해체 테스트")]
    public void Disarm()
    {
        IsDisarmed = true;
        Apply();
    }

    /// <summary>센서에 총을 맞았을 때: 센서만 부서진 버전으로 교체</summary>
    [ContextMenu("센서 파괴 테스트")]
    public void DestroySensor()
    {
        IsSensorDestroyed = true;
        Apply();
    }

    /// <summary>
    /// 이 콜라이더가 센서(카메라) 피격 판정용인지. 히트스캔이 본체에 맞았을 때와 센서에 맞았을 때를 구분하려고 쓴다.
    /// (GetComponentInParent만 쓰면 본체에 맞아도 이 스크립트가 찾아져서 센서가 부서져 버린다)
    /// </summary>
    public bool IsSensorCollider(Collider col)
    {
        if (sensorIntact == null) sensorIntact = FindChild("Sensor_Intact");
        return col != null && sensorIntact != null && col.gameObject == sensorIntact;
    }

    [ContextMenu("원래대로")]
    public void ResetState()
    {
        IsDisarmed = false;
        IsSensorDestroyed = false;
        Apply();
    }

    private void Apply()
    {
        if (cableIntact == null) cableIntact = FindChild("Cable_Intact");
        if (cableCut == null) cableCut = FindChild("Cable_Cut");
        if (sensorIntact == null) sensorIntact = FindChild("Sensor_Intact");
        if (sensorDestroyed == null) sensorDestroyed = FindChild("Sensor_Destroyed");

        SetActive(cableIntact, !IsDisarmed);
        SetActive(cableCut, IsDisarmed);
        SetActive(sensorIntact, !IsSensorDestroyed);
        SetActive(sensorDestroyed, IsSensorDestroyed);
    }

    private GameObject FindChild(string childName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t.gameObject;
        }
        Debug.LogWarning($"[ClaymoreParts] 자식 '{childName}' 을(를) 찾지 못했습니다.", this);
        return null;
    }

    private static void SetActive(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }
}
