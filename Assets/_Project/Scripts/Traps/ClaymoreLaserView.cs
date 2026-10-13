using KillOrDead.Combat;
using UnityEngine;
using UnityEngine.Serialization;

namespace KillOrDead.Traps
{
    /// <summary>
    /// 크레모어 레이저의 "보이는 부분"만 담당한다. 판정은 하지 않는다.
    ///
    /// - 빔: 감지 레이 31개를 전부 그리되, <b>가장 양옆 2줄만 또렷하게</b>, 그 사이 29줄은 <b>아주 은은하게</b> 그린다.
    ///   ("레이저가 너무 많고 밝다" → 양옆만 남겼더니 허전하다 → 사이를 은은한 빛으로 채움)
    ///   빔 끝점은 감지 레이 결과를 그대로 받으므로, 벽에 막히면 보이는 선도 거기서 끊긴다 (보이는 레이저 = 판정).
    ///   빔 방향은 감지 각도에서 나오므로, 감지 각도를 줄이면 부채꼴 전체가 같이 좁아진다.
    /// - 끝점: 빔이 벽·바닥·몸에 닿았으면 그 자리에 작은 빨간 점.
    /// - LED: 센서 옆 빨간 LED가 천천히 깜빡인다.
    /// - 전자음: 가까이 가야 들리는 작은 소리 (클립이 비어 있으면 무음).
    /// - 센서가 부서지면(<see cref="ClaymoreMine.IsSensorActive"/> = false) 빔·점·소리를 모두 끈다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Traps/Claymore Laser View")]
    [RequireComponent(typeof(ClaymoreMine))]
    public class ClaymoreLaserView : MonoBehaviour
    {
        [Header("Beam")]
        [Tooltip("빔 색.")]
        [SerializeField] private Color laserColor = new Color(1f, 0.05f, 0.03f, 1f);

        [Tooltip("양 끝 빔 2줄의 밝기. 색에 곱하는 값이라 0이면 안 보이고 클수록 밝다. 플레이 중에 바꿔도 바로 반영된다.")]
        [FormerlySerializedAs("laserIntensity")]
        [SerializeField, Range(0f, 5f)] private float laserBrightness = 1f;

        [Tooltip("양 끝 사이를 채우는 빔 29줄의 밝기. 아주 은은하게 — 0이면 양 끝만 보인다. 플레이 중에 바꿔도 바로 반영된다.")]
        [SerializeField, Range(0f, 1f)] private float fillBrightness = 0.25f;   // 0.08은 밝은 바닥에서 아예 안 보였다

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
        private LineRenderer[] _beams;   // 감지 레이와 같은 순서. [0] = 왼쪽 끝, [마지막] = 오른쪽 끝
        private Transform[] _dots;       // 끝점 빨간 점은 양 끝 빔에만 (사이 빔까지 찍으면 다시 지저분해진다)
        private GameObject _beamRoot;
        private AudioSource _hum;
        private Material _ledOnMaterial;
        private Material _ledOffMaterial;
        private Material _beamMaterial;
        private Material _dotMaterial;
        private Material _fillMaterial;
        private float _appliedBrightness = -1f;
        private float _appliedFillBrightness = -1f;

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
            // 양 끝 빔·점·사이 빔이 각각 머티리얼 하나씩을 같이 쓴다.
            // 가산 합성(뒤 배경에 색을 더함)이라 빛나는 선처럼 보이고, 색이 어두울수록 더 은은해진다.
            // 색(밝기)은 ApplyBrightness에서 넣는다.
            _beamMaterial = RuntimeUnlitMaterial.CreateAdditive(laserColor);
            _dotMaterial = RuntimeUnlitMaterial.CreateAdditive(laserColor);
            _fillMaterial = RuntimeUnlitMaterial.CreateAdditive(laserColor);

            _beamRoot = new GameObject("LaserBeams");
            _beamRoot.transform.SetParent(transform, false);

            int count = _mine.BeamCount;
            _beams = new LineRenderer[count];
            _dots = new Transform[2];

