using UnityEditor;
using UnityEngine;
using KillOrDead.Combat;
using System.Collections.Generic;

namespace KillOrDead.EditorTools
{
    /// <summary>
    /// 야외 사격 및 전술 훈련 시설을 씬에 짓는다. (참조: Assets/Low Poly AR Weapon Pack 3/야외사격장.jpg)
    ///
    /// 왜 스크립트로 짓는가:
    ///   오브젝트가 800개가 넘어서 손으로 배치하면 수치를 다시 못 맞춘다.
    ///   여기 상수만 바꾸고 메뉴 [KILL OR DEAD/야외 사격장 짓기]를 다시 누르면 전체가 새로 지어진다.
    ///
    /// 좌표 규칙: +Z가 사격 방향(downrange), +X가 오른쪽, +Y가 위.
    ///           사격선(firing line)이 z = FiringLineZ 이고 거리는 전부 거기서부터 잰다.
    ///
    /// 다시 지으면 기존 "OutdoorRange" 루트를 통째로 지우고 새로 만든다.
    /// 거점의 WorkTable / Player 는 건드리지 않는다. Floor 와 DirectionalLight 는 야외에 맞게 다시 잡는다.
    /// </summary>
    public static class OutdoorRangeBuilder
    {
        // ── 사격 구역 치수 ─────────────────────────────────────────────
        private const float FiringLineZ = 30f;    // 사격선. 모든 거리의 기준점
        private const int LaneCount = 10;         // 사로 수
        private const float LaneSpacing = 3f;     // 사로 간격(m)
        private const float RangeHalfWidth = 19f; // 사격 구역 반폭(측면 둑 위치)

        // 참조 도면의 거리 표기 그대로
        private static readonly float[] TargetDistances = { 10f, 25f, 100f, 200f, 300f };

        // ── 전술 훈련 코스 구역 (거점 서쪽) ────────────────────────────
        private const float CourseMinX = -76f;
        private const float CourseMaxX = -22f;
        private const float CourseMinZ = -26f;
        private const float CourseMaxZ = 36f;

        private const string RootName = "OutdoorRange";
        private const string MaterialFolder = "Assets/_Project/Materials/Range";

        private static Dictionary<string, Material> _mats;
        private static Font _font;

        [MenuItem("KILL OR DEAD/야외 사격장 짓기", false, 100)]
        public static void Build()
        {
            EnsureMaterials();
            _font = null;
            try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }

            // 이전에 지은 것과 실내 사격장을 치운다.
            RemoveByName(RootName);
            RemoveByName("ShootingRange");

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            BuildGround(root.transform);
            BuildFiringPoint(root.transform);
            BuildFiringZone(root.transform);
            BuildTacticalCourse(root.transform);
            BuildPerimeterFence(root.transform);

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);

