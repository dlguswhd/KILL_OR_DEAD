using System.Collections.Generic;
using KINEMATION.TacticalShooterPack.Scripts.Player;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using KillOrDead.Player;
using UnityEngine;

namespace KillOrDead.Combat
{
    [AddComponentMenu("KILL OR DEAD/Combat/Hitscan Tactical Weapon")]
    public class HitscanTacticalWeapon : TacticalShooterWeapon
    {
        [Header("Hitscan")]
        [SerializeField] private float damage = 25f;
        [SerializeField] private float range = 200f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private ImpactEffectLibrary impactLibrary;
        [SerializeField] private float impactScale = 0.35f;

        // 기본 조준점(총 자체의 아이언사이트). 옵틱을 끼면 aimPoint가 옵틱 조준선으로 바뀌고,
        // 옵틱을 빼면 여기로 되돌린다.
        private Transform _defaultAimPoint;
        private bool _defaultAimPointCached;

        [Header("Character Animator")]
        [Tooltip("수류탄 투척 레이어가 들어있는 우리 캐릭터 컨트롤러(AC_TacFPS_Character_KOD). " +
                 "무기별 오버라이드 컨트롤러의 베이스를 런타임에 이걸로 갈아끼운다. 비우면 벤더 원본을 그대로 쓴다.")]
        [SerializeField] private RuntimeAnimatorController characterControllerBase;

        // tacWeaponSettings는 여러 무기가 공유하는 에셋이라 그대로 고치면 프로젝트 파일이 오염된다.
        // 그래서 런타임 복사본을 만들어 두고 aimFov 같은 값은 복사본에만 쓴다.
        private bool _hasRuntimeSettings;
        private float _defaultAimFov;

        private WeaponRecoil _recoil;

        // 이 무기를 들고 있는 플레이어. "지금 손에 든 무기가 나인가"를 확인하는 데 쓴다.
        private TacticalShooterPlayer _ownerPlayer;
        private GrenadeThrower _grenadeThrower;

        /// <summary>총 자체의 기본 조준점(아이언사이트).</summary>
        public Transform DefaultAimPoint
        {
            get
            {
                CacheDefaultAimPoint();
                return _defaultAimPoint;
            }
        }

        public override void Initialize(GameObject owner, Transform rightHand)
        {
            // 플레이어가 tacWeaponSettings를 캐싱하기 전에 복사본으로 바꿔둬야
            // 나중에 참조가 어긋나지 않는다.
            EnsureRuntimeSettings();
            base.Initialize(owner, rightHand);

            if (_recoil == null) _recoil = GetComponent<WeaponRecoil>();
            if (_recoil != null) _recoil.Initialize(owner);

            if (owner != null)
            {
                _ownerPlayer = owner.GetComponent<TacticalShooterPlayer>();
                _grenadeThrower = owner.GetComponent<GrenadeThrower>();
            }
        }

        public override void StartFiring()
        {
            // TSP의 구형 입력 경로는 동작 잠금을 확인하지 않고 Mouse0으로 바로 격발한다.
            // 모든 격발이 반드시 지나는 무기 단계에서 다시 막아 수류탄 자세 중 한 발도 새지 않게 한다.
            if (_grenadeThrower != null && (_grenadeThrower.IsAiming || _grenadeThrower.IsThrowing))
            {
                StopFiring();
                return;
            }

            base.StartFiring();
        }

        private void CacheDefaultAimPoint()
        {
            if (_defaultAimPointCached) return;
            _defaultAimPoint = aimPoint;
            _defaultAimPointCached = true;
        }

        private void EnsureRuntimeSettings()
        {
            if (_hasRuntimeSettings || tacWeaponSettings == null) return;

            tacWeaponSettings = Instantiate(tacWeaponSettings);
            _defaultAimFov = tacWeaponSettings.aimFov;
            _hasRuntimeSettings = true;

            RebaseCharacterController();
        }

        /// <summary>
        /// 무기마다 딸린 오버라이드 컨트롤러(OC_FP_*)의 베이스를 우리 컨트롤러로 바꾼다.
        ///
        /// 벤더 오버라이드 컨트롤러는 벤더 원본 컨트롤러를 베이스로 쓰기 때문에, 우리가 추가한
        /// 수류탄 투척 레이어가 들어있지 않다. 벤더 에셋을 고치지 않고 해결하려고
        /// <b>런타임에 같은 클립 교체 목록을 그대로 옮겨 담은 오버라이드 컨트롤러를 새로 만든다.</b>
        /// 우리 컨트롤러는 벤더 원본의 복사본이라 클립 참조가 동일해서 교체 목록이 그대로 들어맞는다.
        ///
        /// 벤더 <see cref="TacticalShooterWeapon.Draw"/>가 <c>tacWeaponSettings.characterAnimatorController</c>를
        /// 애니메이터에 넣기 때문에, 그 전에 복사본 설정의 값을 바꿔두면 자연스럽게 우리 것이 쓰인다.
        /// </summary>
        private void RebaseCharacterController()
        {
            if (characterControllerBase == null) return;

            var current = tacWeaponSettings.characterAnimatorController;
            if (current == characterControllerBase) return;

            var rebased = new AnimatorOverrideController(characterControllerBase)
            {
                name = characterControllerBase.name + "_" + name + "(Runtime)"
            };

            // 기존 오버라이드(무기별 재장전/점검 등 클립 교체)를 그대로 옮긴다.
            if (current is AnimatorOverrideController source && source.overridesCount > 0)
            {
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(source.overridesCount);
                source.GetOverrides(overrides);
                rebased.ApplyOverrides(overrides);
            }

            tacWeaponSettings.characterAnimatorController = rebased;
        }

