using KINEMATION.TacticalShooterPack.Scripts.Player;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using UnityEngine;

namespace KillOrDead.Player
{
    /// <summary>
    /// 무기를 바꾸는 순간 손에서 내려간 무기의 격발을 멈춘다.
    ///
    /// 왜 필요한가:
    ///   Kinemation 원본은 사격 입력을 <c>GetPrimaryWeapon()</c> 하나에만 전달한다.
    ///   그래서 AK105를 연사로 누르고 있는 상태에서 X(권총 퀵드로우)를 누르면
    ///     1. AK105는 <c>_isFiring = true</c> 인 채로 남아 스스로 계속 격발하고
    ///     2. 마우스를 떼도 <c>StopFiring()</c> 이 새로 든 권총에게 가버려서
    ///        AK105는 탄창이 빌 때까지 영영 안 멈춘다.
    ///
    ///   F키 일반 무기 교체도 같은 구조라 같은 문제가 생긴다.
    ///
    /// 왜 이렇게 고쳤는가:
    ///   원본 스크립트(TacticalShooterPlayer)는 수정하지 않는 것이 프로젝트 규칙이고,
    ///   <c>OnQuickPistolDraw()</c> 가 virtual 이 아니라 상속으로 가로챌 수도 없다.
    ///   그래서 들고 있는 무기가 바뀌는 순간을 밖에서 감시해서 직전 무기를 멈춘다.
    ///
    ///   <c>LateUpdate</c> 에서 보는 이유: 무기 교체는 입력 처리(Update 단계)에서 일어나므로,
    ///   모든 Update 가 끝난 뒤에 확인해야 교체된 그 프레임 안에 바로 멈출 수 있다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/Player/Weapon Swap Fire Guard")]
    [RequireComponent(typeof(TacticalShooterPlayer))]
    public class WeaponSwapFireGuard : MonoBehaviour
    {
        private TacticalShooterPlayer _player;
        private TacticalShooterWeapon _lastPrimary;

        private void Awake()
        {
            _player = GetComponent<TacticalShooterPlayer>();
        }

        private void LateUpdate()
        {
            if (_player == null) return;

            var primary = _player.GetPrimaryWeapon();
            if (primary == _lastPrimary) return;

            // 손에서 내려간 무기가 아직 격발 중이면 멈춘다.
            if (_lastPrimary != null && _lastPrimary.IsFiring) _lastPrimary.StopFiring();

            _lastPrimary = primary;
        }
    }
}
