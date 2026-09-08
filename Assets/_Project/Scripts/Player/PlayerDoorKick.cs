using System;
using UnityEngine;

namespace KillOrDead.Player
{
    /// <summary>
    /// 왼발 발차기. 캐릭터 컨트롤러의 <b>Kick 레이어</b>(왼쪽 다리 전용 마스크 `Mask_LeftLeg`, Override)로 재생한다.
    ///
    /// 마스크에 왼쪽 다리(`thigh_l` 이하)만 들어 있어서 <b>상체·오른쪽 다리는 기존 동작 그대로</b> 유지된다.
    /// 총을 든 자세도 그대로다.
    ///
    /// 레이어 기본 가중치는 0이라 평소엔 발차기 동작이 전혀 섞이지 않고,
    /// 이 스크립트가 발차기 도중에만 0 → 1 → 0으로 직접 올렸다 내린다.
    /// (수류탄 투척 <see cref="GrenadeThrower"/>와 같은 방식이다.)
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Player/Player Door Kick")]
    public class PlayerDoorKick : MonoBehaviour
    {
        private const string LayerName = "Kick";
        private const string StateName = "Kick";
        private const string TriggerName = "KickDoor";

        [Header("Timing")]
        [Tooltip("발이 목표물에 닿는 시점(클립 정규화 시간 0~1). 문이 '퍽' 하고 열리는 순간이 여기에 맞춰진다.\n" +
                 "왼발이 가장 앞·위로 뻗는 지점을 실측해서 0.54로 잡았다(앞 0.83m / 높이 1.12m).")]
        [SerializeField, Range(0f, 1f)] private float impactNormalizedTime = 0.54f;

        [Tooltip("한 번 찬 뒤 다시 찰 수 있을 때까지의 시간(초). 연타 방지용.")]
        [SerializeField, Min(0f)] private float cooldown = 0.8f;

        [Tooltip("발차기 레이어가 켜지는 데 걸리는 시간(초).")]
        [SerializeField, Min(0f)] private float blendInTime = 0.08f;

        [Tooltip("발차기 레이어가 꺼지는 데 걸리는 시간(초).")]
        [SerializeField, Min(0f)] private float blendOutTime = 0.18f;

        [Header("조준")]
        [Tooltip("보고 있는 방향으로 내지른다. 위를 보면 위로, 아래를 보면 아래로 찬다.\n" +
                 "끄면 클립 그대로 몸이 향한 정면으로만 찬다.")]
        [SerializeField] private bool kickTowardLook = true;

        [Tooltip("몸이 향한 방향에서 이 각도까지만 따라간다. 너무 크면 다리가 꺾여 보인다.")]
        [SerializeField, Range(0f, 80f)] private float maxAimAngle = 55f;

        private Animator _animator;
        private int _layerIndex = -1;

        /// <summary>차는 다리(왼쪽 허벅지). 여기를 돌리면 무릎·발이 통째로 따라온다.</summary>
        private Transform _kickThigh;
        private Camera _camera;

        private bool _kicking;
        private bool _stateEntered;
        private bool _impactFired;
        private float _weight;
        private float _cooldownTimer;

        public bool InputEnabled { get; set; } = true;

        /// <summary>지금 발차기 동작 중인지.</summary>
        public bool IsKicking => _kicking;

        /// <summary>다시 찰 수 있는 상태인지(쿨다운이 끝났고 동작 중이 아님).</summary>
        public bool CanKick => !_kicking && _cooldownTimer <= 0f && _layerIndex >= 0 && InputEnabled;

        /// <summary>발이 목표물에 닿는 순간. 문을 이 시점에 열어야 발차기와 문이 따로 놀지 않는다.</summary>
        public event Action KickImpact;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>(true);
            _camera = GetComponentInChildren<Camera>(true);

