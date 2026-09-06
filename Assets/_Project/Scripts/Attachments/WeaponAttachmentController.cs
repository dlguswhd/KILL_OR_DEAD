using System.Collections.Generic;
using KINEMATION.TacticalShooterPack.Scripts.Weapon;
using KillOrDead.Combat;
using UnityEngine;

namespace KillOrDead.Attachments
{
    [AddComponentMenu("KILL OR DEAD/Attachments/Weapon Attachment Controller")]
    public class WeaponAttachmentController : MonoBehaviour
    {
        // 조준점 마커에 붙이는 이름. 디버깅할 때 하이러키에서 찾기 쉬우라고 상수로 뺐다.
        private const string AimPointMarkerName = "AttachmentAimPoint";
        private const string ReticleDotName = "AttachmentReticleDot";
        private const string EmitterName = "DeviceEmitter";
        private const string LaserBeamName = "AttachmentLaserBeam";
        private const string LaserDotName = "AttachmentLaserDot";
        private const string FlashlightName = "AttachmentFlashlight";

        [Tooltip("이 무기의 종류. 여기에 맞는 부착물만 장착된다. " +
                 "라이플에 권총 부착물을, 권총에 라이플 부착물을 다는 것을 막는 용도다.")]
        [SerializeField] private WeaponClass weaponClass = WeaponClass.Rifle;

        private readonly Dictionary<AttachmentSlotType, AttachmentSocket> _sockets = new();
        private readonly Dictionary<AttachmentSlotType, GameObject> _equippedInstances = new();
        private readonly Dictionary<AttachmentSlotType, GameObject> _equippedMounts = new();
        private readonly Dictionary<AttachmentSlotType, AttachmentDefinition> _equippedDefinitions = new();

        private TacticalShooterWeapon _weapon;
        private HitscanTacticalWeapon _hitscanWeapon;

        // 옵틱 조준선 위치에 만들어 두는 빈 트랜스폼. 옵틱을 갈아 끼울 때마다 위치만 갱신한다.
        private Transform _aimPointMarker;
        private AttachmentSlotType _aimPointOwnerSlot;
        private bool _hasAimPointOverride;

        // 렌즈 한가운데에 띄우는 빨간 점. 옵틱 밑이 아니라 무기 밑에 달아서
        // 부착물 크기 배율(scaleMultiplier)에 같이 늘어나지 않게 한다.
        private Transform _reticleDot;
        private Material _reticleMaterial;

        // 레이저 사이트. 빔과 점은 부착물이 아니라 무기 밑에 달아서
        // 부착물 크기 배율(scaleMultiplier)에 두께/크기가 딸려가지 않게 한다.
        private AttachmentDefinition _laserDefinition;
        private AttachmentSlotType _laserOwnerSlot;
        private Transform _laserEmitter;
        private LineRenderer _laserBeam;
        private Transform _laserDot;

        // 전술 후레쉬.
        private AttachmentSlotType _flashlightOwnerSlot;
        private Light _flashlight;

        public bool HasLaser => _laserBeam != null;
        public bool IsLaserOn => _laserBeam != null && _laserBeam.enabled;

        public bool HasFlashlight => _flashlight != null;
        public bool IsFlashlightOn => _flashlight != null && _flashlight.enabled;

        public void SetLaser(bool isOn)
        {
            if (_laserBeam == null) return;

            _laserBeam.enabled = isOn;
            if (_laserDot != null) _laserDot.gameObject.SetActive(false); // 켜진 프레임에 위치가 갱신된다.
        }

        /// <summary>레이저를 토글하고 켜졌는지 돌려준다. 레이저가 없으면 아무 일도 안 한다.</summary>
        public bool ToggleLaser()
        {
            if (_laserBeam == null) return false;

            SetLaser(!_laserBeam.enabled);
            return _laserBeam.enabled;
        }

        public void SetFlashlight(bool isOn)
        {
            if (_flashlight == null) return;
            _flashlight.enabled = isOn;
        }

