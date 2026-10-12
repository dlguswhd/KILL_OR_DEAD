using UnityEngine;

// 감지용 트리거의 충돌 이벤트를 InteractionDetector로 전달한다.
public class InteractionDetectorTrigger : MonoBehaviour
{
    public InteractionDetector detector;

    private void OnTriggerEnter(Collider other) => detector?.OnPointEnter(other);

    private void OnTriggerExit(Collider other) => detector?.OnPointExit(other);
}