        /// <summary>조준 기준점을 옵틱의 조준선으로 바꾼다. null을 넣으면 아이언사이트로 복귀.</summary>
        public void SetAimPointOverride(Transform overridePoint)
        {
            CacheDefaultAimPoint();
            aimPoint = overridePoint != null ? overridePoint : _defaultAimPoint;
        }

        public void ClearAimPointOverride()
        {
            SetAimPointOverride(null);
        }

        /// <summary>조준 시 화각을 바꾼다. 0 이하를 넣으면 무기 기본값으로 복귀.</summary>
        public void SetAimFovOverride(float fov)
        {
            EnsureRuntimeSettings();
            if (tacWeaponSettings == null) return;

            tacWeaponSettings.aimFov = fov > 0f ? fov : _defaultAimFov;
        }

        public override float Holster(bool playAnimation, float visibilityDelay = -1f)
        {
            // 무기를 집어넣기 시작하면 격발도 그 자리에서 멈춘다.
            //
            // F키 일반 교체는 홀스터 애니메이션이 다 끝난 뒤에야 활성 무기가 바뀐다.
            // 그래서 아래 Fire()의 "손에 든 무기인가" 검사만으로는 애니메이션이 도는 1초 남짓 동안
            // 소총이 계속 나갔다(실측 14발).
            StopFiring();
            return base.Holster(playAnimation, visibilityDelay);
        }

        protected override void Fire()
        {
            // 손에 들고 있지 않은 무기는 절대 격발되지 않는다.
            //
            // Kinemation 원본은 사격 입력을 GetPrimaryWeapon() 하나에만 보내기 때문에,
            // 소총을 연사로 누른 채 X(권총 퀵드로우)를 누르면 소총이 _isFiring = true 인 채
            // 남아서 혼자 계속 격발하고, 마우스를 떼도 StopFiring()이 권총에게 가버려 안 멈춘다.
            //
            // 밖에서 감시하는 방식(WeaponSwapFireGuard)만으로는 한 프레임이 새어나가므로,
            // 모든 발사가 반드시 지나가는 여기서 한 번 더 막는다.
            if (_ownerPlayer != null && _ownerPlayer.GetPrimaryWeapon() != this)
            {
                StopFiring();
                return;
            }

            int ammoBefore = GetActiveAmmo();
            base.Fire();

            if (GetActiveAmmo() >= ammoBefore) return;

            var cam = Camera.main;
            if (cam == null) return;

            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, range, hitMask,
                    QueryTriggerInteraction.Ignore))
            {
                SpawnImpact(hit);

                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                damageable?.TakeDamage(damage);
            }

            // 반동은 탄착 판정이 끝난 뒤에 적용한다. 먼저 적용하면 조준하던 곳이 아니라
            // 이미 밀려난 방향으로 첫 발이 나가버린다.
            if (_recoil != null) _recoil.ApplyShot();
        }

        private void SpawnImpact(RaycastHit hit)
        {
            if (impactLibrary == null) return;

            var surface = hit.collider.GetComponentInParent<SurfaceIdentifier>();
            var surfaceType = surface != null ? surface.surfaceType : SurfaceType.Concrete;

            var prefab = impactLibrary.GetPrefab(surfaceType);
            if (prefab == null) return;

            var rotation = Quaternion.LookRotation(hit.normal);

            // 먼저 월드에 그대로 만들어서 크기를 확정한 뒤에 부모를 붙인다.
            //
            // 부모부터 붙이고 localScale을 주면 맞은 물체의 배율이 그대로 곱해진다.
            // 지형 블록아웃은 늘린 큐브로 만들기 때문에(예: 바닥이 220 x 0.05 x 460배) 탄흔이
            // 수십 배로 커지고 축마다 다르게 늘어나 기괴하게 보인다.
            var instance = Instantiate(prefab, hit.point, rotation);
            instance.transform.localScale = Vector3.one * impactScale;

            // 배율이 1인 물체에만 자식으로 붙인다.
            //
            // SetParent(worldPositionStays: true)도 만능이 아니다. 부모가 축마다 다르게 늘어나 있고
            // 탄흔이 표면 법선 방향으로 회전해 있으면 월드 배율을 정확히 되돌릴 수가 없어서 찌그러진다.
            // 지형 블록아웃은 어차피 움직이지 않으니 월드에 그대로 두는 편이 정확하다.
            // 움직이는 표적(회전 표적판 등)은 배율이 1이라 여기서 걸러지지 않고 정상적으로 따라간다.
            Vector3 s = hit.collider.transform.lossyScale;
            bool unscaled = Mathf.Abs(s.x - 1f) < 0.05f
                         && Mathf.Abs(s.y - 1f) < 0.05f
                         && Mathf.Abs(s.z - 1f) < 0.05f;
            if (unscaled) instance.transform.SetParent(hit.collider.transform, true);
        }
    }
}