        public bool ToggleFlashlight()
        {
            if (_flashlight == null) return false;

            _flashlight.enabled = !_flashlight.enabled;
            return _flashlight.enabled;
        }

        private void Awake()
        {
            _weapon = GetComponent<TacticalShooterWeapon>();
            _hitscanWeapon = _weapon as HitscanTacticalWeapon;

            foreach (var socket in GetComponentsInChildren<AttachmentSocket>(true))
            {
                _sockets[socket.slotType] = socket;
            }
        }

        public bool HasSocket(AttachmentSlotType slot)
        {
            return _sockets.ContainsKey(slot);
        }

        /// <summary>이 무기의 종류(라이플 / 권총).</summary>
        public WeaponClass Class => weaponClass;

        /// <summary>
        /// 이 무기에 달 수 있는 부착물인지. 무기 종류가 맞고 해당 슬롯의 소켓이 있어야 한다.
        /// 모딩 테이블이 목록을 만들 때도 이걸로 거른다.
        /// </summary>
        public bool Accepts(AttachmentDefinition definition)
        {
            if (definition == null || definition.weaponClass != weaponClass) return false;

            foreach (var slot in _sockets.Keys)
            {
                if (FitsSlot(definition, slot)) return true;
            }

            return false;
        }

        /// <summary>
        /// 이 부착물을 그 자리에 달 수 있는지.
        ///
        /// 측면 레일은 좌우 두 칸이 같은 규격이라, <see cref="AttachmentSlotType.SideRailRight"/>용 부착물은
        /// 왼쪽 레일에도 그대로 달 수 있다. (레이저와 후레쉬를 양쪽에 하나씩 달기 위한 규칙이다.)
        /// </summary>
        public static bool FitsSlot(AttachmentDefinition definition, AttachmentSlotType slot)
        {
            if (definition == null) return false;
            if (definition.slotType == slot) return true;

            return definition.slotType == AttachmentSlotType.SideRailRight
                   && slot == AttachmentSlotType.SideRailLeft;
        }

        public AttachmentDefinition GetEquipped(AttachmentSlotType slot)
        {
            return _equippedDefinitions.TryGetValue(slot, out var def) ? def : null;
        }

        /// <summary>
        /// 지금 장착된 부착물들의 반동 배수를 전부 곱한 값. x=수직, y=수평.
        /// 부착물이 없으면 (1, 1)이라 반동 계산에 아무 영향이 없다.
        /// </summary>
        public Vector2 GetRecoilMultipliers()
        {
            var result = Vector2.one;

            foreach (var definition in _equippedDefinitions.Values)
            {
                if (definition == null) continue;
                result.x *= definition.verticalRecoilMultiplier;
                result.y *= definition.horizontalRecoilMultiplier;
            }

            return result;
        }

        public void EquipAttachment(AttachmentDefinition definition)
        {
            if (definition == null) return;
            EquipAttachment(definition, definition.slotType);
        }

