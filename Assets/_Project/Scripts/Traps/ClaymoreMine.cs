using System.Collections.Generic;
using KINEMATION.KShooterCore.Runtime.Camera;
using KillOrDead.Combat;
using KillOrDead.Player;
using UnityEngine;

namespace KillOrDead.Traps
{
    /// <summary>
    /// 레이저 감지형 크레모어 (노션 "부비트랩 기획서 - 레이저 감지형 크레모어" 기준).
    ///
    /// 동작 순서
    /// 1. 센서 렌즈(<see cref="laserOrigin"/>)에서 수평 부채꼴로 레이저 빔 31개를 쏜다 (60°, 5m).
    /// 2. 빔 하나 = 감지 레이 하나다. 화면에 보이는 빔과 판정이 항상 같다.
    ///    벽·엄폐물에 닿으면 빔은 거기서 끊기고, 그 뒤는 감지하지 않는다.
    /// 3. 빔에 플레이어가 걸리면 지연 없이 즉시 기폭한다.
    /// 4. 폭발 피해는 범위형이다 — 전방 60° 부채꼴(파편, 100m) + 원형 후폭풍(16m).
    ///    폭발 지점과 대상 사이가 벽으로 막혀 있으면 피해가 없다.
    /// 5. 본체는 폭발과 동시에 사라진다.
    ///
    /// 빔을 그리는 일은 <see cref="ClaymoreLaserView"/>가 맡는다. 이 클래스는 판정만 한다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Traps/Claymore Mine")]
    public class ClaymoreMine : MonoBehaviour
    {
        [Header("Detection (기획 확정값)")]
        [Tooltip("레이저가 나가는 지점. 센서 렌즈 중심에 둔다. 반드시 본체 콜라이더 바깥에 있어야 한다(안쪽이면 빔이 자기 몸에 막힌다).")]
        [SerializeField] private Transform laserOrigin;

        [Tooltip("감지 반경(m). 기획 확정 5m.")]
        [SerializeField, Min(0.1f)] private float detectionRadius = 5f;

        [Tooltip("감지 각도(도). 정면 기준 좌우 절반씩. 기획 확정 60°.")]
        [SerializeField, Range(1f, 180f)] private float detectionAngle = 60f;

        [Tooltip("레이저 빔 개수. 기획 초안 31개(2° 간격). 5m 끝에서도 빔 사이가 약 17cm라 사람이 빠져나갈 수 없다.")]
        [SerializeField, Min(2)] private int beamCount = 31;

        [Tooltip("빔이 부딪히는 레이어. 여기에 없는 레이어는 빔이 그냥 통과한다.")]
        [SerializeField] private LayerMask detectionMask = ~0;

        [Tooltip("플레이어가 이 거리 안에 있을 때만 감지 판정을 돌린다(최적화). 감지 반경보다 넉넉히 크게.")]
        [SerializeField, Min(0f)] private float activationDistance = 10f;

        [Header("Explosion — 범위 (기획 초안, 실제 M18A1 수치)")]
        [Tooltip("전방 파편 부채꼴의 길이(m). 각도는 감지 각도와 같다.")]
        [SerializeField, Min(0f)] private float fragmentRange = 100f;

        [Tooltip("후폭풍 원의 반경(m). 전방 부채꼴 밖의 모든 방향.")]
        [SerializeField, Min(0f)] private float blastRadius = 16f;

        [Tooltip("벽·엄폐물 판정에 쓰는 레이어.")]
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        [Header("Explosion — 피해 수치 (기획 미정, 정해지면 입력)")]
        [Tooltip("레이저에 걸린 대상은 즉사(기획 확정). 이 값과 상관없이 최대치가 들어간다.")]
        [SerializeField, Min(0f)] private float fragmentDamage = 0f;

        [SerializeField, Min(0f)] private float blastDamage = 0f;

        [Header("Explosion — 연출")]
        [Tooltip("폭발 이펙트. WarFX는 반드시 Unlit 버전을 쓸 것(Lit은 URP에서 분홍색으로 깨진다).")]
        [SerializeField] private GameObject explosionEffect;