            if (_animator != null)
            {
                foreach (var t in _animator.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "thigh_l") continue;
                    _kickThigh = t;
                    break;
                }
            }
        }

        private void OnEnable()
        {
            // 무기를 바꾸면 컨트롤러가 통째로 교체되므로 레이어 번호를 그때그때 다시 찾는다.
            _layerIndex = -1;
        }

        private void OnDisable()
        {
            if (_kicking) FinishKick();
        }

        private void Update()
        {
            if (_animator == null) return;

            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            ResolveLayerIndex();
            if (_layerIndex < 0) return;

            if (_kicking) UpdateKick();

            _animator.SetLayerWeight(_layerIndex, _weight);
        }

        /// <summary>
        /// 애니메이터가 자세를 다 잡은 뒤(LateUpdate) 차는 다리를 <b>보고 있는 방향</b>으로 돌린다.
        ///
        /// 카메라는 머리 뼈에 붙어 있어서 위아래로 볼 때 몸통 방향과 어긋난다.
        /// 그 차이만큼 허벅지를 월드 기준으로 돌려 주면 무릎·발이 통째로 따라와서 시선 쪽으로 내지른다.
        /// 발차기 레이어 가중치만큼만 섞으므로 시작·끝에서 튀지 않는다.
        /// </summary>
        private void LateUpdate()
        {
            if (!kickTowardLook || !_kicking) return;
            if (_kickThigh == null || _camera == null || _weight <= 0.001f) return;

            var bodyForward = transform.forward;
            var lookForward = _camera.transform.forward;

            // 너무 많이 꺾이지 않게 제한한다.
            if (Vector3.Angle(bodyForward, lookForward) > maxAimAngle)
            {
                lookForward = Vector3.RotateTowards(bodyForward, lookForward, maxAimAngle * Mathf.Deg2Rad, 0f);
            }

            var aim = Quaternion.Slerp(Quaternion.identity,
                Quaternion.FromToRotation(bodyForward, lookForward), _weight);

            // 월드 회전에 그대로 곱한다. 부모(골반) 기준 로컬 값은 유니티가 알아서 환산해 준다.
            _kickThigh.rotation = aim * _kickThigh.rotation;
        }

        private void ResolveLayerIndex()
        {
            // 컨트롤러가 바뀌면 인덱스가 달라질 수 있어서 이름으로 다시 찾는다.
            if (_layerIndex >= 0 && _layerIndex < _animator.layerCount
                && _animator.GetLayerName(_layerIndex) == LayerName)
            {
                return;
            }

            _layerIndex = -1;
            for (int i = 0; i < _animator.layerCount; i++)
            {
                if (_animator.GetLayerName(i) != LayerName) continue;
                _layerIndex = i;
                break;
            }
        }

        /// <summary>발차기를 시작한다. 시작했으면 true, 쿨다운 등으로 못 찼으면 false.</summary>
        public bool TryKick()
        {
            if (_animator == null) return false;

            ResolveLayerIndex();
            if (!CanKick) return false;

            _kicking = true;
            _stateEntered = false;
            _impactFired = false;
            _weight = 0f;
            _cooldownTimer = cooldown;

            _animator.SetTrigger(TriggerName);
            return true;
        }

        private void UpdateKick()
        {
            var state = _animator.GetCurrentAnimatorStateInfo(_layerIndex);
            bool inKickState = state.IsName(StateName);

            // 트리거를 넣은 직후 몇 프레임은 아직 Empty 상태다. 그동안은 가중치만 올려둔다.
            if (!inKickState && !_stateEntered)
            {
                _weight = Approach(_weight, 1f, blendInTime);
                return;
            }

            _stateEntered = true;

            if (!inKickState)
            {
                // 클립이 끝나 Empty로 돌아왔다. 남은 가중치를 내리고 마무리한다.
                _weight = Approach(_weight, 0f, blendOutTime);
                if (_weight <= 0.001f) FinishKick();
                return;
            }

            float t = Mathf.Clamp01(state.normalizedTime);

            if (!_impactFired && t >= impactNormalizedTime)
            {
                _impactFired = true;
                KickImpact?.Invoke();
            }

            _weight = Approach(_weight, 1f, blendInTime);
        }

        private void FinishKick()
        {
            _kicking = false;
            _stateEntered = false;
            _weight = 0f;

            // 임팩트 시점 전에 끊겼더라도 구독자가 계속 기다리지 않게 한 번은 알린다.
            if (!_impactFired)
            {
                _impactFired = true;
                KickImpact?.Invoke();
            }
        }

        /// <summary>지정한 시간에 걸쳐 목표값까지 가는 선형 보간 한 스텝.</summary>
        private static float Approach(float current, float target, float duration)
        {
            if (duration <= 0f) return target;
            return Mathf.MoveTowards(current, target, Time.deltaTime / duration);
        }
    }
}
