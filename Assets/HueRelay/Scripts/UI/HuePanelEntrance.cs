using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    // Matches the result card: 420 ms cubic ease-out, 18 px rise, 0.97 -> 1 scale.
    // Reuses the authored card/backdrop and allocates no per-frame animation objects.
    internal sealed class HuePanelEntrance
    {
        internal const float Duration = .42f;
        private const float Rise = 18, InitialScale = .97f;
        public static float Evaluate(float elapsed)
            => 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / Duration), 3);
        public static void Pose(RectTransform card, Vector2 position, float scale, float easing)
        {
            card.anchoredPosition = position - Vector2.up * ((1 - easing) * Rise * scale);
            card.localScale = Vector3.one * (scale * Mathf.Lerp(InitialScale, 1, easing));
        }
        private RectTransform card;
        private CanvasGroup group;
        private Image backdrop;
        private float started, scale, alpha;
        private bool interactable;
        public bool IsActive { get; private set; }
        public bool IsAnimating { get; private set; }
        public Vector2 RestPosition { get; private set; }

        public void Begin(RectTransform target, Image dimmer, float now)
        {
            float initialShade = dimmer && HueModalBackdrop.Contains(dimmer) ? dimmer.color.a : 0;
            Reset();
            card = target; backdrop = dimmer;
            if (!card) return;
            RestPosition = card.anchoredPosition;
            scale = card.localScale.x;
            group = card.GetComponent<CanvasGroup>();
            if (!group) group = card.gameObject.AddComponent<CanvasGroup>();
            alpha = group.alpha; interactable = group.interactable;
            HueModalBackdrop.Show(backdrop, initialShade);
            started = now;
            IsActive = IsAnimating = true;
            Apply(now);
        }

        public void SetLayout(Vector2 position, float fitScale, float now)
        {
            if (!IsAnimating && RestPosition == position && scale == fitScale) return;
            RestPosition = position; scale = fitScale;
            Apply(now);
        }

        public void Apply(float now)
        {
            if (!IsActive || !card) return;
            float t = Mathf.Clamp01((now - started) / Duration);
            float easing = Evaluate(now - started);
            group.alpha = alpha * easing;
            group.interactable = interactable && easing > .9f;
            Pose(card, RestPosition, scale, easing);
            HueModalBackdrop.Apply(backdrop, easing);
            IsAnimating = t < 1;
        }

        public void Reset()
        {
            if (IsActive)
            {
                if (card) { card.anchoredPosition = RestPosition; card.localScale = Vector3.one * scale; }
                if (group) { group.alpha = alpha; group.interactable = interactable; }
                HueModalBackdrop.Hide(backdrop);
            }
            IsActive = IsAnimating = false;
        }
    }
}