        [SerializeField, Min(0f)] private float effectLifetime = 6f;
        [SerializeField] private AudioClip explosionSound;
        [SerializeField, Min(0f)] private float explosionVolume = 1f;

        [Tooltip("범위 안 물리 물체를 밀어내는 힘(AddExplosionForce). 0이면 밀지 않는다.")]
        [SerializeField, Min(0f)] private float explosionForce = 800f;

        [Header("Explosion — 화면 흔들림")]
        [Tooltip("Kinemation 카메라 흔들림 데이터. 총 반동 흔들림과 같은 기능을 그대로 쓴다.")]
        [SerializeField] private FPSCameraShake explosionShake;

        [Tooltip("이 거리 밖에서는 흔들리지 않는다(m). 가까울수록 세게 흔들린다(기획: 거리에 비례).")]
        [SerializeField, Min(0.1f)] private float shakeMaxDistance = 25f;

        [Tooltip("이 거리 안쪽은 최대 세기로 흔들린다(m).")]
        [SerializeField, Min(0f)] private float shakeFullDistance = 4f;

        /// <summary>즉사 판정에 넘기는 값. IDamageable이 부위 정보 없이 양만 받기 때문에 최대치로 표현한다.</summary>
        private const float LethalDamage = float.MaxValue;

        private Vector3[] _beamEnds;
        private bool[] _beamHitSomething;
        private Transform _player;
        private CharacterController _playerController;
        private bool _exploded;

        public Transform LaserOrigin => laserOrigin != null ? laserOrigin : transform;
        public int BeamCount => beamCount;

        /// <summary>빔 i의 끝점(월드 좌표). 장애물에 막히면 그 지점, 아니면 감지 반경 끝.</summary>
        public Vector3 GetBeamEnd(int i) => _beamEnds[i];

        /// <summary>빔 i가 무언가(벽·바닥·몸)에 닿아서 끊겼는지. 끝점에 빨간 점을 찍을지 정할 때 쓴다.</summary>
        public bool BeamHitSomething(int i) => _beamHitSomething[i];

        private void Awake()
        {
            _beamEnds = new Vector3[beamCount];
            _beamHitSomething = new bool[beamCount];
        }

        private void Start()
        {
            var locomotion = FindAnyObjectByType<PlayerLocomotion>();
            if (locomotion != null)
            {
                _player = locomotion.transform;
                _playerController = locomotion.GetComponent<CharacterController>();
            }

            // 플레이어가 멀리 있어서 판정을 건너뛰더라도, 처음 한 번은 빔 끝점을 계산해 둬야 빔이 제대로 그려진다.
            ScanBeams();
        }

        private void FixedUpdate()
        {
            if (_exploded) return;
            if (!IsPlayerNearby()) return;

            ScanBeams();
        }

        private bool IsPlayerNearby()
        {
            // 플레이어를 못 찾았으면 안전하게 항상 판정한다.
            if (_player == null) return true;
            return (_player.position - LaserOrigin.position).sqrMagnitude <= activationDistance * activationDistance;
        }

        /// <summary>
        /// 빔 31개를 한 번씩 쏴서 끝점을 갱신하고, 플레이어가 걸렸으면 기폭한다.
        /// 빔마다 "처음 닿은 것"만 본다 — 벽에 먼저 닿으면 그 뒤에 선 플레이어는 감지하지 않는다.
        /// </summary>
        private void ScanBeams()
        {
            Vector3 origin = LaserOrigin.position;

            for (int i = 0; i < beamCount; i++)
            {
                Vector3 direction = GetBeamDirection(i);

                if (Physics.Raycast(origin, direction, out RaycastHit hit, detectionRadius,
                        detectionMask, QueryTriggerInteraction.Ignore))
                {
                    _beamEnds[i] = hit.point;
                    _beamHitSomething[i] = true;

                    if (!_exploded && IsDetectionTarget(hit.collider))
                    {
                        Detonate(hit.collider);
                        return;
                    }
                }
                else
                {
                    _beamEnds[i] = origin + direction * detectionRadius;
                    _beamHitSomething[i] = false;
                }

                // 벽에 막히기 전 구간에 플레이어 몸(원기둥)이 걸쳐 있으면 기폭.
                float beamLength = Vector3.Distance(origin, _beamEnds[i]);
                if (!_exploded && BeamCrossesPlayerBody(origin, direction, beamLength, out float hitDistance))
                {
                    _beamEnds[i] = origin + direction * hitDistance;
                    _beamHitSomething[i] = true;
                    Detonate(_playerController);
                    return;
                }
            }
        }

