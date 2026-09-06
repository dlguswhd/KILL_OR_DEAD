using KINEMATION.TacticalShooterPack.Scripts.Animation;
using UnityEngine;

namespace KillOrDead.Player
{
    /// <summary>
    /// 기울이기(Q/E)를 할 때 화면도 같이 기울인다.
    ///
    /// 벤더 팩은 몸만 옆으로 내밀고 <b>화면은 수평 그대로</b>였다. 여기서 카메라를 좌우로 굴려서
    /// 실제로 고개를 기울인 느낌을 낸다.
    ///
    /// 기울이는 양은 <see cref="TacticalProceduralAnimation.leanInput"/>을 따라간다.
    /// 그 값은 플레이어가 매 프레임 부드럽게 보간해 주는 값이라, 화면도 자연스럽게 같이 따라온다.
    /// </summary>
    // FPSCameraAnimator가 LateUpdate에서 카메라 회전을 통째로 덮어쓴다.
    // 그 뒤에 굴려야 하므로 실행 순서를 뒤로 미룬다(벤더는 순서 지정이 없어서 0이다).
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("KILL OR DEAD/Player/Lean Camera Tilt")]
    public class LeanCameraTilt : MonoBehaviour
    {
        [Tooltip("몸이 1도 기울 때 화면이 몇 도 기울어지는지. 0이면 화면은 안 기울어진다.\n" +
                 "기본 기울이기 각도가 30도이므로 0.3이면 화면은 9도 기운다.")]
        [SerializeField] private float rollPerLeanDegree = 0.3f;

        [Tooltip("화면이 기울어지는 최대 각도(도). 너무 크면 멀미가 난다.")]
        [SerializeField, Min(0f)] private float maxRoll = 12f;

        [Tooltip("체크하면 기울어지는 방향이 반대가 된다.")]
        [SerializeField] private bool invert;

        private TacticalProceduralAnimation _proceduralAnimation;
        private Transform _camera;

        private void Awake()
        {
            _proceduralAnimation = GetComponent<TacticalProceduralAnimation>();
            if (_proceduralAnimation == null) _proceduralAnimation = GetComponentInChildren<TacticalProceduralAnimation>(true);

            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) _camera = cam.transform;
        }

        private void LateUpdate()
        {
            if (_camera == null || _proceduralAnimation == null) return;
            if (Mathf.Approximately(rollPerLeanDegree, 0f)) return;

            float roll = Mathf.Clamp(_proceduralAnimation.leanInput * rollPerLeanDegree, -maxRoll, maxRoll);
            if (invert) roll = -roll;

            _camera.rotation *= Quaternion.Euler(0f, 0f, roll);
        }
    }
}
