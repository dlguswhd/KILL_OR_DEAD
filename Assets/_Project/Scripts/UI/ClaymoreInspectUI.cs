using UnityEngine;
using UnityEngine.UI;

namespace KillOrDead.UI
{
    /// <summary>
    /// 크레모어가 해체(선 절단)되거나 센서가 파괴됐을 때, 그 부위를 확대해서 보여주는 UI.
    ///
    /// - 화면 오른쪽 위 네모 칸(box)에 확대 화면을 띄운다.
    ///   전용 카메라(zoom camera)가 부위를 가까이서 찍어 RenderTexture에 그리고, 칸 안의 RawImage가 그걸 보여준다.
    /// - 화면 속 부위 위치에 작은 점(marker)을 찍고, 그 점에서 칸까지 선을 잇는다. 매 프레임 다시 계산한다.
    /// - 부위가 화면 밖이나 카메라 뒤로 가면 점·선만 숨기고 칸은 계속 보여준다.
    /// - 표시 시간이 끝나거나 부위가 사라지면(크레모어 삭제) 닫는다.
    ///
    /// 크레모어 쪽(<c>ClaymoreLaserView</c>)이 <see cref="Show"/>를 부른다. 이 UI는 Canvas(Screen Space - Overlay) 밑에 둔다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/UI/Claymore Inspect UI")]
    [RequireComponent(typeof(RectTransform))]
    // 화면 속 부위 위치는 카메라가 이번 프레임에 다 움직인 뒤에 구해야 점·선이 부위에 딱 붙는다.
    // 카메라를 마지막으로 움직이는 LeanCameraTilt(100)보다 뒤에 돌린다.
    [DefaultExecutionOrder(200)]
    public class ClaymoreInspectUI : MonoBehaviour
    {
        [Header("UI 연결")]
        [Tooltip("오른쪽 위 네모 칸. 켜고 끄는 단위이자 선이 닿는 사각형.")]
        [SerializeField] private RectTransform box;

        [Tooltip("확대 화면을 보여줄 RawImage(칸 안).")]
        [SerializeField] private RawImage zoomImage;

        [Tooltip("부위에서 칸까지 잇는 선. 왼쪽 끝(피벗)이 부위 쪽이다.")]
        [SerializeField] private Image line;

        [Tooltip("화면 속 부위 위치에 찍는 작은 점.")]
        [SerializeField] private RectTransform marker;

        [Header("확대 카메라")]
        [Tooltip("확대 카메라와 부위 사이 거리(m). 가까울수록 크게 보인다.")]
        [SerializeField, Min(0.02f)] private float viewDistance = 0.2f;

        [Tooltip("확대 카메라 시야각(도). 작을수록 크게 보인다.")]
        [SerializeField, Range(5f, 60f)] private float fieldOfView = 25f;

        [Tooltip("확대 화면 해상도(px). 칸이 정사각형이라 가로세로 같다.")]
        [SerializeField, Min(64)] private int textureSize = 512;

        [Header("표시")]
        [Tooltip("한 번 띄우면 이 시간(초) 동안 보여준다. 그 전에 부위가 사라지면 바로 닫는다.")]
        [SerializeField, Min(0.1f)] private float showDuration = 5f;

        private RectTransform _rect;
        private Camera _zoomCamera;
        private RenderTexture _texture;
        private Transform _focus;
        private float _hideTime;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            SetVisible(false);
        }

        /// <summary>
        /// 부위를 확대해서 보여준다. 이미 보여주는 중이면 새 부위로 바꾼다.
        /// focus는 확대할 지점이고, focus의 앞(파란 축, +Z)이 카메라가 바라보는 방향이다.
        /// </summary>
        public void Show(Transform focus)
        {
            if (focus == null) return;

            EnsureCamera();
            _focus = focus;
            _hideTime = Time.time + showDuration;
            SetVisible(true);
            UpdateCamera();
        }

        private void LateUpdate()
        {
            if (!box.gameObject.activeSelf) return;

            // 시간이 끝났거나 크레모어가 사라져서 부위가 없어지면 닫는다.
            if (_focus == null || Time.time >= _hideTime)
            {
                SetVisible(false);
                return;
            }

            UpdateCamera();
            UpdateLine();
        }

        // 확대 카메라를 부위 앞(focus의 뒤쪽 viewDistance 지점)에 두고 부위를 바라보게 한다.
        private void UpdateCamera()
        {
            _zoomCamera.transform.SetPositionAndRotation(
                _focus.position - _focus.forward * viewDistance,
                Quaternion.LookRotation(_focus.forward, Vector3.up));
        }

        // 화면 속 부위 위치에 점을 찍고, 그 점에서 칸 가장자리의 가장 가까운 곳까지 선을 잇는다.
        private void UpdateLine()
        {
            Camera viewCamera = Camera.main;
            Vector3 screenPoint = viewCamera != null ? viewCamera.WorldToScreenPoint(_focus.position) : Vector3.back;

            // 카메라 뒤에 있거나 화면 밖이면 점·선을 숨긴다.
            bool onScreen = screenPoint.z > 0f
                            && screenPoint.x >= 0f && screenPoint.x <= Screen.width
                            && screenPoint.y >= 0f && screenPoint.y <= Screen.height;
            marker.gameObject.SetActive(onScreen);
            line.gameObject.SetActive(onScreen);
            if (!onScreen) return;

            // Overlay 캔버스라 카메라 인자는 null. 이 UI 루트의 로컬 좌표로 바꾼다.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPoint, null, out Vector2 start);
            marker.localPosition = start;

            // 칸 사각형을 루트 로컬 좌표로 구해서, 점에서 가장 가까운 칸 위의 점을 끝점으로 쓴다.
            Vector2 boxMin = _rect.InverseTransformPoint(box.TransformPoint(box.rect.min));
            Vector2 boxMax = _rect.InverseTransformPoint(box.TransformPoint(box.rect.max));
            Vector2 end = new Vector2(Mathf.Clamp(start.x, boxMin.x, boxMax.x), Mathf.Clamp(start.y, boxMin.y, boxMax.y));

            Vector2 delta = end - start;
            var lineRect = (RectTransform)line.transform;
            lineRect.localPosition = start;
            lineRect.sizeDelta = new Vector2(delta.magnitude, lineRect.sizeDelta.y);
            lineRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        // 확대 카메라와 RenderTexture는 처음 띄울 때 한 번만 만든다. 안 보일 때는 카메라를 꺼서 렌더 비용을 없앤다.
        private void EnsureCamera()
        {
            if (_zoomCamera != null) return;

            _texture = new RenderTexture(textureSize, textureSize, 24) { name = "ClaymoreInspect", antiAliasing = 4 };

            _zoomCamera = new GameObject("ClaymoreInspectCamera").AddComponent<Camera>();
            _zoomCamera.targetTexture = _texture;
            _zoomCamera.fieldOfView = fieldOfView;
            _zoomCamera.nearClipPlane = 0.01f;
            _zoomCamera.farClipPlane = 50f;
            _zoomCamera.enabled = false;

            zoomImage.texture = _texture;
        }

        private void SetVisible(bool visible)
        {
            box.gameObject.SetActive(visible);
            line.gameObject.SetActive(visible);
            marker.gameObject.SetActive(visible);
            if (_zoomCamera != null) _zoomCamera.enabled = visible;
            if (!visible) _focus = null;
        }

        private void OnDestroy()
        {
            if (_zoomCamera != null) Destroy(_zoomCamera.gameObject);
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }
    }
}