        /// <summary>
        /// 빔이 플레이어 몸을 지나가는지 직접 계산한다.
        ///
        /// 왜 레이캐스트만으로는 안 되나: 플레이어 충돌체(CharacterController)는 발 쪽이 둥글게 깎인 알약(캡슐) 모양이다.
        /// 크레모어 빔은 무릎 아래 낮은 높이로 나가는데, 그 높이에서 캡슐은 거의 끝부분이라 폭이 좁거나 아예 없다.
        /// 실제로 빔이 발밑으로 빠져나가 정면에 서 있어도 안 터졌다.
        ///
        /// 그래서 플레이어 몸을 "발끝(지면)부터 머리까지 꽉 찬 원기둥"으로 보고, 위에서 내려다본 평면에서
        /// 빔(선분)이 원과 만나는지 계산한다. 기획서의 "서기·앉기·엎드리기 모두 걸린다"를 지키기 위한 처리다.
        /// </summary>
        private bool BeamCrossesPlayerBody(Vector3 origin, Vector3 direction, float beamLength, out float hitDistance)
        {
            hitDistance = 0f;
            if (_playerController == null || !_playerController.enabled) return false;

            // 높이 확인: 빔 높이가 발끝~머리 사이에 있어야 한다. 발끝은 스킨 두께만큼 지면에서 떠 있으니 그만큼 내려 잡는다.
            Bounds body = _playerController.bounds;
            float feet = body.min.y - _playerController.skinWidth;
            if (origin.y < feet || origin.y > body.max.y) return false;

            // 위에서 내려다본 평면(XZ)에서 선분과 원의 교차 판정.
            float radius = body.extents.x;
            Vector2 o = new Vector2(origin.x, origin.z);
            Vector2 d = new Vector2(direction.x, direction.z).normalized;
            Vector2 toCenter = new Vector2(body.center.x, body.center.z) - o;

            float along = Vector2.Dot(toCenter, d);                       // 빔을 따라 원 중심에 가장 가까워지는 거리
            float sideSqr = toCenter.sqrMagnitude - along * along;        // 그때 빔과 원 중심의 옆 거리(제곱)
            if (sideSqr > radius * radius) return false;                  // 원 옆으로 지나감

            float entry = along - Mathf.Sqrt(radius * radius - sideSqr);  // 빔이 원에 처음 들어가는 거리
            float exit = along + Mathf.Sqrt(radius * radius - sideSqr);
            if (exit < 0f || entry > beamLength) return false;            // 원이 크레모어 뒤에 있거나, 벽 너머에 있음

            hitDistance = Mathf.Max(entry, 0f);
            return true;
        }

        /// <summary>
        /// 빔 i의 방향. 크레모어가 조금 기울어져 놓여도 빔은 항상 지면과 평행하게 나간다(기획: "센서 높이에서 지면과 평행하게").
        /// </summary>
        private Vector3 GetBeamDirection(int i)
        {
            float step = beamCount > 1 ? detectionAngle / (beamCount - 1) : 0f;
            float angle = -detectionAngle * 0.5f + step * i;
            return Quaternion.AngleAxis(angle, Vector3.up) * FlatForward();
        }

        private Vector3 FlatForward()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        /// <summary>감지 대상은 플레이어뿐이다(기획). 적 AI 포함 여부는 아직 검토 중이라 넣지 않았다.</summary>
        private static bool IsDetectionTarget(Collider col)
        {
            return col.GetComponentInParent<PlayerLocomotion>() != null;
        }