            int count = root.GetComponentsInChildren<Transform>(true).Length;
            Debug.Log($"[야외 사격장] 완성. 오브젝트 {count}개.");
        }

        private static void RemoveByName(string name)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) Object.DestroyImmediate(go);
        }

        // ══════════════════════════════════════════════════════════════
        //  재질
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 큰 면에는 트라이플래너 셰이더를 월드 공간 모드로 쓴다.
        /// 유니티 큐브를 늘려서 쓰면 UV가 같이 늘어나 텍스처가 흉하게 뭉개지는데,
        /// 트라이플래너는 UV를 아예 안 쓰기 때문에 아무리 늘려도 결이 일정하다.
        /// (움직이지 않는 지형이라 월드 공간 투영을 써도 텍스처가 미끄러지지 않는다)
        /// </summary>
        private static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Materials", "Range");

            _mats = new Dictionary<string, Material>();

            string texDir = "Assets/_Project/Textures/Poliigon_PlasticMoldDryBlast/Poliigon_PlasticMoldDryBlast_7495_";
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Normal.png");
            var rgh = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Roughness.jpg");
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "AmbientOcclusion.jpg");
            var triplanar = Shader.Find("KOD/Triplanar Lit");
            var lit = Shader.Find("Universal Render Pipeline/Lit");

            // 이름, 색, 메탈릭최대, 러프min, 러프max, 미터당 타일수
            // 야외 대낮 기준이라 실내용보다 알베도를 높게 잡는다. 마른 흙·콘크리트는 실제로 꽤 밝다.
            MakeTri("Ground", triplanar, nrm, rgh, ao, new Color(0.235f, 0.198f, 0.146f), 0.02f, 0.62f, 0.98f, 1.2f);
            MakeTri("Gravel", triplanar, nrm, rgh, ao, new Color(0.285f, 0.268f, 0.238f), 0.02f, 0.66f, 0.98f, 3.0f);
            MakeTri("Concrete", triplanar, nrm, rgh, ao, new Color(0.375f, 0.370f, 0.352f), 0.02f, 0.55f, 0.95f, 1.6f);
            MakeTri("Berm", triplanar, nrm, rgh, ao, new Color(0.185f, 0.152f, 0.108f), 0.02f, 0.70f, 1.00f, 0.9f);
            MakeTri("Metal", triplanar, nrm, rgh, ao, new Color(0.165f, 0.170f, 0.178f), 0.85f, 0.28f, 0.66f, 5f);
            MakeTri("Wood", triplanar, nrm, rgh, ao, new Color(0.245f, 0.170f, 0.096f), 0.02f, 0.48f, 0.88f, 3.5f);
            MakeTri("Sandbag", triplanar, nrm, rgh, ao, new Color(0.320f, 0.283f, 0.196f), 0.02f, 0.68f, 0.98f, 6f);
            MakeTri("Tire", triplanar, nrm, rgh, ao, new Color(0.042f, 0.042f, 0.045f), 0.02f, 0.55f, 0.92f, 8f);
            MakeTri("Roof", triplanar, nrm, rgh, ao, new Color(0.150f, 0.155f, 0.165f), 0.75f, 0.32f, 0.70f, 4f);
            MakeTri("Barrier", triplanar, nrm, rgh, ao, new Color(0.365f, 0.358f, 0.342f), 0.10f, 0.55f, 0.95f, 2.5f);

            // 페인트/표지처럼 작고 색이 중요한 것은 그냥 URP Lit 단색으로 둔다.
            MakeLit("PaintYellow", lit, new Color(0.72f, 0.55f, 0.05f), 0.25f);
            MakeLit("PaintWhite", lit, new Color(0.78f, 0.78f, 0.75f), 0.25f);
            MakeLit("PaintRed", lit, new Color(0.52f, 0.055f, 0.045f), 0.25f);
            MakeLit("SignGreen", lit, new Color(0.045f, 0.16f, 0.075f), 0.30f);

            AssetDatabase.SaveAssets();
        }

        private static void MakeTri(string name, Shader shader, Texture2D nrm, Texture2D rgh, Texture2D ao,
            Color tint, float metallic, float roughMin, float roughMax, float tiles)
        {
            string path = $"{MaterialFolder}/M_Range_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_NormalMap", nrm);
            m.SetTexture("_RoughnessMap", rgh);
            m.SetTexture("_AOMap", ao);
            m.SetColor("_TintColor", tint);
            m.SetVector("_MetallicRemap", new Vector4(0f, metallic, 0f, 0f));
            m.SetVector("_RoughnessRemap", new Vector4(roughMin, roughMax, 0f, 0f));
            m.SetFloat("_NormalStrength", 1.6f);
            m.SetFloat("_AOStrength", 0.55f);
            m.SetFloat("_TriplanarScale", tiles);
            m.SetFloat("_BlendSharpness", 6f);
            // 지형은 움직이지 않으므로 월드 공간 투영을 쓴다. 늘린 큐브에서도 결 크기가 일정해진다.
            m.SetFloat("_TriplanarWorld", 1f);
            m.EnableKeyword("_TRIPLANAR_WORLD");
            EditorUtility.SetDirty(m);
            _mats[name] = m;
        }

        private static void MakeLit(string name, Shader shader, Color color, float smoothness)
        {
            string path = $"{MaterialFolder}/M_Range_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            _mats[name] = m;
        }

        private static Material M(string name) => _mats[name];

        // ══════════════════════════════════════════════════════════════
        //  기본 도형 헬퍼
        // ══════════════════════════════════════════════════════════════

        private static GameObject Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>중심 좌표와 실제 크기(m)로 상자를 놓는다. 콜라이더와 표면 태그가 같이 붙는다.</summary>
        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size,
            Material mat, SurfaceType surface, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.AddComponent<SurfaceIdentifier>().surfaceType = surface;
            return go;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 center, float diameter, float height,
            Material mat, SurfaceType surface, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(euler);
            // 유니티 실린더는 높이 2가 기본이라 절반으로 나눈다.
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.AddComponent<SurfaceIdentifier>().surfaceType = surface;
            return go;
        }

        /// <summary>FBX 안의 자식 하나만 꺼내 놓는다.</summary>
        private static GameObject SpawnPart(string fbxPath, string childName, Transform parent,
            Vector3 pos, float yaw, float scale = 1f)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (source == null) return null;

            Transform found = null;
            foreach (var t in source.GetComponentsInChildren<Transform>(true))
                if (t.name == childName) { found = t; break; }
            if (found == null) return null;

            var go = (GameObject)Object.Instantiate(found.gameObject, parent);
            go.name = childName;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>
        /// FBX 안에서 이름이 <paramref name="prefix"/>로 시작하는 자식을 전부 한 덩어리로 꺼낸다.
        ///
        /// 스틸 표적류는 받침 / 팔 / 표적판이 따로 나뉘어 있어서 하나만 꺼내면 앙상한 기둥만 선다.
        /// 원본의 상대 위치를 그대로 지켜서 붙여야 형태가 맞는다.
        /// 꺼낸 뒤에는 합쳐진 바운즈의 밑면 중앙을 그룹 원점으로 끌어와서,
        /// 놓을 때 바닥 높이만 신경 쓰면 되게 한다.
        /// </summary>
        private static GameObject SpawnProp(string fbxPath, string prefix, Transform parent,
            Vector3 pos, float yaw, float scale = 1f)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (source == null) return null;

            var g = new GameObject(prefix.TrimEnd('_'));
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localScale = Vector3.one * scale;

            int taken = 0;
            foreach (Transform t in source.transform)
            {
                if (!t.name.StartsWith(prefix)) continue;
                var inst = (GameObject)Object.Instantiate(t.gameObject, g.transform);
                inst.name = t.name;
                inst.transform.localPosition = t.localPosition;
                inst.transform.localRotation = t.localRotation;
                inst.transform.localScale = t.localScale;
                taken++;
            }
            if (taken == 0) { Object.DestroyImmediate(g); return null; }

            Bounds? sum = null;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                sum = sum.HasValue ? Encapsulated(sum.Value, r.bounds) : r.bounds;
            if (sum.HasValue)
            {
                Vector3 localCenter = g.transform.InverseTransformPoint(sum.Value.center);
                Vector3 localMin = g.transform.InverseTransformPoint(
                    new Vector3(sum.Value.center.x, sum.Value.min.y, sum.Value.center.z));
                Vector3 shift = new Vector3(-localCenter.x, -localMin.y, -localCenter.z);
                foreach (Transform t in g.transform) t.localPosition += shift;
            }
            return g;
        }

        private static Bounds Encapsulated(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

        private static GameObject SpawnPrefab(string prefabPath, Transform parent, Vector3 pos, float yaw,
            float scale = 1f, Material overrideMat = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (source == null) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;

            // 벤더 재질이 URP용이 아니면(Autodesk Interactive 등) 분홍색으로 깨진다.
            // 원본 에셋은 건드리지 않고 씬 인스턴스의 재질만 갈아 끼운다.
            if (overrideMat != null)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var arr = r.sharedMaterials;
                    for (int i = 0; i < arr.Length; i++) arr[i] = overrideMat;
                    r.sharedMaterials = arr;
                }
            }
            return go;
        }

        // TextMesh는 자기 +Z가 보는 사람 반대쪽을 향할 때 글자가 똑바로 읽힌다.
        // 사대(-Z 쪽)에서 읽어야 하는 표지판은 yaw = 0 이다. 180을 주면 좌우가 뒤집혀 보인다.
        private static void Label(string text, Transform parent, Vector3 pos, float size, Color color, float yaw = 0f)
        {
            var go = new GameObject("Label_" + text.Replace("\n", " "));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = size;
            tm.fontSize = 96;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            if (_font != null)
            {
                tm.font = _font;
                go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  1. 지면 / 환경
        // ══════════════════════════════════════════════════════════════

        private static void BuildGround(Transform root)
        {
            var g = Group("00_Ground", root);

            // 시설 전체를 덮는 흙바닥. 포장면보다 3cm 아래에 깔아
            // 같은 높이에서 겹쳐 깜빡이는 것(Z-fighting)을 피한다.
            Box("Ground_Dirt", g.transform,
                new Vector3(-20f, -0.03f, 160f), new Vector3(220f, 0.05f, 460f),
                M("Ground"), SurfaceType.Dirt);

            // 사격선 흰 줄
            Box("Line_Firing", g.transform,
                new Vector3(0f, 0.03f, FiringLineZ), new Vector3(LaneCount * LaneSpacing + 4f, 0.03f, 0.25f),
                M("PaintWhite"), SurfaceType.Concrete);

            // 기존 씬의 Floor를 거점 + 사대 포장면으로 재활용한다.
            // 원래 100x100짜리 정사각형이라 시설 밖으로 크게 삐져나와 있었다.
            var floor = GameObject.Find("Floor");
            if (floor != null)
            {
                floor.transform.position = new Vector3(3f, 0f, 0f);
                floor.transform.localScale = new Vector3(4.6f, 1f, 6.8f); // 46 x 68 → x -20..26, z -34..34
                var fr = floor.GetComponent<Renderer>();
                if (fr != null) fr.sharedMaterial = M("Concrete");
                var si = floor.GetComponent<SurfaceIdentifier>();
                if (si == null) si = floor.AddComponent<SurfaceIdentifier>();
                si.surfaceType = SurfaceType.Concrete;
                EditorUtility.SetDirty(floor);
            }

            SetupEnvironment();
        }

        /// <summary>햇빛과 하늘. 실내용 세팅 그대로 두면 야외가 초저녁처럼 어둡게 나온다.</summary>
        private static void SetupEnvironment()
        {
            var sun = GameObject.Find("DirectionalLight");
            Light sunLight = null;
            if (sun != null)
            {
                sunLight = sun.GetComponent<Light>();
                if (sunLight != null)
                {
                    sun.transform.rotation = Quaternion.Euler(48f, 25f, 0f);
                    sunLight.shadows = LightShadows.Soft;
                    sunLight.shadowStrength = 0.8f;
                    sunLight.intensity = 1.5f;
                    sunLight.color = new Color(1f, 0.955f, 0.89f);
                    EditorUtility.SetDirty(sun);
                }
            }

            // 낮 하늘. 기본 스카이박스는 대기가 얇게 잡혀 있어 천정이 거의 검게 나온다.
            string skyPath = MaterialFolder + "/M_Sky_Day.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1.15f);
            sky.SetColor("_SkyTint", new Color(0.52f, 0.60f, 0.72f));
            sky.SetColor("_GroundColor", new Color(0.29f, 0.26f, 0.22f));
            sky.SetFloat("_Exposure", 1.35f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sunLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.15f;

            // 300m짜리 사거리라 옅은 안개를 깔면 거리감이 생기고 먼 표적이 덜 붕 떠 보인다.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0016f;
            RenderSettings.fogColor = new Color(0.62f, 0.66f, 0.71f);

            DynamicGI.UpdateEnvironment();
        }

        // ══════════════════════════════════════════════════════════════
        //  2. 사대 (사로 + 지붕 + 사격대)
        // ══════════════════════════════════════════════════════════════

        private static void BuildFiringPoint(Transform root)
        {
            var g = Group("10_FiringPoint", root);
            string targetFbx = "Assets/Shooting Target Set/Models/ShootingTargetCollection.fbx";

            float shedBackZ = FiringLineZ - 13f;
            float shedFrontZ = FiringLineZ + 1f;
            float shedWidth = LaneCount * LaneSpacing + 4f;
            float roofY = 3.4f;

            Box("Roof", g.transform,
                new Vector3(0f, roofY, (shedBackZ + shedFrontZ) * 0.5f),
                new Vector3(shedWidth, 0.25f, shedFrontZ - shedBackZ),
                M("Roof"), SurfaceType.Metal);

            Box("Wall_Back", g.transform,
                new Vector3(0f, 1.7f, shedBackZ), new Vector3(shedWidth, 3.4f, 0.35f),
                M("Concrete"), SurfaceType.Concrete);

            for (int i = 0; i <= LaneCount; i++)
            {
                float x = (i - LaneCount * 0.5f) * LaneSpacing;
                Box($"Post_{i}", g.transform, new Vector3(x, roofY * 0.5f, shedFrontZ - 0.6f),
                    new Vector3(0.22f, roofY, 0.22f), M("Metal"), SurfaceType.Metal);
            }

            for (int lane = 0; lane < LaneCount; lane++)
            {
                float x = (lane - (LaneCount - 1) * 0.5f) * LaneSpacing;
                var laneGroup = Group($"Lane_{lane + 1:00}", g.transform);

                // 안전 칸막이 — 사로 사이를 갈라주는 벽. 참조 도면의 "안전 칸막이"에 해당한다.
                // 옆 사로가 보이긴 해야 하므로 가슴 높이로만 세운다.
                if (lane < LaneCount - 1)
                    Box($"Divider_{lane + 1}", laneGroup.transform,
                        new Vector3(x + LaneSpacing * 0.5f, 0.75f, FiringLineZ - 3f),
                        new Vector3(0.15f, 1.5f, 5.5f), M("Concrete"), SurfaceType.Concrete);

                // 사격대: 홀수 사로는 라이플 벤치, 짝수 사로는 권총 사대.
                // 권총 사대는 Table / Wall / Glass 세 조각이라 한 덩어리로 꺼내야 형태가 맞는다.
                if (lane % 2 == 0)
                    SpawnProp(targetFbx, "Bench_Rifle", laneGroup.transform,
                        new Vector3(x, 0.02f, FiringLineZ - 1.4f), 180f);
                else
                    SpawnProp(targetFbx, "Bench_Pistol", laneGroup.transform,
                        new Vector3(x, 0.02f, FiringLineZ - 1.4f), 180f);

                // 사로 번호판 — 사대 지붕 앞쪽에 매단다
                Box($"LaneSignPlate_{lane + 1}", laneGroup.transform,
                    new Vector3(x, 2.85f, FiringLineZ + 0.2f), new Vector3(0.7f, 0.5f, 0.06f),
                    M("SignGreen"), SurfaceType.Metal);
                Label($"{lane + 1}", laneGroup.transform,
                    new Vector3(x, 2.85f, FiringLineZ + 0.15f), 0.03f, new Color(0.95f, 0.92f, 0.85f));
            }

            // 장비 테이블(작업대 모델 재사용) — 사대 뒤쪽
            for (int i = 0; i < 3; i++)
                SpawnPart("Assets/Rack Table/Models/RACK TABLE.fbx", "Table_with_rack", g.transform,
                    new Vector3(-9f + i * 9f, 0f, shedBackZ + 1.6f), 0f);

            // 사대 조명
            for (int i = 0; i < 4; i++)
            {
                var lgo = new GameObject($"ShedLight_{i}");
                lgo.transform.SetParent(g.transform, false);
                lgo.transform.localPosition = new Vector3(-12f + i * 8f, roofY - 0.4f, FiringLineZ - 6f);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point;
                l.intensity = 8f;
                l.range = 14f;
                l.color = new Color(1f, 0.95f, 0.88f);
                l.shadows = LightShadows.None;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  3. 사격 구역 (표적선 / 배플 / 둑)
        // ══════════════════════════════════════════════════════════════

        private static void BuildFiringZone(Transform root)
        {
            var g = Group("20_FiringZone", root);
            string rangeFbx = "Assets/Shooting Range/Models/Shooting Range.fbx";
            string targetFbx = "Assets/Shooting Target Set/Models/ShootingTargetCollection.fbx";

            float maxDistance = TargetDistances[TargetDistances.Length - 1];

            // ── 측면 둑 ──
            foreach (int side in new[] { -1, 1 })
                Box($"Berm_Side_{(side < 0 ? "L" : "R")}", g.transform,
                    new Vector3(side * RangeHalfWidth, 1.6f, FiringLineZ + maxDistance * 0.5f + 6f),
                    new Vector3(3.5f, 3.2f, maxDistance + 24f),
                    M("Berm"), SurfaceType.Dirt);

            // ── 후면 방탄 둑 (탄을 받아내는 흙벽) ──
            float backstopZ = FiringLineZ + maxDistance + 12f;
            Box("Backstop_Berm", g.transform,
                new Vector3(0f, 3.2f, backstopZ), new Vector3(RangeHalfWidth * 2f + 4f, 6.4f, 8f),
                M("Berm"), SurfaceType.Dirt);
            var slope = Box("Backstop_Slope", g.transform,
                new Vector3(0f, 2.4f, backstopZ - 5.4f), new Vector3(RangeHalfWidth * 2f + 4f, 7.6f, 0.6f),
                M("Berm"), SurfaceType.Dirt);
            slope.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);

            // ── 안전 칸막이(오버헤드 배플) ──
            // 실제 사격장은 총구가 위로 튀어도 탄이 장외로 못 나가게 천장 배플을 건다.
            // 얇게, 멀리 떨어뜨린다. 두껍게 여러 개를 놓으면 원근 때문에 겹쳐서 하늘을 막는 벽처럼 보인다.
            float[] baffleZ = { FiringLineZ + 9f, FiringLineZ + 34f };
            for (int i = 0; i < baffleZ.Length; i++)
            {
                var bg = Group($"Baffle_{i + 1}", g.transform);
                float h = 5.2f + i * 1.4f;
                Box("Beam", bg.transform, new Vector3(0f, h, baffleZ[i]),
                    new Vector3(RangeHalfWidth * 2f - 2f, 0.5f, 0.35f), M("Concrete"), SurfaceType.Concrete);
                foreach (int side in new[] { -1, 1 })
                    Box($"Post_{(side < 0 ? "L" : "R")}", bg.transform,
                        new Vector3(side * (RangeHalfWidth - 2.5f), h * 0.5f, baffleZ[i]),
                        new Vector3(0.4f, h, 0.4f), M("Metal"), SurfaceType.Metal);
            }

            // ── 표적선 ──
            foreach (float dist in TargetDistances)
            {
                float z = FiringLineZ + dist;
                var lineGroup = Group($"TargetLine_{dist:0}m", g.transform);

                Box("Line", lineGroup.transform, new Vector3(0f, 0.03f, z),
                    new Vector3(LaneCount * LaneSpacing + 2f, 0.03f, 0.18f), M("PaintYellow"), SurfaceType.Dirt);

                foreach (int side in new[] { -1, 1 })
                {
                    var sign = Group($"Sign_{(side < 0 ? "L" : "R")}", lineGroup.transform);
                    Box("Post", sign.transform, new Vector3(side * (RangeHalfWidth - 3f), 1.0f, z),
                        new Vector3(0.12f, 2.0f, 0.12f), M("Metal"), SurfaceType.Metal);
                    Box("Plate", sign.transform, new Vector3(side * (RangeHalfWidth - 3f), 2.1f, z),
                        new Vector3(1.6f, 0.9f, 0.08f), M("SignGreen"), SurfaceType.Metal);
                    Label($"{dist:0}m", sign.transform,
                        new Vector3(side * (RangeHalfWidth - 3f), 2.1f, z - 0.08f), 0.05f, Color.white);
                }

                if (dist <= 25f)
                {
                    // 가까운 거리: 사로마다 표적지 설치대 + 종이 표적
                    for (int lane = 0; lane < LaneCount; lane++)
                    {
                        float x = (lane - (LaneCount - 1) * 0.5f) * LaneSpacing;
                        SpawnPart(rangeFbx, "SR Target Holder", lineGroup.transform, new Vector3(x, 0f, z), 180f);
                        SpawnPart(targetFbx, "PaperTarget", lineGroup.transform, new Vector3(x, 0f, z + 0.12f), 180f);
                    }
                }
                else
                {
                    // 먼 거리: 중앙 사로 몇 개에만 스틸 표적. 멀리서도 맞은 게 보이는 종류로.
                    int steelCount = dist >= 300f ? 2 : (dist >= 200f ? 3 : 5);
                    for (int i = 0; i < steelCount; i++)
                    {
                        float x = (i - (steelCount - 1) * 0.5f) * (LaneSpacing * 1.8f);
                        string part = (i % 2 == 0) ? "Steel66" : "Steel10Inch";
                        SpawnProp(targetFbx, part, lineGroup.transform, new Vector3(x, 0f, z), 180f);
                    }
                }
            }

            BuildMovingTargetSystem(g.transform, targetFbx);

            // ── 50m 스틸 표적 세트 (도면의 과녁 아이콘들) ──
            // 전부 여러 조각으로 나뉜 소품이라 SpawnProp로 통째로 꺼낸다.
            var steelLine = Group("SteelLine_50m", g.transform);
            float sz = FiringLineZ + 50f;
            SpawnProp(targetFbx, "DuelTree_", steelLine.transform, new Vector3(-13f, 0f, sz), 180f);
            SpawnProp(targetFbx, "GongTarget", steelLine.transform, new Vector3(-7f, 0f, sz), 180f);
            SpawnProp(targetFbx, "PlateRack_", steelLine.transform, new Vector3(0f, 0f, sz), 180f);
            SpawnProp(targetFbx, "TexasStar_", steelLine.transform, new Vector3(7f, 0f, sz), 180f);
            SpawnProp(targetFbx, "HostageSteel", steelLine.transform, new Vector3(13f, 0f, sz), 180f);
            SpawnProp(targetFbx, "SpinningPlateRack_", steelLine.transform, new Vector3(-20f, 0f, sz + 3f), 180f);
            SpawnProp(targetFbx, "Popper_", steelLine.transform, new Vector3(20f, 0f, sz + 3f), 180f);

            // 사대 옆 정리 도구 — 사람이 쓰는 곳처럼 보이게 하는 소품
            var tools = Group("Tools", g.transform);
            SpawnProp(targetFbx, "BrassCollector_", tools.transform, new Vector3(-16.5f, 0f, FiringLineZ - 2f), 20f);
            SpawnProp(targetFbx, "Broom", tools.transform, new Vector3(-16.0f, 0f, FiringLineZ - 3.2f), -14f);
            SpawnProp(targetFbx, "DustBin_", tools.transform, new Vector3(16.2f, 0f, FiringLineZ - 2.6f), 8f);
        }

        /// <summary>참조 도면의 "움직이는 타겟 시스템". 10m~25m 사이를 가로지르는 레일과 그 위의 표적.</summary>
        private static void BuildMovingTargetSystem(Transform parent, string targetFbx)
        {
            var g = Group("MovingTargetSystem", parent);
            float z = FiringLineZ + 17f;
            float halfW = LaneCount * LaneSpacing * 0.5f;

            Box("Rail", g.transform, new Vector3(0f, 0.9f, z),
                new Vector3(halfW * 2f, 0.12f, 0.12f), M("Metal"), SurfaceType.Metal);
            for (int i = 0; i <= 6; i++)
            {
                float x = -halfW + (halfW * 2f) * i / 6f;
                Box($"RailPost_{i}", g.transform, new Vector3(x, 0.45f, z),
                    new Vector3(0.1f, 0.9f, 0.1f), M("Metal"), SurfaceType.Metal);
            }
            for (int i = 0; i < 3; i++)
            {
                float x = -8f + i * 8f;
                var carrier = Group($"Carrier_{i + 1}", g.transform);
                Box("Bracket", carrier.transform, new Vector3(x, 1.15f, z),
                    new Vector3(0.25f, 0.45f, 0.25f), M("Metal"), SurfaceType.Metal);
                SpawnPart(targetFbx, "Popper_Target", carrier.transform, new Vector3(x, 1.4f, z), 180f);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  4. 전술 훈련 코스
        // ══════════════════════════════════════════════════════════════

        private static void BuildTacticalCourse(Transform root)
        {
            var g = Group("30_TacticalCourse", root);

            // 코스 바닥은 자갈로 깐다. 사격 구역(콘크리트 포장)과 색이 달라야 구역이 구분돼 보인다.
            Box("Pad_Gravel", g.transform,
                new Vector3((CourseMinX + CourseMaxX) * 0.5f, 0.012f, (CourseMinZ + CourseMaxZ) * 0.5f),
                new Vector3(CourseMaxX - CourseMinX, 0.03f, CourseMaxZ - CourseMinZ),
                M("Gravel"), SurfaceType.Dirt);

            BuildStationA_Cover(g.transform);
            BuildStationB_TireField(g.transform);
            BuildStationC_CqbVillage(g.transform);
            BuildStationD_Obstacles(g.transform);
            BuildStationE_DangerZone(g.transform);
            BuildCoursePath(g.transform);
        }

        // ── 구역 A: 엄폐물 사격 구간 ────────────────────────────────────
        // 콘크리트 엄폐물 뒤에 숨었다가 나와서 스틸 표적을 쏘는 구간.
        private static void BuildStationA_Cover(Transform parent)
        {
            var g = Group("A_Cover", parent);
            string barrierDir = "Assets/Abandoned World/Metal and Concrete Barrier/Prefabs/";
            string targetFbx = "Assets/Shooting Target Set/Models/ShootingTargetCollection.fbx";

            // 엄폐물을 지그재그로 놓아 한 줄로 뛰지 못하게 한다.
            for (int i = 0; i < 6; i++)
            {
                float x = -40f + i * 2.6f;
                float z = 12f + i * 3.4f;
                string which = (i % 3 == 0) ? "Concrete_Barrier_3"
                             : (i % 3 == 1) ? "Concrete_Barrier_2" : "Concrete_Barrier_1";
                SpawnPrefab(barrierDir + which + ".prefab", g.transform,
                    new Vector3(x, 0f, z), (i % 2 == 0) ? 12f : -14f, 1f, M("Barrier"));
            }

            SandbagEmplacement(g.transform, "Emplacement_1", new Vector3(-38f, 0f, 16f), 0f);
            SandbagEmplacement(g.transform, "Emplacement_2", new Vector3(-32f, 0f, 24f), -18f);
            SandbagEmplacement(g.transform, "Emplacement_3", new Vector3(-36f, 0f, 30f), 14f);

            // 표적 — 구역 안쪽 끝에 세워 엄폐물에서 노려 쏘게 한다.
            for (int i = 0; i < 4; i++)
                SpawnProp(targetFbx, "Popper_", g.transform, new Vector3(-40f + i * 4f, 0f, 34.5f), 200f + i * 7f);
            SpawnProp(targetFbx, "Steel10Inch", g.transform, new Vector3(-26.5f, 0f, 33f), 195f);

            // 뒤쪽 방탄 둑 — 이 구간 탄이 밖으로 못 나가게
            Box("Backstop", g.transform, new Vector3(-33f, 1.5f, 36.5f),
                new Vector3(20f, 3.0f, 1.6f), M("Berm"), SurfaceType.Dirt);
        }

        /// <summary>ㄷ자 모래주머니 진지. 벽돌처럼 층마다 어긋나게 쌓아야 쌓은 티가 난다.</summary>
        private static void SandbagEmplacement(Transform parent, string name, Vector3 origin, float yaw)
        {
            var g = Group(name, parent);
            g.transform.localPosition = origin;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            for (int row = 0; row < 4; row++)
            {
                float y = 0.16f + row * 0.30f;
                float off = (row % 2) * 0.34f;
                for (int i = 0; i < 6; i++)
                    Box($"Front_{row}_{i}", g.transform, new Vector3(-1.75f + i * 0.7f + off, y, 0f),
                        new Vector3(0.68f, 0.28f, 0.42f), M("Sandbag"), SurfaceType.Sand, (i * 7 + row * 13) % 9 - 4f);
            }
            foreach (int side in new[] { -1, 1 })
                for (int row = 0; row < 3; row++)
                {
                    float y = 0.16f + row * 0.30f;
                    float off = (row % 2) * 0.3f;
                    for (int i = 0; i < 3; i++)
                        Box($"Wing_{(side < 0 ? "L" : "R")}_{row}_{i}", g.transform,
                            new Vector3(side * 1.9f, y, -0.5f - i * 0.62f - off),
                            new Vector3(0.42f, 0.28f, 0.68f), M("Sandbag"), SurfaceType.Sand, (i * 11 + row * 5) % 9 - 4f);
                }
        }

        // ── 구역 B: 타이어 지대 + 모래주머니 벽 ─────────────────────────
        private static void BuildStationB_TireField(Transform parent)
        {
            var g = Group("B_TireField", parent);

            // 세워 쌓은 타이어 더미 — 엄폐물 겸 장애물
            for (int i = 0; i < 7; i++)
            {
                float x = -57f + (i % 4) * 4.2f;
                float z = 14f + (i / 4) * 6.5f + (i % 2) * 2.2f;
                var sg = Group($"TireStack_{i + 1}", g.transform);
                int high = 3 + (i % 3);
                for (int k = 0; k < high; k++)
                    Cylinder($"Tire_{k}", sg.transform, new Vector3(x, 0.13f + k * 0.26f, z),
                        1.05f, 0.26f, M("Tire"), SurfaceType.SoftBody, new Vector3(0f, (k * 37 + i * 11) % 90, 0f));
            }

            // 눕혀 놓은 타이어 — 발 넣고 건너뛰는 구간
            for (int i = 0; i < 10; i++)
                Cylinder($"TireFlat_{i}", g.transform,
                    new Vector3(-56f + (i % 2) * 1.15f, 0.13f, 28f + (i / 2) * 1.25f),
                    1.05f, 0.26f, M("Tire"), SurfaceType.SoftBody);

            // 긴 모래주머니 방벽
            for (int row = 0; row < 5; row++)
            {
                float y = 0.16f + row * 0.30f;
                float off = (row % 2) * 0.34f;
                for (int i = 0; i < 14; i++)
                    Box($"Wall_{row}_{i}", g.transform, new Vector3(-50f + i * 0.7f + off, y, 12.5f),
                        new Vector3(0.68f, 0.28f, 0.42f), M("Sandbag"), SurfaceType.Sand, (i * 7 + row * 13) % 9 - 4f);
            }

            // 목재 장애물 벽 (넘어 다니는 용도)
            for (int i = 0; i < 3; i++)
            {
                var wg = Group($"WoodWall_{i + 1}", g.transform);
                float x = -56f + i * 4.5f;
                Box("Panel", wg.transform, new Vector3(x, 0.85f, 33f),
                    new Vector3(3.0f, 1.7f, 0.18f), M("Wood"), SurfaceType.Wood);
                foreach (int s in new[] { -1, 1 })
                    Box($"Brace_{(s < 0 ? "L" : "R")}", wg.transform, new Vector3(x + s * 1.4f, 0.55f, 33.7f),
                        new Vector3(0.16f, 1.1f, 1.5f), M("Wood"), SurfaceType.Wood);
            }
        }

        // ── 구역 C: CQB(근접 전투) 모의 마을 ──────────────────────────
        // 건물 4동을 골목을 두고 배치해서 진입 · 모서리 처리 훈련이 되게 한다.
        private static void BuildStationC_CqbVillage(Transform parent)
        {
            var g = Group("C_CQB", parent);
            string barrierDir = "Assets/Abandoned World/Metal and Concrete Barrier/Prefabs/";
            string targetFbx = "Assets/Shooting Target Set/Models/ShootingTargetCollection.fbx";

            MockBuilding(g.transform, "House_1", new Vector3(-68f, 0f, 20f), 13f, 9f, 0f, true);
            MockBuilding(g.transform, "House_2", new Vector3(-68f, 0f, 6f), 10f, 8f, 0f, false);
            MockBuilding(g.transform, "House_3", new Vector3(-58f, 0f, -1f), 9f, 11f, 90f, true);
            MockBuilding(g.transform, "House_4", new Vector3(-70f, 0f, -8f), 11f, 8f, 0f, false);

            // 골목 엄폐물
            SpawnPrefab(barrierDir + "Concrete_Barrier_2.prefab", g.transform, new Vector3(-62f, 0f, 13f), 90f, 1f, M("Barrier"));
            SpawnPrefab(barrierDir + "Concrete_Barrier_1.prefab", g.transform, new Vector3(-62f, 0f, 2f), 90f, 1f, M("Barrier"));
            SpawnPrefab(barrierDir + "Metal_Barrier_1.prefab", g.transform, new Vector3(-74f, 0f, 12f), 0f, 1f, M("Barrier"));
            SpawnPrefab(barrierDir + "Metal_Barrier_1.prefab", g.transform, new Vector3(-64f, 0f, -7f), 25f, 1f, M("Barrier"));

            // 실내 표적 — 문으로 들어가면 보이도록 건물 안쪽에 둔다
            SpawnProp(targetFbx, "HostageSteel", g.transform, new Vector3(-70f, 0f, 21f), 180f);
            SpawnProp(targetFbx, "Popper_", g.transform, new Vector3(-66f, 0f, 5f), 170f);
            SpawnProp(targetFbx, "Popper_", g.transform, new Vector3(-57f, 0f, -3f), 250f);
            SpawnProp(targetFbx, "Steel10Inch", g.transform, new Vector3(-71f, 0f, -8f), 150f);
        }

        /// <summary>
        /// 벽 · 문 · 창이 있는 단층 모의 건물. 지붕은 일부러 덮지 않는다.
        /// 위에서 내려다보며 진입 동선을 확인할 수 있어야 훈련장으로 쓸모가 있다.
        /// </summary>
        private static void MockBuilding(Transform parent, string name, Vector3 origin,
            float width, float depth, float yaw, bool innerWall)
        {
            var g = Group(name, parent);
            g.transform.localPosition = origin;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            const float H = 3.0f;
            const float T = 0.28f;
            const float DoorW = 1.2f;
            float hw = width * 0.5f;
            float hd = depth * 0.5f;

            Box("Floor", g.transform, new Vector3(0f, 0.03f, 0f),
                new Vector3(width, 0.06f, depth), M("Concrete"), SurfaceType.Concrete);

            Box("Wall_Back", g.transform, new Vector3(0f, H * 0.5f, -hd),
                new Vector3(width, H, T), M("Concrete"), SurfaceType.Concrete);

            // 앞벽 — 가운데 문 구멍
            float sideW = (width - DoorW) * 0.5f;
            Box("Wall_Front_L", g.transform, new Vector3(-(DoorW * 0.5f + sideW * 0.5f), H * 0.5f, hd),
                new Vector3(sideW, H, T), M("Concrete"), SurfaceType.Concrete);
            Box("Wall_Front_R", g.transform, new Vector3(DoorW * 0.5f + sideW * 0.5f, H * 0.5f, hd),
                new Vector3(sideW, H, T), M("Concrete"), SurfaceType.Concrete);
            Box("Wall_Front_Top", g.transform, new Vector3(0f, H - 0.35f, hd),
                new Vector3(DoorW, 0.7f, T), M("Concrete"), SurfaceType.Concrete);

            // 좌우벽 — 창 구멍
            foreach (int side in new[] { -1, 1 })
            {
                string s = side < 0 ? "L" : "R";
                float winW = 1.4f;
                float segW = (depth - winW) * 0.5f;
                Box($"Wall_{s}_A", g.transform, new Vector3(side * hw, H * 0.5f, -(winW * 0.5f + segW * 0.5f)),
                    new Vector3(T, H, segW), M("Concrete"), SurfaceType.Concrete);
                Box($"Wall_{s}_B", g.transform, new Vector3(side * hw, H * 0.5f, winW * 0.5f + segW * 0.5f),
                    new Vector3(T, H, segW), M("Concrete"), SurfaceType.Concrete);
                Box($"Wall_{s}_Sill", g.transform, new Vector3(side * hw, 0.45f, 0f),
                    new Vector3(T, 0.9f, winW), M("Concrete"), SurfaceType.Concrete);
                Box($"Wall_{s}_Head", g.transform, new Vector3(side * hw, H - 0.35f, 0f),
                    new Vector3(T, 0.7f, winW), M("Concrete"), SurfaceType.Concrete);
            }

            if (innerWall)
            {
                float gap = 1.1f;
                float segD = (depth - gap) * 0.5f;
                Box("Wall_Inner_A", g.transform, new Vector3(0f, H * 0.5f, -(gap * 0.5f + segD * 0.5f)),
                    new Vector3(T, H, segD), M("Concrete"), SurfaceType.Concrete);
                Box("Wall_Inner_B", g.transform, new Vector3(0f, H * 0.5f, gap * 0.5f + segD * 0.5f),
                    new Vector3(T, H, segD), M("Concrete"), SurfaceType.Concrete);
            }
        }

        // ── 구역 D: 장애물 코스 ────────────────────────────────────────
        private static void BuildStationD_Obstacles(Transform parent)
        {
            var g = Group("D_Obstacle", parent);
            float z = -16f;

            // 넘기 벽 — 점점 높아진다
            for (int i = 0; i < 3; i++)
                Box($"VaultWall_{i + 1}", g.transform,
                    new Vector3(-68f + i * 4.2f, 0.55f + i * 0.18f, z),
                    new Vector3(2.8f, 1.1f + i * 0.36f, 0.22f), M("Wood"), SurfaceType.Wood);

            // 낮은 포복 — 가로대 밑으로 기어간다
            var crawl = Group("Crawl", g.transform);
            for (int i = 0; i < 6; i++)
                Box($"Bar_{i}", crawl.transform, new Vector3(-56f + i * 1.5f, 0.72f, z),
                    new Vector3(3.0f, 0.12f, 0.12f), M("Metal"), SurfaceType.Metal);
            foreach (int side in new[] { -1, 1 })
                Box($"Rail_{(side < 0 ? "L" : "R")}", crawl.transform,
                    new Vector3(-52.2f, 0.38f, z + side * 1.5f),
                    new Vector3(10.5f, 0.76f, 0.14f), M("Metal"), SurfaceType.Metal);

            // 균형 잡기 통나무
            Cylinder("BalanceLog", g.transform, new Vector3(-45f, 0.55f, z),
                0.36f, 8f, M("Wood"), SurfaceType.Wood, new Vector3(0f, 0f, 90f));
            foreach (int side in new[] { -1, 1 })
                Box($"LogSupport_{(side < 0 ? "L" : "R")}", g.transform,
                    new Vector3(-45f + side * 3.8f, 0.27f, z),
                    new Vector3(0.4f, 0.55f, 0.7f), M("Wood"), SurfaceType.Wood);

            // 타이어 스텝 — 지그재그로 밟고 지나간다
            for (int i = 0; i < 8; i++)
                Cylinder($"StepTire_{i}", g.transform,
                    new Vector3(-68f + i * 1.5f, 0.13f, -22f + (i % 2) * 1.3f),
                    1.05f, 0.26f, M("Tire"), SurfaceType.SoftBody);

            // 사다리 오르기 틀
            var frame = Group("ClimbFrame", g.transform);
            foreach (int side in new[] { -1, 1 })
                Box($"Post_{(side < 0 ? "L" : "R")}", frame.transform,
                    new Vector3(-50f + side * 1.5f, 1.35f, -22f),
                    new Vector3(0.2f, 2.7f, 0.2f), M("Wood"), SurfaceType.Wood);
            for (int i = 0; i < 6; i++)
                Box($"Rung_{i}", frame.transform, new Vector3(-50f, 0.45f + i * 0.42f, -22f),
                    new Vector3(3.0f, 0.1f, 0.1f), M("Wood"), SurfaceType.Wood);
            Box("Top", frame.transform, new Vector3(-50f, 2.75f, -22f),
                new Vector3(3.2f, 0.16f, 0.6f), M("Wood"), SurfaceType.Wood);
        }

        // ── 구역 E: 위험 구역 ─────────────────────────────────────────
        private static void BuildStationE_DangerZone(Transform parent)
        {
            var g = Group("E_DangerZone", parent);
            float x0 = -40f, x1 = -24f;
            float z0 = -24f, z1 = -7f;

            // 빗금은 "여기 들어가지 마라" 표시지 붉은 바닥이 아니다.
            // 줄을 얇게 하고 간격을 넓혀야 빗금으로 읽힌다. (촘촘하게 깔았더니 붉은 덩어리로 보였다)
            int stripes = 9;
            for (int i = 0; i < stripes; i++)
            {
                float t = (i + 0.5f) / stripes;
                Box($"Stripe_{i}", g.transform,
                    new Vector3(Mathf.Lerp(x0, x1, t), 0.028f, (z0 + z1) * 0.5f),
                    new Vector3(0.34f, 0.02f, (z1 - z0) * 1.05f),
                    M("PaintRed"), SurfaceType.Concrete, 24f);
            }

            string barrierDir = "Assets/Abandoned World/Metal and Concrete Barrier/Prefabs/";
            for (int i = 0; i < 6; i++)
                SpawnPrefab(barrierDir + "Metal_Barrier_2.prefab", g.transform,
                    new Vector3(x0 - 0.4f, 0f, z0 + 1f + i * 2.9f), 0f, 1f, M("Barrier"));
            for (int i = 0; i < 5; i++)
                SpawnPrefab(barrierDir + "Metal_Barrier_2.prefab", g.transform,
                    new Vector3(x0 + 1.5f + i * 3.2f, 0f, z1 + 0.4f), 90f, 1f, M("Barrier"));

            Box("SignPost", g.transform, new Vector3(x0 - 0.6f, 1.1f, z1 - 2f),
                new Vector3(0.12f, 2.2f, 0.12f), M("Metal"), SurfaceType.Metal);
            Box("SignPlate", g.transform, new Vector3(x0 - 0.6f, 2.2f, z1 - 2f),
                new Vector3(0.08f, 1.0f, 1.8f), M("PaintRed"), SurfaceType.Metal);
            Label("위험 구역\nDANGER", g.transform, new Vector3(x0 - 0.66f, 2.2f, z1 - 2f), 0.035f, Color.white, 270f);
        }

        // ── 순환 경로 ─────────────────────────────────────────────────
        // 참조 도면의 노란 화살표. 구역들을 차례로 지나가는 뱀 모양 동선이다.
        private static void BuildCoursePath(Transform parent)
        {
            var g = Group("F_Path", parent);

            Vector3[] way =
            {
                new Vector3(-24f, 0f,   2f),   // 거점 쪽 입구
                new Vector3(-33f, 0f,  10f),   // A 엄폐물 사격 구간
                new Vector3(-33f, 0f,  32f),
                new Vector3(-50f, 0f,  32f),   // B 타이어 지대
                new Vector3(-50f, 0f,  16f),
                new Vector3(-62f, 0f,  16f),   // C CQB 마을
                new Vector3(-62f, 0f,  -4f),
                new Vector3(-62f, 0f, -16f),   // D 장애물 코스
                new Vector3(-43f, 0f, -16f),
                new Vector3(-24f, 0f, -12f),   // E 위험 구역 옆을 지나 복귀
                new Vector3(-24f, 0f,   2f),
            };

            for (int i = 0; i < way.Length - 1; i++)
                PathSegment(g.transform, $"Seg_{i + 1}", way[i], way[i + 1]);
        }

        private static void PathSegment(Transform parent, string name, Vector3 a, Vector3 b)
        {
            var g = Group(name, parent);
            Vector3 mid = (a + b) * 0.5f;
            float len = Vector3.Distance(a, b);
            float yaw = Quaternion.LookRotation((b - a).normalized).eulerAngles.y;

            Box("Line", g.transform, new Vector3(mid.x, 0.022f, mid.z),
                new Vector3(0.5f, 0.02f, len), M("PaintYellow"), SurfaceType.Concrete, yaw);

            int arrows = Mathf.Max(1, Mathf.RoundToInt(len / 3f));
            for (int i = 0; i < arrows; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (i + 0.5f) / arrows);
                Arrow(g.transform, $"Arrow_{i}", new Vector3(p.x, 0.028f, p.z), yaw);
            }
        }

        private static void Arrow(Transform parent, string name, Vector3 pos, float yaw)
        {
            var g = Group(name, parent);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // V자 두 획으로 화살촉을 만든다
            Box("A", g.transform, new Vector3(0.22f, 0f, -0.22f), new Vector3(0.14f, 0.02f, 0.7f),
                M("PaintYellow"), SurfaceType.Concrete, 45f);
            Box("B", g.transform, new Vector3(-0.22f, 0f, -0.22f), new Vector3(0.14f, 0.02f, 0.7f),
                M("PaintYellow"), SurfaceType.Concrete, -45f);
        }

        // ══════════════════════════════════════════════════════════════
        //  5. 경계 펜스
        // ══════════════════════════════════════════════════════════════

        private static void BuildPerimeterFence(Transform root)
        {
            var g = Group("40_Fence", root);

            // 시설 구역(사대 + 전술 코스)만 두른다.
            // 사격 구역은 양옆이 흙둑이라 펜스 대신 둑이 경계가 된다.
            float x0 = CourseMinX - 4f;
            float x1 = 26f;
            float z0 = CourseMinZ - 8f;
            float z1 = FiringLineZ + 4f;

            FenceRun(g.transform, "Fence_S", new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), true);
            FenceRun(g.transform, "Fence_W", new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), false);
            FenceRun(g.transform, "Fence_E", new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), false);
            FenceRun(g.transform, "Fence_N_W", new Vector3(x0, 0f, z1), new Vector3(-RangeHalfWidth, 0f, z1), false);
            FenceRun(g.transform, "Fence_N_E", new Vector3(RangeHalfWidth, 0f, z1), new Vector3(x1, 0f, z1), false);
        }

        /// <summary>기둥 + 철망 패널로 펜스 한 줄을 놓는다. 가운데에 정문을 남길 수 있다.</summary>
        private static void FenceRun(Transform parent, string name, Vector3 a, Vector3 b, bool withGate)
        {
            var g = Group(name, parent);
            const float PostSpacing = 4f;
            const float H = 2.6f;

            float length = Vector3.Distance(a, b);
            if (length < 0.5f) return;
            int segments = Mathf.Max(1, Mathf.RoundToInt(length / PostSpacing));
            Vector3 dir = (b - a).normalized;
            float yaw = Quaternion.LookRotation(dir).eulerAngles.y;

            int gateStart = withGate ? segments / 2 - 1 : -99;
            int gateEnd = gateStart + 1;

            for (int i = 0; i <= segments; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / segments);
                Box($"Post_{i}", g.transform, p + new Vector3(0f, H * 0.5f, 0f),
                    new Vector3(0.14f, H, 0.14f), M("Metal"), SurfaceType.Metal, yaw);
            }
            for (int i = 0; i < segments; i++)
            {
                if (i >= gateStart && i <= gateEnd) continue;
                Vector3 p = Vector3.Lerp(a, b, (i + 0.5f) / segments);
                Box($"Panel_{i}", g.transform, p + new Vector3(0f, H * 0.5f, 0f),
                    new Vector3(0.05f, H - 0.15f, length / segments), M("Metal"), SurfaceType.Metal, yaw);
            }

            if (withGate)
            {
                Vector3 p = Vector3.Lerp(a, b, (gateStart + 1f) / segments);
                Box("Gate_Sign", g.transform, p + new Vector3(0f, 3.4f, 0f),
                    new Vector3(0.2f, 0.5f, length / segments * 2f), M("SignGreen"), SurfaceType.Metal, yaw);
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 gp = Vector3.Lerp(a, b, (gateStart + 1f + side) / segments);
                    Box($"Gate_Post_{(side < 0 ? "L" : "R")}", g.transform, gp + new Vector3(0f, 1.8f, 0f),
                        new Vector3(0.3f, 3.6f, 0.3f), M("Metal"), SurfaceType.Metal, yaw);
                }
            }
        }
    }
}
