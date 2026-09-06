using UnityEngine;

namespace KillOrDead.Combat
{
    /// <summary>
    /// 던진 수류탄. 손을 떠난 순간부터 신관(퓨즈)이 타기 시작하고, 시간이 다 되면 터진다.
    ///
    /// 손에 쥐고 있는 동안(<see cref="HoldInHand"/>)은 아무 일도 하지 않는다.
    /// 실제 카운트다운은 <see cref="Release"/>가 불린 뒤부터다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Combat/Grenade")]
    [RequireComponent(typeof(Rigidbody))]
    public class Grenade : MonoBehaviour
    {
        [Header("Fuse")]
        [Tooltip("손을 떠난 뒤 터질 때까지의 시간(초).")]
        [SerializeField, Min(0f)] private float fuseTime = 3.5f;

        [Header("Pin")]
        [Tooltip("안전핀과 링 메시. 핀을 뽑으면 이것들이 사라진다.")]
        [SerializeField] private GameObject[] pinParts;

        [Header("Damage")]
        [Tooltip("폭심에서의 데미지. 거리에 따라 줄어든다.")]
        [SerializeField, Min(0f)] private float maxDamage = 400f;

        [Tooltip("이 거리 안쪽은 최대 데미지를 그대로 받는다(m).")]
        [SerializeField, Min(0f)] private float innerRadius = 2f;

        [Tooltip("이 거리 밖은 데미지가 0이다(m).")]
        [SerializeField, Min(0f)] private float outerRadius = 8f;

        [SerializeField] private LayerMask damageMask = ~0;

        [Tooltip("벽 뒤에 있으면 데미지를 주지 않는다.")]
        [SerializeField] private bool requireLineOfSight = true;

        [SerializeField] private LayerMask lineOfSightMask = ~0;

        [Header("Effects")]
        [Tooltip("폭발 이펙트 프리팹. WarFX를 쓸 땐 반드시 Unlit 버전을 쓸 것(Lit은 URP에서 분홍색으로 깨진다).")]
        [SerializeField] private GameObject explosionEffect;

        [SerializeField, Min(0f)] private float effectLifetime = 5f;
        [SerializeField] private AudioClip explosionSound;
        [SerializeField, Min(0f)] private float explosionVolume = 1f;

        [Header("Physics")]
        [Tooltip("폭발이 주변 물체를 밀어내는 힘. 0이면 밀어내지 않는다.")]
        [SerializeField, Min(0f)] private float explosionForce = 500f;

        private bool _armed;
        private bool _exploded;
        private float _timer;
        private GameObject _thrower;

        /// <summary>손에 쥐고 있는 상태. 신관은 아직 타지 않는다.</summary>
        public void HoldInHand()
        {
            _armed = false;
            _timer = 0f;
        }

        /// <summary>
        /// 안전핀을 뽑는다(핀·링 메시를 감춘다).
        /// 신관은 여기서 타기 시작하지 않는다 — 실제 카운트다운은 손을 떠나는 <see cref="Release"/>부터다.
        /// </summary>
        public void PullPin()
        {
            if (pinParts == null) return;

            foreach (var part in pinParts)
            {
                if (part != null) part.SetActive(false);
            }
        }

        /// <summary>손을 떠났다. 여기서부터 신관이 탄다.</summary>
        public void Release(GameObject thrower)
        {
            _armed = true;
            _timer = 0f;
            _thrower = thrower;
        }

        private void Update()
        {
            if (!_armed || _exploded) return;

            _timer += Time.deltaTime;
            if (_timer >= fuseTime) Explode();
        }

        private void Explode()
        {
            _exploded = true;

            SpawnEffects();
            ApplyDamage();

            Destroy(gameObject);
        }

        private void SpawnEffects()
        {
            if (explosionEffect != null)
            {
                var fx = Instantiate(explosionEffect, transform.position, Quaternion.identity);
                if (effectLifetime > 0f) Destroy(fx, effectLifetime);
            }

            if (explosionSound != null)
            {
                AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
            }
        }

        private void ApplyDamage()
        {
            Vector3 center = transform.position;
            var hits = Physics.OverlapSphere(center, outerRadius, damageMask, QueryTriggerInteraction.Ignore);

            // 한 대상에 콜라이더가 여러 개 붙어 있을 수 있으니 같은 대상은 한 번만 때린다.
            var damaged = new System.Collections.Generic.HashSet<IDamageable>();

            foreach (var col in hits)
            {
                if (explosionForce > 0f && col.attachedRigidbody != null)
                {
                    col.attachedRigidbody.AddExplosionForce(explosionForce, center, outerRadius);
                }

                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || !damaged.Add(target)) continue;

                Vector3 point = col.ClosestPoint(center);
                if (requireLineOfSight && IsBlocked(center, point, col)) continue;

                target.TakeDamage(CalculateDamage(Vector3.Distance(center, point)));
            }
        }

        /// <summary>폭심에서 거리에 따라 줄어드는 데미지. innerRadius 안은 최대, outerRadius 밖은 0.</summary>
        private float CalculateDamage(float distance)
        {
            if (distance <= innerRadius) return maxDamage;
            if (distance >= outerRadius) return 0f;

            float falloff = 1f - Mathf.InverseLerp(innerRadius, outerRadius, distance);
            return maxDamage * falloff;
        }

        private bool IsBlocked(Vector3 center, Vector3 point, Collider target)
        {
            Vector3 delta = point - center;
            float distance = delta.magnitude;
            if (distance <= 0.01f) return false;

            var blockers = Physics.RaycastAll(center, delta / distance, distance,
                lineOfSightMask, QueryTriggerInteraction.Ignore);

            foreach (var blocker in blockers)
            {
                // 대상 자신은 가림막이 아니다.
                if (blocker.collider == target) continue;
                if (blocker.collider.transform.IsChildOf(target.transform.root)) continue;
                return true;
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, innerRadius);
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, outerRadius);
        }
    }
}