        /// <summary>기폭. 지연 0초 — 걸린 그 판정 프레임에 바로 터진다.</summary>
        private void Detonate(Collider trigger)
        {
            _exploded = true;
            Debug.Log($"[ClaymoreMine] '{name}' 기폭 — 걸린 대상: {trigger.name}");

            SpawnEffects();
            ApplyExplosion(trigger);

            // 실물도 플라스틱 케이스가 완전히 부서진다. 본체는 바로 제거한다.
            Destroy(gameObject);
        }

        private void SpawnEffects()
        {
            Vector3 center = LaserOrigin.position;

            if (explosionEffect != null)
            {
                // 크레모어는 앞으로만 터지는 지뢰다. 폭발 이펙트(FX_Claymore_Explosion)는 원본 WarFX StarSmoke의
                // 프리팹 변형으로, 사방으로 뿜던 발사 모양을 "앞(로컬 Z축) 60° 납작한 원뿔"로 바꿔 둔 것이다.
                // 그래서 이펙트의 앞쪽을 크레모어 정면(수평)에 맞춰 생성한다.
                var fx = Instantiate(explosionEffect, center, Quaternion.LookRotation(FlatForward()));
                if (effectLifetime > 0f) Destroy(fx, effectLifetime);
            }

            if (explosionSound != null)
            {
                AudioSource.PlayClipAtPoint(explosionSound, center, explosionVolume);
            }

            ShakeCamera(center);
        }

        /// <summary>
        /// 1인칭 카메라를 흔든다. 새로 만들지 않고 Kinemation의 FPSCameraAnimator.PlayCameraShake를 재사용한다
        /// (총 쏠 때 화면이 튀는 것과 같은 기능). 카메라 회전은 그 컴포넌트가 매 프레임 덮어쓰기 때문에
        /// 우리가 카메라를 직접 돌리면 바로 지워진다 — 반드시 이 경로로 흔들어야 한다.
        ///
        /// 거리에 비례하게 하려고, 흔들림 데이터를 복사해서 세기(pitch/yaw/roll 범위)만 줄여서 넘긴다.
        /// 원본 에셋을 직접 고치면 프로젝트 파일이 바뀌어 버리므로 복사본을 쓴다.
        /// </summary>
        private void ShakeCamera(Vector3 center)
        {
            if (explosionShake == null) return;

            var cameraAnimator = FindAnyObjectByType<FPSCameraAnimator>();
            if (cameraAnimator == null) return;

            float distance = Vector3.Distance(cameraAnimator.transform.position, center);
            float strength = 1f - Mathf.InverseLerp(shakeFullDistance, shakeMaxDistance, distance);
            if (strength <= 0f) return;

            var shake = Instantiate(explosionShake);
            shake.pitch *= strength;
            shake.yaw *= strength;
            shake.roll *= strength;
            cameraAnimator.PlayCameraShake(shake);

            // 흔들림이 다 끝난 뒤 복사본을 치운다 (본체는 곧 사라지지만 예약된 삭제는 그대로 실행된다).
            float duration = shake.shakeCurve.GetCurveLength() / Mathf.Max(shake.playRate, 0.01f);
            Destroy(shake, duration + 1f);
        }