        /// <summary>
        /// 부착물을 지정한 자리에 단다.
        ///
        /// 측면 레일처럼 같은 규격의 자리가 여러 개일 때, 어느 칸에 달지 정해서 부르면 된다.
        /// </summary>
        public void EquipAttachment(AttachmentDefinition definition, AttachmentSlotType targetSlot)
        {
            if (definition == null) return;

            if (!FitsSlot(definition, targetSlot))
            {
                Debug.LogWarning(
                    $"WeaponAttachmentController: '{definition.name}'은(는) {definition.slotType}용이라 " +
                    $"{targetSlot} 자리에는 달 수 없다.");
                return;
            }

            // 무기 종류가 다르면 장착 자체를 막는다. 부착물 팩이 무기 종류별로 고정되어 있고
            // 크기도 달라서, 라이플에 권총용을 달거나 그 반대로 다는 것은 양방향 모두 금지다.
            if (definition.weaponClass != weaponClass)
            {
                Debug.LogWarning(
                    $"WeaponAttachmentController: '{definition.name}'은(는) {definition.weaponClass}용 부착물이라 " +
                    $"{weaponClass}인 {name}에는 달 수 없다.");
                return;
            }

            if (!_sockets.TryGetValue(targetSlot, out var socket))
            {
                Debug.LogWarning($"WeaponAttachmentController: no socket for slot {targetSlot} on {name}.");
                return;
            }

            RemoveAttachment(targetSlot);

            // 왼쪽 레일처럼 반대편에 다는 자리는 부착물을 좌우로 뒤집어야 몸통이 바깥을 향한다.
            Quaternion mountRotation = socket.MountRotation;

            // 마운트(권총 조준경 레일 등)를 먼저 깔고 그 위에 부착물을 올린다.
            GameObject mount = null;
            if (definition.mountPrefab != null)
            {
                mount = Instantiate(definition.mountPrefab, socket.transform);
                mount.transform.localPosition = mountRotation * definition.mountPositionOffset;
                mount.transform.localRotation = mountRotation * Quaternion.Euler(definition.mountEulerOffset);
                mount.transform.localScale = Vector3.one * Mathf.Max(0.01f, definition.mountScaleMultiplier);
                _equippedMounts[targetSlot] = mount;
            }

            GameObject instance = null;
            if (definition.prefab != null)
            {
                instance = Instantiate(definition.prefab, socket.transform);
                instance.transform.localPosition = mountRotation * definition.localPositionOffset;
                instance.transform.localRotation = mountRotation * Quaternion.Euler(definition.localEulerOffset);
                instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, definition.scaleMultiplier);
                _equippedInstances[targetSlot] = instance;
            }

            _equippedDefinitions[targetSlot] = definition;

            if (targetSlot == AttachmentSlotType.Muzzle && _weapon != null)
            {
                if (definition.isSuppressor) _weapon.AttachSuppressor();
                else _weapon.DetachSuppressor();
            }

            // 몸체와 유리를 각각 따로 갈아 끼운다. 몸체는 총기 팩 재질로 바꿔 아트 스타일을 맞추고,
            // 유리는 더 투명한 것으로 바꿔 조준경 안이 잘 보이게 한다.
            ReplaceMaterial(instance, definition.bodyMaterialName, definition.bodyMaterialOverride);
            ReplaceMaterial(instance, definition.lensMaterialName, definition.lensMaterialOverride);

            // 마운트도 같은 저폴리 재질을 쓰므로 같이 갈아 끼운다. 안 그러면 레일만 원본 회색으로 튄다.
            ReplaceMaterial(mount, definition.bodyMaterialName, definition.bodyMaterialOverride);

            ApplyAimPoint(definition, instance, targetSlot);
            SetupDevice(definition, instance, targetSlot);
        }

