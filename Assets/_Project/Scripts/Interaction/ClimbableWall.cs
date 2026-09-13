using UnityEngine;

namespace KillOrDead.Interaction
{
    /// <summary>
    /// "이 벽은 탈 수 있다"고 표시하는 마커 컴포넌트.
    ///
    /// 벽타기(<see cref="KillOrDead.Player.PlayerWallClimb"/>)는 아무 벽에나 붙지 않고
    /// <b>이 컴포넌트가 붙은 오브젝트</b>에만 붙는다. 맵에 벽이 수백 개 생겨도
    /// 기획자가 올라갈 수 있는 벽만 골라서 지정할 수 있게 하려는 것이다.
    ///
    /// 붙이는 위치는 <b>콜라이더가 있는 오브젝트나 그 부모</b> 아무 데나 된다.
    /// 레이캐스트가 맞은 콜라이더에서 부모를 타고 올라가며 찾기 때문이다.
    /// (예: 벽 모델이 자식 메시 여러 개로 쪼개져 있어도 부모에 하나만 붙이면 된다.)
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Interaction/Climbable Wall")]
    public class ClimbableWall : MonoBehaviour
    {
        [Tooltip("끄면 이 벽은 잠시 못 타게 된다. 연출이나 임무 진행에 따라 막고 싶을 때 코드에서 바꾼다.")]
        [SerializeField] private bool climbable = true;

        [Tooltip("이 벽만 다른 높이 제한을 쓰고 싶을 때 넣는다(m).\n" +
                 "0이면 플레이어 쪽 Player Wall Climb의 기본 제한을 그대로 쓴다.")]
        [SerializeField, Min(0f)] private float maxHeightOverride = 0f;

        /// <summary>지금 탈 수 있는 상태인지.</summary>
        public bool Climbable
        {
            get => climbable && isActiveAndEnabled;
            set => climbable = value;
        }

        /// <summary>이 벽 전용 높이 제한(m). 0이면 플레이어 기본값을 쓴다는 뜻.</summary>
        public float MaxHeightOverride => maxHeightOverride;
    }
}