        /// <summary>
        /// 범위형 데미지 판정. 폭발 순간 한 번만 계산한다.
        ///
        /// 기획서는 "트리거 콜라이더 영역"으로 설명하지만, 여기선 Physics.OverlapSphere(그 순간 구 모양 영역 안의
        /// 콜라이더를 한 번에 모으는 기능)로 같은 일을 한다. 실제 트리거 콜라이더를 만들면 물리 프레임이 한 번
        /// 지나야 OnTriggerEnter가 불리는데, 본체는 폭발 즉시 사라져야 해서 즉석 조회가 더 맞다.
        ///
        /// 큰 구(파편 사거리 100m)로 후보를 모은 뒤, 대상마다 위치를 보고 둘 중 하나로 나눈다.
        /// - 전방 60° 부채꼴 안 + 100m 이내 → 파편 피해
        /// - 그 밖 + 16m 이내 → 후폭풍 피해
        /// 부채꼴 안의 대상은 파편 피해만 받는다(중복 없음).
        /// </summary>
        private void ApplyExplosion(Collider trigger)
        {
            Vector3 center = LaserOrigin.position;
            Vector3 forward = FlatForward();
            float halfAngle = detectionAngle * 0.5f;
            float searchRadius = Mathf.Max(fragmentRange, blastRadius);

            IDamageable triggerTarget = trigger != null ? trigger.GetComponentInParent<IDamageable>() : null;

            var hits = Physics.OverlapSphere(center, searchRadius, ~0, QueryTriggerInteraction.Ignore);
            var damaged = new HashSet<IDamageable>();   // 콜라이더가 여러 개 붙은 대상도 한 번만 때린다
            var pushed = new HashSet<Rigidbody>();

            foreach (var col in hits)
            {
                if (col.transform.IsChildOf(transform)) continue;

                Vector3 point = SafeClosestPoint(col, center);
                Vector3 toTarget = point - center;
                float distance = toTarget.magnitude;

                Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
                bool inFragmentCone = distance <= fragmentRange
                                      && (flat.sqrMagnitude < 0.0001f || Vector3.Angle(forward, flat) <= halfAngle);
                bool inBlast = distance <= blastRadius;
                if (!inFragmentCone && !inBlast) continue;

                if (explosionForce > 0f && col.attachedRigidbody != null && pushed.Add(col.attachedRigidbody))
                {
                    col.attachedRigidbody.AddExplosionForce(explosionForce, center, blastRadius, 0.5f);
                }

                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || damaged.Contains(target)) continue;
                if (IsBlocked(center, point, col)) continue;

                damaged.Add(target);

                float damage;
                if (target == triggerTarget) damage = LethalDamage;        // 레이저에 걸린 대상은 즉사 (기획 확정)
                else if (inFragmentCone) damage = fragmentDamage;
                else damage = blastDamage;

                if (damage > 0f) target.TakeDamage(damage);
            }
        }

        /// <summary>
        /// 콜라이더 위에서 폭발 지점에 가장 가까운 점.
        /// Physics.ClosestPoint는 볼록하지 않은 MeshCollider(벽·지형 등)에 쓰면 경고를 내므로, 그런 경우엔 바운즈 중심을 쓴다.
        /// </summary>
        private static Vector3 SafeClosestPoint(Collider col, Vector3 from)
        {
            if (col is MeshCollider mesh && !mesh.convex) return col.bounds.center;
            if (col is TerrainCollider) return col.bounds.center;
            return col.ClosestPoint(from);
        }

        /// <summary>폭발 지점과 대상 사이에 벽이 있으면 true. 대상 자기 몸은 가림막으로 치지 않는다.</summary>
        private bool IsBlocked(Vector3 center, Vector3 point, Collider target)
        {
            Vector3 delta = point - center;
            float distance = delta.magnitude;
            if (distance <= 0.01f) return false;

            var blockers = Physics.RaycastAll(center, delta / distance, distance,
                lineOfSightMask, QueryTriggerInteraction.Ignore);

            foreach (var blocker in blockers)
            {
                if (blocker.collider == target) continue;
                if (blocker.collider.transform.IsChildOf(target.transform.root)) continue;
                if (blocker.collider.transform.IsChildOf(transform)) continue;
                return true;
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = LaserOrigin.position;
            Vector3 forward = FlatForward();
            float half = detectionAngle * 0.5f;

            // 감지 부채꼴 (빨강)
            Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
            Vector3 left = Quaternion.AngleAxis(-half, Vector3.up) * forward;
            Vector3 right = Quaternion.AngleAxis(half, Vector3.up) * forward;
            Gizmos.DrawLine(origin, origin + left * detectionRadius);
            Gizmos.DrawLine(origin, origin + right * detectionRadius);
            Vector3 prev = origin + left * detectionRadius;
            for (int i = 1; i <= 20; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(-half + detectionAngle * i / 20f, Vector3.up) * forward;
                Vector3 next = origin + dir * detectionRadius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }

            // 후폭풍 반경 (주황)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            Gizmos.DrawWireSphere(origin, blastRadius);
        }
    }
}
