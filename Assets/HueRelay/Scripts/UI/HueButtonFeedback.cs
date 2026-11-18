using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>复刻顶部圆形按钮的 CSS 按压曲线，整个按钮及其图标共同缩放。</summary>
    public sealed class HueButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Range(.5f, 1)] public float pressedScale = .9f;
        [Min(.01f)] public float duration = .25f;
        private float started = -100;
        private float from = 1;
        private float target = 1;
        private Button button;
        private CanvasGroup opacity;
        private bool? lastInteractable;

        private void OnEnable()
        {
            button = GetComponent<Button>();
            opacity = GetComponent<CanvasGroup>();
            lastInteractable = null;
        }

        /// <summary>开始按压反馈，不触发业务逻辑。</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && !button.IsInteractable()) return;
            AnimateTo(pressedScale);
        }
        /// <summary>松开后按原版曲线恢复。</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            AnimateTo(1);
        }
        /// <summary>指针移出时恢复正常尺寸。</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateTo(1);
        }

        private void AnimateTo(float value)
        {
            from = transform.localScale.x;
            target = value;
            started = Time.unscaledTime;
        }

        private void Update()
        {
            bool interactable = button == null || button.IsInteractable();
            if (opacity != null && lastInteractable != interactable)
            {
                opacity.alpha = interactable ? 1 : .4f;
                lastInteractable = interactable;
            }
            float t = (Time.unscaledTime - started) / duration;
            if (t > 1 && transform.localScale.x == target)
                return;
            float e = HueGame.Bezier(Mathf.Clamp01(t), .3f, 1.6f, .5f, 1);
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(from, target, e);
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
        }
    }
}
