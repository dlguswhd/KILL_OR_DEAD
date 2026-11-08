using KillOrDead.Combat;
using KillOrDead.UI;
using UnityEngine;

namespace KillOrDead.Traps
{
    /// <summary>
    /// 크레모어의 "보이고 들리는 부분"만 담당한다. 판정은 하지 않는다.
    ///
    /// - 레이저 빔은 그리지 않는다. 감지는 <see cref="ClaymoreMine"/>이 보이지 않는 레이로 그대로 한다.
    /// - LED: 센서 옆 빨간 LED가 천천히 깜빡인다.
    /// - 부저음: 플레이어가 일정 범위 안에 들어오면 반복 재생하고, 나가면 멈춘다 (클립이 비어 있으면 무음).
    /// - 센서가 부서지거나 해체되면(<see cref="ClaymoreMine.IsArmed"/> = false) LED·부저음을 모두 끈다.
    /// - 센서가 총에 맞아 부서지는 순간 전기 스파크 이펙트와 소리를 한 번 낸다.
    /// - 해체(선 절단)·센서 파괴 순간, 그 부위를 화면 오른쪽 위 칸에 확대해서 보여준다(<see cref="ClaymoreInspectUI"/>).
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Traps/Claymore Laser View")]
    [RequireComponent(typeof(ClaymoreMine))]
    public class ClaymoreLaserView : MonoBehaviour
    {
        [Header("LED")]
        [Tooltip("깜빡일 LED 렌더러. 센서 렌즈 옆에 붙은 작은 구.")]
        [SerializeField] private Renderer ledRenderer;

        [SerializeField] private Color ledOnColor = new Color(1f, 0.05f, 0.03f, 1f);
        [SerializeField] private Color ledOffColor = new Color(0.12f, 0.01f, 0.01f, 1f);

        [Tooltip("한 번 켜졌다 꺼지는 데 걸리는 시간(초). 기획: 천천히 깜빡임.")]
        [SerializeField, Min(0.05f)] private float ledBlinkPeriod = 1.5f;

        [Header("Buzzer")]
        [Tooltip("플레이어가 범위 안에 있는 동안 반복 재생되는 부저음. 비워두면 무음.")]
        [SerializeField] private AudioClip buzzerSound;

        [SerializeField, Range(0f, 1f)] private float buzzerVolume = 0.5f;

        [Tooltip("플레이어가 크레모어로부터 이 거리(m) 안에 들어오면 부저음이 울린다. 멀어질수록 작게 들린다.")]
        [SerializeField, Min(0.1f)] private float buzzerRange = 6f;

        [Header("Sensor Break (센서 피격)")]
        [Tooltip("센서가 총에 맞아 부서지는 순간 센서 렌즈 위치에 생성할 전기 스파크 이펙트. 비워두면 안 나온다.")]
        [SerializeField] private GameObject sensorBreakEffect;

        [Tooltip("센서가 부서지는 순간 재생할 소리. 비워두면 무음.")]
        [SerializeField] private AudioClip sensorBreakSound;

        [SerializeField, Range(0f, 1f)] private float sensorBreakVolume = 1f;

        [Header("Inspect (확대 화면)")]
        [Tooltip("선이 잘렸을 때(해체) 확대해서 보여줄 지점. 이 오브젝트의 앞(+Z)이 확대 카메라가 바라보는 방향이다.")]
        [SerializeField] private Transform cableInspectFocus;

        [Tooltip("센서가 부서졌을 때 확대해서 보여줄 지점. 이 오브젝트의 앞(+Z)이 확대 카메라가 바라보는 방향이다.")]
        [SerializeField] private Transform sensorInspectFocus;

        private ClaymoreMine _mine;
        private ClaymoreInspectUI _inspectUI;
        private bool _wasDisarmed;
        private AudioSource _buzzer;
        private Material _ledOnMaterial;
        private Material _ledOffMaterial;
        private bool _sensorWasActive;

        private void Awake()
        {
            _mine = GetComponent<ClaymoreMine>();
        }

        private void Start()
        {
            SetupLed();
            SetupBuzzer();
            _sensorWasActive = _mine.IsSensorActive;
            _wasDisarmed = _mine.IsDisarmed;
        }

