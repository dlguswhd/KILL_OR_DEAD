using KillOrDead.Combat;
using UnityEngine;

namespace KillOrDead.Traps
{
    /// <summary>
    /// 크레모어 레이저의 "보이는 부분"만 담당한다. 판정은 하지 않는다.
    ///
    /// - 빔: <see cref="ClaymoreMine"/>가 감지 레이로 구한 끝점을 그대로 받아 선을 긋는다.
    ///   그래서 보이는 빔과 판정이 절대 어긋나지 않는다 (기획 설계 원칙 "보이는 레이저 = 판정").
    /// - 끝점: 빔이 벽·바닥·몸에 닿았으면 그 자리에 작은 빨간 점.
    /// - LED: 센서 옆 빨간 LED가 천천히 깜빡인다.
    /// - 전자음: 가까이 가야 들리는 작은 소리 (클립이 비어 있으면 무음).
    ///
    /// 빔·점은 에셋을 따로 두지 않고 실행 시 코드로 만든다. 31개를 프리팹에 손으로 깔면
    /// 빔 개수를 바꿀 때마다 다시 깔아야 하기 때문이다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Traps/Claymore Laser View")]
    [RequireComponent(typeof(ClaymoreMine))]
    public class ClaymoreLaserView : MonoBehaviour
    {
        [Header("Beam")]
        [Tooltip("빔 색. 밝기(Intensity)는 아래 값으로 곱한다.")]
        [SerializeField] private Color laserColor = new Color(1f, 0.05f, 0.03f, 1f);

        [Tooltip("색에 곱하는 밝기. 카메라에서 HDR + Bloom이 켜져 있으면 1보다 큰 값이 은은하게 번진다.")]
        [SerializeField, Min(0f)] private float laserIntensity = 3f;

        [Tooltip("빔 두께(m). 기획: 아주 얇게. 3mm는 화면 픽셀보다 얇아져서 멀리서 점선처럼 끊겨 보였다 → 6mm.")]
        [SerializeField, Min(0.0005f)] private float beamWidth = 0.006f;

        [Tooltip("빔이 닿은 곳에 찍히는 점의 지름(m).")]
        [SerializeField, Min(0f)] private float hitDotSize = 0.012f;

        [Header("LED")]
        [Tooltip("깜빡일 LED 렌더러. 센서 렌즈 옆에 붙은 작은 구.")]
        [SerializeField] private Renderer ledRenderer;

        [SerializeField] private Color ledOnColor = new Color(1f, 0.05f, 0.03f, 1f);
        [SerializeField] private Color ledOffColor = new Color(0.12f, 0.01f, 0.01f, 1f);

        [Tooltip("한 번 켜졌다 꺼지는 데 걸리는 시간(초). 기획: 천천히 깜빡임.")]
        [SerializeField, Min(0.05f)] private float ledBlinkPeriod = 1.5f;

        [Header("Sound")]
        [Tooltip("작동 중 반복 재생되는 작은 전자음. 비워두면 무음.")]
        [SerializeField] private AudioClip humSound;

        [SerializeField, Range(0f, 1f)] private float humVolume = 0.25f;

        [Tooltip("이 거리 밖에서는 안 들린다(m). 기획: 가까이 가야 들리게.")]
        [SerializeField, Min(0.1f)] private float humMaxDistance = 4f;

        private ClaymoreMine _mine;
        private LineRenderer[] _beams;
        private Transform[] _dots;
        private Material _ledOnMaterial;
        private Material _ledOffMaterial;
        private Material _beamMaterial;
        private Material _dotMaterial;

        private void Awake()
        {
            _mine = GetComponent<ClaymoreMine>();
        }

        private void Start()
        {
            BuildBeams();
            SetupLed();
            SetupHum();
        }

        private void BuildBeams()
        {
            Color hdrColor = laserColor * laserIntensity;
            hdrColor.a = 1f;

            // 빔 31개와 점 31개가 머티리얼 하나씩을 같이 쓴다.
            // 가산 합성(뒤 배경에 색을 더함)이라 빛나는 선처럼 보인다.
            _beamMaterial = RuntimeUnlitMaterial.CreateAdditive(hdrColor);
            _dotMaterial = RuntimeUnlitMaterial.CreateAdditive(hdrColor);

            var root = new GameObject("LaserBeams").transform;
            root.SetParent(transform, false);

            int count = _mine.BeamCount;
            _beams = new LineRenderer[count];
            _dots = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                var beam = new GameObject($"Beam_{i:00}").AddComponent<LineRenderer>();
                beam.transform.SetParent(root, false);
                beam.useWorldSpace = true;
                beam.positionCount = 2;
                beam.startWidth = beamWidth;
                beam.endWidth = beamWidth;
                beam.sharedMaterial = _beamMaterial;
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.receiveShadows = false;
                beam.numCapVertices = 0;
                _beams[i] = beam;

                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = $"HitDot_{i:00}";
                Destroy(dot.GetComponent<Collider>());   // 점이 빔을 막으면 안 된다
                dot.transform.SetParent(root, false);
                dot.transform.localScale = Vector3.one * hitDotSize;
                var dotRenderer = dot.GetComponent<MeshRenderer>();
                dotRenderer.sharedMaterial = _dotMaterial;
                dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dotRenderer.receiveShadows = false;
                _dots[i] = dot.transform;
            }
        }

        private void SetupLed()
        {
            if (ledRenderer == null) return;

            _ledOnMaterial = RuntimeUnlitMaterial.CreateOpaque(ledOnColor);
            _ledOffMaterial = RuntimeUnlitMaterial.CreateOpaque(ledOffColor);
            ledRenderer.sharedMaterial = _ledOffMaterial;
            ledRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void SetupHum()
        {
            if (humSound == null) return;

            var source = gameObject.AddComponent<AudioSource>();
            source.clip = humSound;
            source.loop = true;
            source.volume = humVolume;
            source.spatialBlend = 1f;                                   // 완전 3D 소리
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.5f;
            source.maxDistance = humMaxDistance;
            source.Play();
        }

        // 판정(FixedUpdate)이 끝난 뒤, 화면에 그리기 직전에 빔을 맞춘다.
        private void LateUpdate()
        {
            if (_beams == null) return;

            Vector3 origin = _mine.LaserOrigin.position;

            for (int i = 0; i < _beams.Length; i++)
            {
                Vector3 end = _mine.GetBeamEnd(i);
                _beams[i].SetPosition(0, origin);
                _beams[i].SetPosition(1, end);

                bool showDot = _mine.BeamHitSomething(i);
                if (_dots[i].gameObject.activeSelf != showDot) _dots[i].gameObject.SetActive(showDot);
                if (showDot) _dots[i].position = end;
            }

            UpdateLed();
        }

        private void UpdateLed()
        {
            if (ledRenderer == null) return;

            bool on = Mathf.Repeat(Time.time, ledBlinkPeriod) < ledBlinkPeriod * 0.5f;
            var target = on ? _ledOnMaterial : _ledOffMaterial;
            if (ledRenderer.sharedMaterial != target) ledRenderer.sharedMaterial = target;
        }

        private void OnDestroy()
        {
            // 코드로 만든 머티리얼은 직접 치워야 메모리에 남지 않는다.
            if (_ledOnMaterial != null) Destroy(_ledOnMaterial);
            if (_ledOffMaterial != null) Destroy(_ledOffMaterial);
            if (_beamMaterial != null) Destroy(_beamMaterial);
            if (_dotMaterial != null) Destroy(_dotMaterial);
        }
    }
}