        /// <summary>
        /// 부착물 안에서 이름이 <paramref name="targetName"/>으로 시작하는 재질만 골라 갈아 끼운다.
        /// 이름으로 거르기 때문에 몸체를 바꿔도 렌즈 유리는 그대로 남는다.
        /// </summary>
        private static void ReplaceMaterial(GameObject instance, string targetName, Material replacement)
        {
            if (instance == null || replacement == null || string.IsNullOrEmpty(targetName)) return;

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].name.StartsWith(targetName)) continue;

                    materials[i] = replacement;
                    changed = true;
                }

                if (changed) renderer.sharedMaterials = materials;
            }
        }

        public void RemoveAttachment(AttachmentSlotType slot)
        {
            if (_equippedInstances.TryGetValue(slot, out var instance) && instance != null)
            {
                Destroy(instance);
            }

            // 마운트는 부착물의 받침일 뿐이라 부착물을 빼면 같이 사라진다.
            if (_equippedMounts.TryGetValue(slot, out var mount) && mount != null)
            {
                Destroy(mount);
            }

            _equippedInstances.Remove(slot);
            _equippedMounts.Remove(slot);
            _equippedDefinitions.Remove(slot);

            if (slot == AttachmentSlotType.Muzzle && _weapon != null)
            {
                _weapon.DetachSuppressor();
            }

            // 조준점을 제공하던 부착물을 뺐으면 아이언사이트로 되돌린다.
            if (_hasAimPointOverride && _aimPointOwnerSlot == slot) ClearAimPoint();

            if (_laserBeam != null && _laserOwnerSlot == slot) TearDownLaser();
            if (_flashlight != null && _flashlightOwnerSlot == slot) _flashlight = null;
        }

        private void LateUpdate()
        {
            // 총이 애니메이션으로 움직인 뒤에 빔을 갱신해야 빔이 총구에서 어긋나지 않는다.
            UpdateLaserBeam();
        }

        private void OnDestroy()
        {
            TearDownLaser();
        }

        /// <summary>
        /// 옵틱을 장착했을 때 조준 기준점을 그 옵틱의 렌즈 중심 높이로 옮긴다.
        /// 눈까지의 거리(Z)는 총 기본 조준점 그대로 두고 X/Y만 렌즈에 맞추기 때문에,
        /// 옵틱 종류가 달라져도 총이 얼굴에 파묻히지 않는다.
        /// </summary>
        private void ApplyAimPoint(AttachmentDefinition definition, GameObject instance, AttachmentSlotType targetSlot)
        {
            if (_hitscanWeapon == null) return;
            if (definition == null || !definition.providesAimPoint || instance == null) return;

            var opticalPart = FindOpticalPart(instance.transform, definition.aimPointChildName);
            Vector3 opticalCenter = GetOpticalCenter(opticalPart != null ? opticalPart : instance.transform);

            var weaponTransform = _hitscanWeapon.transform;
            Vector3 localCenter = weaponTransform.InverseTransformPoint(opticalCenter);

            var defaultAimPoint = _hitscanWeapon.DefaultAimPoint;
            float eyeZ = defaultAimPoint != null
                ? weaponTransform.InverseTransformPoint(defaultAimPoint.position).z
                : localCenter.z;

            if (_aimPointMarker == null)
            {
                _aimPointMarker = new GameObject(AimPointMarkerName).transform;
                _aimPointMarker.SetParent(weaponTransform, false);
            }

            _aimPointMarker.localRotation = Quaternion.identity;
            _aimPointMarker.localScale = Vector3.one;
            _aimPointMarker.localPosition =
                new Vector3(localCenter.x, localCenter.y, eyeZ) + definition.aimPointLocalOffset;

            _hitscanWeapon.SetAimPointOverride(_aimPointMarker);
            _hitscanWeapon.SetAimFovOverride(definition.aimFovOverride);

            UpdateReticleDot(definition, weaponTransform, localCenter);

            _aimPointOwnerSlot = targetSlot;
            _hasAimPointOverride = true;
        }

        private void ClearAimPoint()
        {
            _hasAimPointOverride = false;
            if (_reticleDot != null) _reticleDot.gameObject.SetActive(false);
            if (_hitscanWeapon == null) return;

            _hitscanWeapon.ClearAimPointOverride();
            _hitscanWeapon.SetAimFovOverride(0f);
        }

        /// <summary>
        /// 렌즈 한가운데에 빨간 점을 띄운다. 저폴리 옵틱 모델에는 조준점 자체가 없어서
        /// 이게 없으면 조준경을 들여다봐도 겨냥할 기준이 안 보인다.
        /// </summary>
        private void UpdateReticleDot(AttachmentDefinition definition, Transform weaponTransform, Vector3 localCenter)
        {
            if (!definition.showReticleDot)
            {
                if (_reticleDot != null) _reticleDot.gameObject.SetActive(false);
                return;
            }

            if (_reticleDot == null)
            {
                var dotObject = new GameObject(ReticleDotName);
                dotObject.AddComponent<MeshFilter>().sharedMesh = CreateCircleMesh(24);

                _reticleMaterial = RuntimeUnlitMaterial.CreateOpaque(definition.reticleDotColor);

                var renderer = dotObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _reticleMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                _reticleDot = dotObject.transform;
                _reticleDot.SetParent(weaponTransform, false);
            }

            if (_reticleMaterial != null)
            {
                // URP Unlit은 _BaseColor, 폴백(Sprites/Default)은 _Color를 쓴다.
                if (_reticleMaterial.HasProperty("_BaseColor")) _reticleMaterial.SetColor("_BaseColor", definition.reticleDotColor);
                if (_reticleMaterial.HasProperty("_Color")) _reticleMaterial.SetColor("_Color", definition.reticleDotColor);
            }

            _reticleDot.gameObject.SetActive(true);
            _reticleDot.localPosition = localCenter;
            // 원판은 +Z를 바라보므로 180도 돌려서 사수(−Z) 쪽을 향하게 한다.
            _reticleDot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _reticleDot.localScale = Vector3.one * Mathf.Max(0.0001f, definition.reticleDotSize);
        }

        /// <summary>
        /// 지름 1짜리 원판 메시를 만든다. 가운데 정점 하나에서 테두리로 부채꼴을 펴는 방식.
        /// 투명 텍스처를 쓰지 않으므로 불투명으로 그릴 수 있어 정렬 문제가 안 생긴다.
        /// </summary>
        private static Mesh CreateCircleMesh(int segments)
        {
            var mesh = new Mesh { name = "ReticleCircle" };

            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = (float) i / segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f);

                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ─────────────────────────── 레이저 / 전술 후레쉬 ───────────────────────────

        private void SetupDevice(AttachmentDefinition definition, GameObject instance, AttachmentSlotType targetSlot)
        {
            if (instance == null || definition.function == AttachmentFunction.None) return;

            var emitter = CreateEmitter(definition, instance);

            if (definition.function == AttachmentFunction.Laser) BuildLaser(definition, emitter, targetSlot);
            else if (definition.function == AttachmentFunction.Flashlight) BuildFlashlight(definition, emitter, targetSlot);
        }

        /// <summary>
        /// 빛이 나가는 지점. 저폴리 부착물에는 발광부 자식이 따로 없어서
        /// 메시 범위의 <b>앞쪽 정중앙</b>을 자동으로 계산해 쓴다.
        /// </summary>
        private static Transform CreateEmitter(AttachmentDefinition definition, GameObject instance)
        {
            var bounds = GetLocalMeshBounds(instance.transform);

            var emitter = new GameObject(EmitterName).transform;
            emitter.SetParent(instance.transform, false);
            emitter.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z)
                                    + definition.deviceEmitterOffset;
            emitter.localRotation = Quaternion.identity;

            return emitter;
        }

        private void BuildLaser(AttachmentDefinition definition, Transform emitter, AttachmentSlotType targetSlot)
        {
            TearDownLaser();

            _laserDefinition = definition;
            _laserOwnerSlot = targetSlot;
            _laserEmitter = emitter;

            var beamObject = new GameObject(LaserBeamName);
            beamObject.transform.SetParent(transform, false);

            _laserBeam = beamObject.AddComponent<LineRenderer>();
            _laserBeam.useWorldSpace = true;
            _laserBeam.positionCount = 2;
            _laserBeam.widthMultiplier = definition.laserBeamWidth;
            _laserBeam.numCapVertices = 0;
            _laserBeam.textureMode = LineTextureMode.Stretch;
            _laserBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _laserBeam.receiveShadows = false;
            _laserBeam.material = RuntimeUnlitMaterial.CreateAdditive(definition.laserColor);

            var dotObject = new GameObject(LaserDotName);
            dotObject.transform.SetParent(transform, false);
            dotObject.AddComponent<MeshFilter>().sharedMesh = CreateCircleMesh(16);

            var dotRenderer = dotObject.AddComponent<MeshRenderer>();
            dotRenderer.sharedMaterial = RuntimeUnlitMaterial.CreateAdditive(definition.laserColor);
            dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dotRenderer.receiveShadows = false;

            _laserDot = dotObject.transform;
            _laserDot.gameObject.SetActive(false);

            SetLaser(definition.deviceOnByDefault);
        }

        private void TearDownLaser()
        {
            if (_laserBeam != null) Destroy(_laserBeam.gameObject);
            if (_laserDot != null) Destroy(_laserDot.gameObject);

            _laserBeam = null;
            _laserDot = null;
            _laserEmitter = null;
            _laserDefinition = null;
        }

        private void BuildFlashlight(AttachmentDefinition definition, Transform emitter, AttachmentSlotType targetSlot)
        {
            _flashlightOwnerSlot = targetSlot;

            var lightObject = new GameObject(FlashlightName);
            lightObject.transform.SetParent(emitter, false);

            _flashlight = lightObject.AddComponent<Light>();
            _flashlight.type = LightType.Spot;
            _flashlight.color = definition.lightColor;
            _flashlight.intensity = definition.lightIntensity;
            _flashlight.range = definition.lightRange;
            _flashlight.spotAngle = definition.lightSpotAngle;
            _flashlight.innerSpotAngle = definition.lightSpotAngle * 0.45f;
            _flashlight.shadows = LightShadows.None;
            _flashlight.enabled = definition.deviceOnByDefault;
        }

        private void UpdateLaserBeam()
        {
            if (_laserBeam == null || _laserEmitter == null || _laserDefinition == null) return;

            if (!_laserBeam.enabled)
            {
                if (_laserDot != null && _laserDot.gameObject.activeSelf) _laserDot.gameObject.SetActive(false);
                return;
            }

            Vector3 origin = _laserEmitter.position;
            float maxDistance = _laserDefinition.laserMaxDistance;

            // 레이저는 부착물이 향한 방향으로 곧게 나간다. 카메라 조준점 쪽으로 꺾으면
            // 뛸 때처럼 총이 크게 흔들릴 때 빔이 총과 따로 노는 게 눈에 보인다.
            Vector3 rayDirection = _laserEmitter.forward;

            bool hasHit = Physics.Raycast(origin, rayDirection, out var hit, maxDistance, ~0,
                QueryTriggerInteraction.Ignore);

            Vector3 endPoint = hasHit ? hit.point : origin + rayDirection * maxDistance;

            _laserBeam.widthMultiplier = _laserDefinition.laserBeamWidth;
            _laserBeam.SetPosition(0, origin);
            _laserBeam.SetPosition(1, endPoint);

            if (_laserDot == null) return;

            _laserDot.gameObject.SetActive(hasHit);
            if (!hasHit) return;

            // 표면에 살짝 띄워야 Z-fighting으로 점이 깜빡이지 않는다.
            _laserDot.position = hit.point + hit.normal * 0.003f;
            _laserDot.rotation = Quaternion.LookRotation(hit.normal);
            _laserDot.localScale = Vector3.one * _laserDefinition.laserDotSize;
        }

        /// <summary>메시 범위를 root 로컬 좌표계로 환산한다.</summary>
        private static Bounds GetLocalMeshBounds(Transform root)
        {
            var bounds = new Bounds();
            bool initialized = false;

            foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null) continue;

                var meshBounds = meshFilter.sharedMesh.bounds;
                var matrix = root.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                        (i & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                        (i & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);

                    var point = matrix.MultiplyPoint3x4(corner);
                    if (!initialized)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return bounds;
        }

        /// <summary>렌즈처럼 조준선 높이를 알려주는 자식을 찾는다. 이름이 지정 안 되어 있으면 "Lens"로 자동 탐색.</summary>
        private static Transform FindOpticalPart(Transform root, string childName)
        {
            bool useExplicitName = !string.IsNullOrEmpty(childName);

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (useExplicitName)
                {
                    if (child.name == childName) return child;
                }
                else if (child.name.IndexOf("Lens", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// 메시 바운즈의 중심을 월드 좌표로 환산한다.
        /// Renderer.bounds는 월드 축 정렬 박스라 총이 기울어져 있으면 부정확하므로 직접 변환한다.
        /// </summary>
        private static Vector3 GetOpticalCenter(Transform part)
        {
            var meshFilter = part.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                return part.TransformPoint(meshFilter.sharedMesh.bounds.center);
            }

            return part.position;
        }
    }
}
