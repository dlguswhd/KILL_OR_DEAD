using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillOrDead.UI
{
    /// <summary>
    /// 무조건 한 줄로만 표시하고, 텍스트 길이에 맞춰 박스 가로폭이 가변하는 텍스트 컴포넌트.
    /// 줄바꿈 문자를 제거해 한 줄을 강제하고, 텍스트가 바뀌면 같은 프레임에 레이아웃을 강제 갱신한다.
    /// 폰트 크기는 자동으로 변하지 않는다(텍스트 길이에 따른 자동 스케일 없음). 값으로만 조절한다.
    /// 가로 크기는 ContentSizeFitter + HorizontalLayoutGroup이 폰트 크기와 텍스트 길이를 반영해 계산한다.
    /// </summary>
    [AddComponentMenu("KILL OR DEAD/UI/Single Line Auto Size Text")]
    [RequireComponent(typeof(RectTransform))]
    public class SingleLineAutoSizeText : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        [Tooltip("폰트 크기. 텍스트 길이에 따라 자동으로 변하지 않고 이 값으로만 조절된다.")]
        [SerializeField] private float fontSize = 16f;

        public TMP_Text Label
        {
            get => label;
            set { label = value; Apply(); }
        }

        /// <summary>폰트 크기. 값을 바꾸면 즉시 반영하고 박스 가로폭을 다시 계산한다.</summary>
        public float FontSize
        {
            get => fontSize;
            set { fontSize = value; Apply(); }
        }

        private void Awake()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || !isActiveAndEnabled) return;
            Apply();
        }
#endif

        /// <summary>표시할 텍스트를 설정한다. 줄바꿈 문자는 공백으로 치환되어 한 줄로만 나온다.</summary>
        public void SetText(string value)
        {
            var target = Target();
            if (target == null) return;

            target.text = Sanitize(value);
            Rebuild();
        }

        /// <summary>표시 문자열을 비운다.</summary>
        public void Clear()
        {
            SetText(string.Empty);
        }

        private void Apply()
        {
            var target = Target();
            if (target == null) return;

            target.textWrappingMode = TextWrappingModes.NoWrap;
            target.overflowMode = TextOverflowModes.Overflow;
            target.fontSize = fontSize;
            Rebuild();
        }

        private TMP_Text Target()
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            return label;
        }

        private void Rebuild()
        {
            var target = Target();
            if (target == null) return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(target.rectTransform);
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0) return value;

            return value.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        }
    }
}