        private void SetupLed()
        {
            if (ledRenderer == null) return;

            _ledOnMaterial = RuntimeUnlitMaterial.CreateOpaque(ledOnColor);
            _ledOffMaterial = RuntimeUnlitMaterial.CreateOpaque(ledOffColor);
            ledRenderer.sharedMaterial = _ledOffMaterial;
            ledRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void SetupBuzzer()
        {
            if (buzzerSound == null) return;

            _buzzer = gameObject.AddComponent<AudioSource>();
            _buzzer.clip = buzzerSound;
            _buzzer.loop = true;
            _buzzer.playOnAwake = false;
            _buzzer.volume = buzzerVolume;
            _buzzer.spatialBlend = 1f;                                // 완전 3D 소리 — 소리 나는 방향을 알 수 있다
            _buzzer.rolloffMode = AudioRolloffMode.Linear;
            _buzzer.minDistance = 0.5f;
            _buzzer.maxDistance = buzzerRange;                        // 범위 끝에서 0, 가까울수록 크게
        }

        private void LateUpdate()
        {
            // 센서가 부서지거나 해체되면 LED·부저음이 꺼진다 (기획: 센서 피격 → 무력화).
            bool active = _mine.IsArmed;

            UpdateBuzzer(active);
            UpdateLed(active);
            CheckSensorBreak();
            CheckDisarm();
        }

        /// <summary>해체되지 않았다가 이번에 해체됐으면(선이 잘렸으면) 잘린 선을 확대 화면으로 보여준다.</summary>
        private void CheckDisarm()
        {
            bool disarmed = _mine.IsDisarmed;
            if (!_wasDisarmed && disarmed) ShowInspect(cableInspectFocus);
            _wasDisarmed = disarmed;
        }

        // 화면 오른쪽 위 확대 칸에 부위를 띄운다. UI는 씬 Canvas에 하나 있고, 처음 쓸 때 찾아 둔다.
        private void ShowInspect(Transform focus)
        {
            if (focus == null) return;

            if (_inspectUI == null)
                _inspectUI = FindFirstObjectByType<ClaymoreInspectUI>(FindObjectsInactive.Include);

            if (_inspectUI != null) _inspectUI.Show(focus);
        }

        /// <summary>
        /// 센서가 살아 있다가 이번에 부서졌으면 스파크와 소리를 한 번 내고, 부서진 센서를 확대 화면으로 보여준다.
        /// 센서 파괴는 총(HitscanTacticalWeapon)이 ClaymoreParts.DestroySensor()를 불러서 일어나는데,
        /// 여기서는 그 결과(살아 있음 → 부서짐)만 보고 반응한다. 이미 부서진 센서를 또 맞혀도 다시 나오지 않는다.
        /// </summary>
        private void CheckSensorBreak()
        {
            bool sensorActive = _mine.IsSensorActive;
            if (_sensorWasActive && !sensorActive) PlaySensorBreak();
            _sensorWasActive = sensorActive;
        }

        private void PlaySensorBreak()
        {
            Vector3 position = _mine.LaserOrigin.position;   // 센서 렌즈 중심

            if (sensorBreakEffect != null)
            {
                // 이펙트는 다 튀고 나면 스스로 사라진다(파티클 Stop Action = Destroy).
                Instantiate(sensorBreakEffect, position, Quaternion.LookRotation(_mine.transform.forward));
            }

            if (sensorBreakSound != null)
            {
                AudioSource.PlayClipAtPoint(sensorBreakSound, position, sensorBreakVolume);
            }

            ShowInspect(sensorInspectFocus);
        }

        private void UpdateBuzzer(bool active)
        {
            if (_buzzer == null) return;

            bool play = active && IsPlayerInBuzzerRange();
            if (_buzzer.isPlaying == play) return;

            if (play) _buzzer.Play();
            else _buzzer.Stop();
        }

        private bool IsPlayerInBuzzerRange()
        {
            Transform player = _mine.Player;
            if (player == null) return false;
            return (player.position - transform.position).sqrMagnitude <= buzzerRange * buzzerRange;
        }

        private void UpdateLed(bool active)
        {
            if (ledRenderer == null) return;

            // 꺼진 크레모어는 LED도 꺼진 색으로 둔다. 해체는 센서 모델이 남아 있으니 따로 꺼야 한다.
            bool on = active && Mathf.Repeat(Time.time, ledBlinkPeriod) < ledBlinkPeriod * 0.5f;
            var target = on ? _ledOnMaterial : _ledOffMaterial;
            if (ledRenderer.sharedMaterial != target) ledRenderer.sharedMaterial = target;
        }

        private void OnDestroy()
        {
            // 코드로 만든 머티리얼은 직접 치워야 메모리에 남지 않는다.
            if (_ledOnMaterial != null) Destroy(_ledOnMaterial);
            if (_ledOffMaterial != null) Destroy(_ledOffMaterial);
        }
    }
}
