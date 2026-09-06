using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KillOrDead.Interaction
{
    [AddComponentMenu("KILL OR DEAD/Interaction/Player Interactor")]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private float interactDistance = 3f;

        [Tooltip("조준선 판정 두께(m). 0이면 얇은 광선. 작업대처럼 낮은 물체는 정면을 봐도 조준선이 위로 넘어가버리므로 두껍게 잡아준다.")]
        [SerializeField] private float interactRadius = 0.25f;

        [SerializeField] private LayerMask interactMask = ~0;

        private Camera _camera;
        private IInteractable _current;
        private Text _promptText;

        public bool InputEnabled { get; set; } = true;

        /// <summary>지금 조준선 안에 상호작용할 대상이 있는지. T키를 누가 가져갈지 정하는 데 쓰인다.</summary>
        public bool HasTarget => _current != null;

        private void Awake()
        {
            _camera = GetComponentInChildren<Camera>();
            BuildPromptUI();
        }

        private void BuildPromptUI()
        {
            var canvasGo = new GameObject("InteractPromptCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();

            var textGo = new GameObject("PromptText");
            textGo.transform.SetParent(canvasGo.transform, false);
            _promptText = textGo.AddComponent<Text>();
            _promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _promptText.fontSize = 28;
            _promptText.alignment = TextAnchor.MiddleCenter;
            _promptText.color = Color.white;
            _promptText.text = string.Empty;

            var rt = _promptText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.4f);
            rt.anchorMax = new Vector2(0.5f, 0.4f);
            rt.sizeDelta = new Vector2(500, 60);
            rt.anchoredPosition = Vector2.zero;
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                if (_current != null)
                {
                    _current = null;
                    _promptText.text = string.Empty;
                }

                return;
            }

            IInteractable found = FindInteractable();

            if (found != _current)
            {
                _current = found;
                _promptText.text = _current != null ? $"[T] {_current.GetPrompt()}" : string.Empty;
            }

            if (_current != null && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                _current.Interact(gameObject);
            }
        }

        /// <summary>
        /// 조준선을 두꺼운 구체로 쓸어서(SphereCast) 상호작용 대상을 찾는다.
        /// 얇은 광선을 쓰면 작업대처럼 눈높이보다 낮은 물체는 정면을 봐도 조준선이 위로 넘어가 못 잡는다.
        /// 트리거 콜라이더도 잡아서(QueryTriggerInteraction.Collide) 물체마다 넉넉한 상호작용 범위를 따로 줄 수 있게 한다.
        /// </summary>
        private IInteractable FindInteractable()
        {
            if (_camera == null) return null;

            var origin = _camera.transform.position;
            var direction = _camera.transform.forward;

            var hits = interactRadius > 0f
                ? Physics.SphereCastAll(origin, interactRadius, direction, interactDistance, interactMask,
                    QueryTriggerInteraction.Collide)
                : Physics.RaycastAll(origin, direction, interactDistance, interactMask,
                    QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                var candidate = hit.collider.GetComponentInParent<IInteractable>();
                if (candidate == null || hit.distance >= bestDistance) continue;

                // 두꺼운 판정은 벽을 통과해서도 물체를 잡아버리므로, 실제로 보이는지 따로 확인한다.
                if (!HasLineOfSight(origin, hit, candidate)) continue;

                best = candidate;
                bestDistance = hit.distance;
            }

            return best;
        }

        private bool HasLineOfSight(Vector3 origin, RaycastHit hit, IInteractable target)
        {
            // SphereCast가 시작 지점에서 겹쳐 있으면 hit.point가 0이 되므로 그냥 보인다고 본다.
            if (hit.distance <= 0f) return true;

            var toHit = hit.point - origin;
            float distance = toHit.magnitude - 0.02f;
            if (distance <= 0f) return true;

            if (!Physics.Raycast(origin, toHit.normalized, out var blocker, distance, interactMask,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            // 가려진 게 대상 자기 자신이면 보이는 것으로 취급한다.
            return ReferenceEquals(blocker.collider.GetComponentInParent<IInteractable>(), target);
        }
    }
}