            for (int i = 0; i < count; i++)
            {
                bool isEdge = IsEdge(i);
                var beam = new GameObject(isEdge ? (i == 0 ? "Beam_Left" : "Beam_Right") : $"Beam_Fill_{i:00}")
                    .AddComponent<LineRenderer>();
                beam.transform.SetParent(_beamRoot.transform, false);
                beam.useWorldSpace = true;
                beam.positionCount = 2;
                beam.startWidth = beamWidth;
                beam.endWidth = beamWidth;
                beam.sharedMaterial = isEdge ? _beamMaterial : _fillMaterial;
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.receiveShadows = false;
                beam.numCapVertices = 0;
                _beams[i] = beam;
            }

            for (int d = 0; d < 2; d++)
            {
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = d == 0 ? "HitDot_Left" : "HitDot_Right";
                Destroy(dot.GetComponent<Collider>());   // 점이 빔을 막으면 안 된다
                dot.transform.SetParent(_beamRoot.transform, false);
                dot.transform.localScale = Vector3.one * hitDotSize;
                var dotRenderer = dot.GetComponent<MeshRenderer>();
                dotRenderer.sharedMaterial = _dotMaterial;
                dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dotRenderer.receiveShadows = false;
                _dots[d] = dot.transform;
            }

            ApplyBrightness();
        }

        private bool IsEdge(int i) => i == 0 || i == _mine.BeamCount - 1;

        /// <summary>밝기 값을 머티리얼 색에 반영한다. 값이 바뀌었을 때만 실제로 쓴다.</summary>
        private void ApplyBrightness()
        {
            if (!Mathf.Approximately(_appliedBrightness, laserBrightness))
            {
                SetColor(_beamMaterial, laserBrightness);
                SetColor(_dotMaterial, laserBrightness);
                _appliedBrightness = laserBrightness;
            }

            if (!Mathf.Approximately(_appliedFillBrightness, fillBrightness))
            {
                SetColor(_fillMaterial, fillBrightness);
                _appliedFillBrightness = fillBrightness;
            }
        }

        private void SetColor(Material material, float brightness)
        {
            if (material == null) return;

            Color color = laserColor * brightness;
            color.a = 1f;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
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

            _hum = gameObject.AddComponent<AudioSource>();
            _hum.clip = humSound;
            _hum.loop = true;
            _hum.volume = humVolume;
            _hum.spatialBlend = 1f;                                   // 완전 3D 소리
            _hum.rolloffMode = AudioRolloffMode.Linear;
            _hum.minDistance = 0.5f;
            _hum.maxDistance = humMaxDistance;
            _hum.Play();
        }

        // 판정(FixedUpdate)이 끝난 뒤, 화면에 그리기 직전에 빔을 맞춘다.
        private void LateUpdate()
        {
            if (_beams == null) return;

            // 센서가 부서지면 레이저가 꺼진다 (기획: 센서 피격 → 무력화, 레이저 OFF).
            // LED는 센서 모델(Sensor_Intact) 밑에 붙어 있어서 센서가 꺼질 때 같이 사라진다.
            bool active = _mine.IsSensorActive;
            if (_beamRoot.activeSelf != active) _beamRoot.SetActive(active);
            if (_hum != null && _hum.isPlaying != active)
            {
                if (active) _hum.Play();
                else _hum.Stop();
            }
            if (!active) return;

            ApplyBrightness();

            Vector3 origin = _mine.LaserOrigin.position;

            for (int i = 0; i < _beams.Length; i++)
            {
                _beams[i].SetPosition(0, origin);
                _beams[i].SetPosition(1, _mine.GetBeamEnd(i));
            }

            // 끝점 빨간 점은 양 끝 빔에만.
            UpdateDot(_dots[0], 0);
            UpdateDot(_dots[1], _beams.Length - 1);

            UpdateLed();
        }

        private void UpdateDot(Transform dot, int beamIndex)
        {
            bool show = _mine.BeamHitSomething(beamIndex);
            if (dot.gameObject.activeSelf != show) dot.gameObject.SetActive(show);
            if (show) dot.position = _mine.GetBeamEnd(beamIndex);
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
            if (_fillMaterial != null) Destroy(_fillMaterial);
        }
    }
}
