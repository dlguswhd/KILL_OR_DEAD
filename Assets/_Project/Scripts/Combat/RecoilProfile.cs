using UnityEngine;

namespace KillOrDead.Combat
{
    /// <summary>
    /// 무기 한 종류의 반동 수치. 배틀그라운드 방식이라 <b>자동 복귀 값이 없다</b> —
    /// 올라간 조준점은 플레이어가 마우스를 내려서 직접 잡아야 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecoilProfile", menuName = "KILL OR DEAD/Combat/Recoil Profile")]
    public class RecoilProfile : ScriptableObject
    {
        [Header("수직 반동 (총구가 위로 올라가는 양)")]
        [Tooltip("한 발당 올라가는 각도(도). 연사 누적 배수가 여기에 곱해진다.")]
        [Min(0f)] public float verticalPerShot = 0.45f;

        [Tooltip("연사 누적에 따른 수직 반동 배수. X축 0=첫 발, 1=rampShots발째.")]
        public AnimationCurve verticalRamp = AnimationCurve.EaseInOut(0f, 1f, 1f, 1.8f);

        [Header("수평 반동 (좌우로 흔들리는 양)")]
        [Min(0f)] public float horizontalPerShot = 0.18f;

        public AnimationCurve horizontalRamp = AnimationCurve.EaseInOut(0f, 1f, 1f, 1.6f);

        [Tooltip("좌우 흔들림이 한쪽으로 쏠리는 정도. 0이면 완전 무작위, 1이면 한 방향으로만 쏠린다. 총마다 '반동 패턴'이 생기는 값.")]
        [Range(0f, 1f)] public float horizontalDriftBias = 0.55f;

        [Tooltip("몇 발마다 쏠리는 방향이 바뀌는지. 0이면 안 바뀐다.")]
        [Min(0)] public int driftFlipShots = 5;

        [Header("연사 누적")]
        [Tooltip("이만큼 연사하면 누적 배수가 최대치에 도달한다.")]
        [Min(1)] public int rampShots = 12;

        [Tooltip("사격을 멈추고 이 시간(초)이 지나면 누적 카운터가 초기화된다. 시야가 원위치로 돌아가는 게 아니라, 다음 연사가 다시 1발째부터 시작한다는 뜻이다.")]
        [Min(0f)] public float sprayResetTime = 0.35f;

        [Header("자세 / 상태별 배수")]
        [Tooltip("조준(ADS) 중일 때의 배수. 견착하면 반동이 줄어든다.")]
        [Min(0f)] public float aimMultiplier = 0.75f;

        [Tooltip("이동 중일 때의 배수.")]
        [Min(0f)] public float movingMultiplier = 1.35f;

        [Tooltip("공중에 떠 있을 때의 배수.")]
        [Min(0f)] public float airborneMultiplier = 1.9f;
    }
}
