using UnityEngine;

// 상호작용 동작 연결을 확인하기 위한 임시 테스트용 컴포넌트. 테스트가 끝나면 제거한다.
public class InteractionPointTestAction : MonoBehaviour
{
    public void Tap()
    {
        Debug.Log($"{name}: 탭 동작 실행", this);
    }

    public void Hold()
    {
        Debug.Log($"{name}: 홀드 동작 실행", this);
    }
}
