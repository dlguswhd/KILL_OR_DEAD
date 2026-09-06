using UnityEngine;

namespace KillOrDead.Player
{
    /// <summary>
    /// 수류탄이 날아갈 길을 미리 보여주는 빨간 포물선 (배틀그라운드 방식).
    ///
    /// 실제 물리와 같은 식으로 한 걸음씩 계산하고, 걸음 사이를 구체로 훑어서
    /// 벽·바닥에 닿는 지점에서 선을 끊는다. 그래서 선의 끝이 곧 떨어질 자리다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Player/Grenade Trajectory")]
    public class GrenadeTrajectory : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private Color lineColor = new Color(1f, 0.15f, 0.1f);

        [Tooltip("선 굵기(m).")]
        [SerializeField, Min(0.001f)] private float lineWidth = 0.025f;

        [Header("Simulation")]
        [Tooltip("최대 몇 걸음까지 계산할지. 늘리면 더 멀리까지 그린다.")]
        [SerializeField, Min(2)] private int maxSteps = 80;

        [Tooltip("한 걸음의 시간(초). 작을수록 곡선이 매끄럽지만 계산이 늘어난다.")]
        [SerializeField, Min(0.005f)] private float timeStep = 0.045f;

        [Tooltip("수류탄 굵기(m). 벽에 닿는 판정을 이 반지름의 구체로 한다.")]
        [SerializeField, Min(0f)] private float grenadeRadius = 0.032f;

        [SerializeField] private LayerMask collisionMask = ~0;

        private LineRenderer _line;
        private Vector3[] _points;

        /// <summary>선의 마지막 점 = 수류탄이 떨어질 자리.</summary>
        public Vector3 LandingPoint { get; private set; }

        private void Awake()
        {
            _points = new Vector3[maxSteps];
            BuildLine();
            Hide();
        }

        private void BuildLine()
        {
            var go = new GameObject("GrenadeArc");
            go.transform.SetParent(transform, false);

            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = lineWidth;
            _line.numCapVertices = 4;
            _line.numCornerVertices = 4;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.material = CreateLineMaterial();
            _line.startColor = _line.endColor = lineColor;
        }

        /// <summary>
        /// 런타임에 만든 URP Unlit 머티리얼은 블렌드 상태가 Zero/Zero로 초기화돼서
        /// <b>화면에 아무것도 안 그려진다.</b> 에러도 안 나서 원인을 찾기 어렵다.
        /// 그래서 불투명 렌더링에 필요한 값을 전부 직접 지정해야 한다. (10-5에서 하루 종일 헤맸던 문제)
        /// </summary>
        private Material CreateLineMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            mat.SetColor("_BaseColor", lineColor);
            mat.SetFloat("_Surface", 0f);      // Opaque
            mat.SetFloat("_SrcBlend", 1f);     // One
            mat.SetFloat("_DstBlend", 0f);     // Zero
            mat.SetFloat("_ZWrite", 1f);
            mat.SetFloat("_Cull", 0f);         // 양면
            mat.renderQueue = 2000;

            return mat;
        }

        public void Show()
        {
            if (_line != null) _line.enabled = true;
        }

        public void Hide()
        {
            if (_line != null) _line.enabled = false;
        }

        /// <summary>
        /// 던지는 지점과 속도를 받아 포물선을 다시 그린다.
        /// 실제 던지기와 <b>같은 시작점·같은 속도</b>를 넣어야 예측선과 실제 궤적이 일치한다.
        /// </summary>
        public void UpdateArc(Vector3 origin, Vector3 velocity)
        {
            if (_line == null) return;

            Vector3 gravity = Physics.gravity;
            Vector3 point = origin;
            Vector3 speed = velocity;

            _points[0] = point;
            int count = 1;

            for (int i = 1; i < maxSteps; i++)
            {
                Vector3 next = point + speed * timeStep + 0.5f * gravity * timeStep * timeStep;
                speed += gravity * timeStep;

                Vector3 delta = next - point;
                float distance = delta.magnitude;

                // 걸음 사이를 구체로 훑어서 벽·바닥에 닿으면 거기서 끊는다.
                if (distance > 0.0001f && Physics.SphereCast(point, grenadeRadius, delta / distance,
                        out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                {
                    _points[count++] = hit.point;
                    break;
                }

                _points[count++] = next;
                point = next;
            }

            LandingPoint = _points[count - 1];

            _line.positionCount = count;
            _line.SetPositions(_points);
        }
    }
}
