using KINEMATION.TacticalShooterPack.Scripts.Animation;
using KillOrDead.Attachments;
using UnityEngine;

namespace KillOrDead.Combat
{
    /// <summary>
    /// 배틀그라운드 방식 반동. 사격할 때마다 플레이어의 시점 자체를 위로 밀어올리고
    /// <b>절대 자동으로 되돌리지 않는다.</b> 되잡는 건 플레이어가 마우스를 내려서 직접 한다.
    ///
    /// 연출용 반동(총이 흔들렸다 제자리로 오는 것)은 Kinemation의 RecoilAnimation이 이미 하고 있다.
    /// 이 컴포넌트는 그것과 별개로 <b>실제 조준점</b>을 움직이는 역할이다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Combat/Weapon Recoil")]
    public class WeaponRecoil : MonoBehaviour
    {
        [SerializeField] private RecoilProfile profile;

        private TacticalProceduralAnimation _proceduralAnimation;
        private CharacterController _ownerController;
        private WeaponAttachmentController _attachments;

        // 연사 몇 발째인지. 발사가 끊기면(sprayResetTime) 0으로 돌아간다.
        private int _shotIndex;
        private float _lastShotTime = float.NegativeInfinity;
        private int _driftSign = 1;

        public RecoilProfile Profile => profile;

        /// <summary>지금까지 반동으로 밀어올린 누적 각도(도). 디버깅/테스트용.</summary>
        public float AccumulatedVertical { get; private set; }

        private void Awake()
        {
            _attachments = GetComponent<WeaponAttachmentController>();
        }

        /// <summary>
        /// 무기가 플레이어에게 장착될 때 호출된다. 반동은 무기가 아니라 <b>플레이어의 시점</b>을
        /// 움직여야 하므로 소유자 쪽 컴포넌트를 여기서 붙잡아 둔다.
        /// </summary>
        public void Initialize(GameObject owner)
        {
            if (owner == null) return;

            _proceduralAnimation = owner.GetComponent<TacticalProceduralAnimation>();
            if (_proceduralAnimation == null) _proceduralAnimation = owner.GetComponentInParent<TacticalProceduralAnimation>();

            _ownerController = owner.GetComponent<CharacterController>();
            if (_ownerController == null) _ownerController = owner.GetComponentInParent<CharacterController>();
        }

        /// <summary>실제로 총알이 나갔을 때 한 번 호출한다. 탄착 판정이 끝난 뒤에 불러야 한다.</summary>
        public void ApplyShot()
        {
            if (profile == null || _proceduralAnimation == null) return;

            // 연사가 끊겼으면 누적 카운터를 초기화한다.
            // (시야가 원위치로 돌아가는 게 아니라, 다음 연사가 다시 1발째부터 시작한다는 뜻)
            if (Time.time - _lastShotTime > profile.sprayResetTime)
            {
                _shotIndex = 0;
                _driftSign = Random.value < 0.5f ? -1 : 1;
            }
            else if (profile.driftFlipShots > 0 && _shotIndex > 0 && _shotIndex % profile.driftFlipShots == 0)
            {
                // 몇 발마다 좌우 쏠림 방향을 뒤집어서 반동 패턴이 지그재그로 그려지게 한다.
                _driftSign = -_driftSign;
            }

            _lastShotTime = Time.time;

            float ramp = Mathf.Clamp01((float) _shotIndex / profile.rampShots);
            float situational = GetSituationalMultiplier();
            var attachmentMultiplier = _attachments != null ? _attachments.GetRecoilMultipliers() : Vector2.one;

            float vertical = profile.verticalPerShot * profile.verticalRamp.Evaluate(ramp)
                                                     * situational * attachmentMultiplier.x;

            float horizontal = profile.horizontalPerShot * profile.horizontalRamp.Evaluate(ramp)
                                                         * situational * attachmentMultiplier.y;

            // 완전 무작위와 "정해진 쏠림 방향" 사이를 horizontalDriftBias로 섞는다.
            float drift = Mathf.Lerp(Random.Range(-1f, 1f), _driftSign, profile.horizontalDriftBias);

            // pitchInput은 음수가 위쪽이다 (FPSCameraAnimator가 Quaternion.Euler(lookInput.y, ...)로 쓰기 때문).
            // 그래서 총구를 올리려면 빼야 한다.
            _proceduralAnimation.pitchInput = Mathf.Clamp(_proceduralAnimation.pitchInput - vertical, -90f, 90f);
            _proceduralAnimation.yawInput = Mathf.Clamp(_proceduralAnimation.yawInput + horizontal * drift, -90f, 90f);

            AccumulatedVertical += vertical;
            _shotIndex++;
        }

        private float GetSituationalMultiplier()
        {
            float multiplier = 1f;

            // 조준 중이면 반동이 줄어든다. aimingWeight가 0~1이라 배수를 부드럽게 섞을 수 있다.
            multiplier *= Mathf.Lerp(1f, profile.aimMultiplier, _proceduralAnimation.aimingWeight);

            if (_ownerController != null && !_ownerController.isGrounded)
            {
                multiplier *= profile.airborneMultiplier;
            }
            else if (_proceduralAnimation.moveInput.sqrMagnitude > 0.01f)
            {
                multiplier *= profile.movingMultiplier;
            }

            return multiplier;
        }
    }
}
