using UnityEngine;
using UnityEngine.UI;

namespace KillOrDead.UI
{
    /// <summary>
    /// middlePoint에서 각 TextArea의 정중앙까지 눈에 보이는 선을 잇는다.
    /// TextArea는 레이아웃(ContentSizeFitter)에 따라 크기가 바뀌므로 매 프레임 다시 계산한다.
    /// 선은 middlePoint의 자식으로 1회 생성해 재사용하며, middlePoint가 루트의 첫 자식이라
    /// Tap/Hold UI 뒤에 그려진다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/UI/Middle Point Line Connector")]
    [RequireComponent(typeof(RectTransform))]
    public class MiddlePointLineConnector : MonoBehaviour
    {
        [Header("연결 대상")]
        [Tooltip("선이 시작되는 중심점 RectTransform.")]
        [SerializeField]
        private RectTransform middlePoint;

        [Tooltip("탭 TextArea. 이 사각형의 정중앙까지 선을 잇는다.")]
        [SerializeField]
        private RectTransform tapTextArea;

        [Tooltip("홀드 TextArea. 이 사각형의 정중앙까지 선을 잇는다.")]
        [SerializeField]
        private RectTransform holdTextArea;

        [Header("선 표시")]
        [Tooltip("선 두께(px).")]
        [SerializeField]
        private float thickness = 2f;

        [Tooltip("선 색.")]
        [SerializeField]
        private Color lineColor = Color.white;

        // 런타임에 생성한 선 Image. middlePoint의 자식이며 1회만 만든다.
        private Image _tapLine;
        private Image _holdLine;

        // 1x1 흰색 스프라이트. Image로 선을 그리기 위한 공용 스프라이트.
        private static Sprite _whiteSprite;

        private void Awake()
        {
            if (middlePoint == null)
            {
                Debug.LogWarning("[MiddlePointLineConnector] middlePoint가 지정되지 않았습니다.", this);
                return;
            }

            _tapLine = CreateLine("TapLine");
            _holdLine = CreateLine("HoldLine");
        }

        private void LateUpdate()
        {
            if (middlePoint == null) return;

            UpdateLine(_tapLine, tapTextArea);
            UpdateLine(_holdLine, holdTextArea);
        }

        // 선 하나를 대상의 중앙에 맞춰 갱신한다. 대상이 없거나 비활성이면 숨긴다.
        private void UpdateLine(Image line, RectTransform target)
        {
            if (line == null) return;

            bool visible = target != null && target.gameObject.activeInHierarchy;
            line.gameObject.SetActive(visible);
            if (!visible) return;

            // 대상 사각형의 정중앙을 월드 좌표로 구한 뒤, middlePoint 로컬 평면으로 옮긴다.
            Vector3 worldCenter = target.TransformPoint(target.rect.center);
            Vector2 end = middlePoint.InverseTransformPoint(worldCenter);

            // 시작은 middlePoint의 정중앙(피벗=로컬 원점).
            float length = end.magnitude;
            float angle = Mathf.Atan2(end.y, end.x) * Mathf.Rad2Deg;

            var rt = (RectTransform)line.transform;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(length, thickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            line.color = lineColor;
        }

        // middlePoint의 자식으로 선 Image를 만든다. 왼쪽 끝(피벗)이 중심점에 붙고 오른쪽으로 뻗는다.
        private Image CreateLine(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(middlePoint, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(0f, thickness);

            var image = go.GetComponent<Image>();
            image.sprite = WhiteSprite();
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.color = lineColor;
            return image;
        }

        private static Sprite WhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;

            _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
            _whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return _whiteSprite;
        }
    }
}
