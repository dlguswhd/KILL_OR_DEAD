using UnityEngine;

namespace KillOrDead.Attachments
{
    [CreateAssetMenu(fileName = "NewAttachmentDefinition", menuName = "KILL OR DEAD/Attachments/Attachment Definition")]
    public class AttachmentDefinition : ScriptableObject
    {
        [Tooltip("이 부착물이 붙는 무기 종류. 라이플용을 권총에, 권총용을 라이플에 다는 것은 양쪽 다 막힌다. " +
                 "라이플 = Low Poly AR Weapon Pack 3, 권총 = Low Poly Pistol Weapon Pack 3 의 부착물을 쓴다.")]
        public WeaponClass weaponClass = WeaponClass.Rifle;

        public AttachmentSlotType slotType;
        public string displayName = string.Empty;
        public Sprite icon;
        public GameObject prefab;

        [Header("마운트 (권총 조준경 레일 등)")]
        // 권총은 슬라이드 위가 밋밋해서 조준경을 바로 못 얹는다. 실총과 마찬가지로 마운트 레일을
        // 먼저 깔고 그 위에 조준경을 올린다. 부착물과 같은 소켓 밑에 같이 만들어지고 같이 사라진다.
        [Tooltip("부착물 밑에 받침으로 같이 붙일 부품(권총용 레일 등). 비워두면 부착물만 붙는다.")]
        public GameObject mountPrefab;

        [Tooltip("마운트의 소켓 기준 위치 보정(m).")]
        public Vector3 mountPositionOffset = Vector3.zero;

        [Tooltip("마운트의 소켓 기준 회전 보정(도).")]
        public Vector3 mountEulerOffset = Vector3.zero;

        [Tooltip("마운트 크기 배율.")]
        [Min(0.01f)] public float mountScaleMultiplier = 1f;

        [Tooltip("Only meaningful for the Muzzle slot: hooks into TacticalShooterWeapon's existing suppressed fire-sound/muzzle-flash selection.")]
        public bool isSuppressor;

        [Header("재질")]
        [Tooltip("장착할 때 부착물 몸체 재질을 이걸로 바꾼다. 비워두면 원본 저폴리 재질을 그대로 쓴다. " +
                 "렌즈/유리처럼 몸체가 아닌 재질은 바꾸지 않는다.")]
        public Material bodyMaterialOverride;

        [Tooltip("교체 대상이 되는 원본 재질 이름. 이 이름으로 시작하는 재질만 바꾼다.")]
        public string bodyMaterialName = "Low Poly Weapon";

        [Tooltip("조준경 유리 재질을 이걸로 바꾼다. 원본 유리가 너무 불투명해서 들여다봐도 잘 안 보일 때 쓴다.")]
        public Material lensMaterialOverride;

        [Tooltip("교체 대상이 되는 원본 유리 재질 이름.")]
        public string lensMaterialName = "Low Poly Lens";

        [Header("Fitting")]
        // 저폴리 부착물 팩과 Kinemation 총기는 제작 스케일이 달라서 부착물마다 개별 보정이 필요하다.
        [Tooltip("소켓에 붙일 때 적용할 크기 배율. 1이면 원본 크기 그대로.")]
        [Min(0.01f)] public float scaleMultiplier = 1f;

        [Tooltip("소켓 기준 위치 미세보정(m). 소켓의 로컬 좌표계 기준.")]
        public Vector3 localPositionOffset = Vector3.zero;

        [Tooltip("소켓 기준 회전 미세보정(도).")]
        public Vector3 localEulerOffset = Vector3.zero;

        [Header("Aim (Optic 슬롯 전용)")]
        [Tooltip("체크하면 이 부착물을 장착했을 때 조준점(ADS 기준)이 이 옵틱의 조준선으로 바뀐다.")]
        public bool providesAimPoint;

        [Tooltip("조준선 높이를 읽어올 자식 오브젝트 이름. 비워두면 이름에 'Lens'가 들어간 자식을 자동으로 찾는다.")]
        public string aimPointChildName = string.Empty;

        [Tooltip("찾아낸 렌즈 중심 기준 추가 미세보정(m). 무기 로컬 좌표계 기준(+Z가 총구 방향, +Y가 위).")]
        public Vector3 aimPointLocalOffset = Vector3.zero;

        [Tooltip("조준 시 카메라 화각(도). 0이면 무기 기본값(tacWeaponSettings.aimFov)을 그대로 쓴다. 배율 있는 스코프는 더 좁게(예: 40).")]
        [Min(0f)] public float aimFovOverride = 0f;

        [Tooltip("렌즈 한가운데에 빨간 조준점(레티클)을 그린다. 저폴리 옵틱 모델에는 조준점이 없어서 이게 없으면 겨냥할 기준이 안 보인다.")]
        public bool showReticleDot = true;

        [Tooltip("조준점 크기(m). 0.003 정도면 조준했을 때 화면에서 작고 또렷한 점으로 보인다.")]
        [Min(0.0001f)] public float reticleDotSize = 0.003f;

        public Color reticleDotColor = new Color(1f, 0.09f, 0.05f, 1f);

        [Header("장치 기능 (레이저 / 전술 후레쉬)")]
        [Tooltip("이 부착물이 켜고 끌 수 있는 장치인지. 레이저는 T, 후레쉬는 Y로 토글된다.")]
        public AttachmentFunction function = AttachmentFunction.None;

        [Tooltip("장치가 처음 장착됐을 때 켜져 있을지.")]
        public bool deviceOnByDefault;

        [Tooltip("빛이 나가는 위치 미세보정(m). 비워두면 부착물 메시의 앞쪽 정중앙에서 자동으로 나간다.")]
        public Vector3 deviceEmitterOffset = Vector3.zero;

        [Header("레이저 (function = Laser 일 때)")]
        public Color laserColor = new Color(1f, 0.06f, 0.04f, 1f);

        [Tooltip("레이저가 닿는 최대 거리(m).")]
        [Min(1f)] public float laserMaxDistance = 100f;

        [Tooltip("빛줄기 두께(m).")]
        [Min(0.0001f)] public float laserBeamWidth = 0.0035f;

        [Tooltip("닿은 자리에 찍히는 점의 크기(m).")]
        [Min(0.0001f)] public float laserDotSize = 0.016f;

        [Header("전술 후레쉬 (function = Flashlight 일 때)")]
        public Color lightColor = new Color(1f, 0.96f, 0.88f, 1f);

        [Min(0f)] public float lightIntensity = 18f;

        [Min(0.1f)] public float lightRange = 35f;

        [Tooltip("빛이 퍼지는 각도(도).")]
        [Range(5f, 120f)] public float lightSpotAngle = 34f;

        [Header("Recoil")]
        [Tooltip("이 부착물을 달았을 때 수직 반동 배수. 1이면 변화 없음, 0.85면 15% 감소. 총구 장치가 주로 여기에 영향을 준다.")]
        [Min(0f)] public float verticalRecoilMultiplier = 1f;

        [Tooltip("이 부착물을 달았을 때 수평 반동 배수. 전방 그립이 주로 여기에 영향을 준다.")]
        [Min(0f)] public float horizontalRecoilMultiplier = 1f;
    }
}
