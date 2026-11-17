using UnityEngine;
using UnityEngine.UI;

/// <summary>Responsive geometry and animation measured from the supplied playable HTML.</summary>
[ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-850)]
public sealed class PlayablePresentation : MonoBehaviour
{
    public RectTransform footer;
    public RectTransform footerTapArea;
    public RectTransform[] footerContent;
    public Vector2 footerReferenceSize = new Vector2(856.44617f, 1513.23f);
    public RectTransform loadingPanel, loadingIcon, loadingLogo, loadingTrack;
    public Text loadingText;
    public RectTransform endBrand, endTitle, endIcon, endSubtitle, endButton, endRays;
    public Text endSubtitleText, endButtonText;
    public float popDuration = .4f;
    public float rayDegreesPerSecond = -17.18873f;
    public float rayReferenceSize = 1495.5676f;

    private Vector2 lastSize;
    private float lastLoadingUnits, lastEndUnits, lastFooterUnits;
    private readonly Vector3[] corners = new Vector3[4];
    private float endStarted;
    private bool endActive, popOwnsScale;
    private Vector3 brandRestScale;
    private Quaternion raysRestRotation;

    void OnEnable() { lastSize = Vector2.zero; EnsureLayout(); }
    void LateUpdate() { EnsureLayout(); }

    void Update()
    {
        EnsureLayout();
        if (!Application.isPlaying || !endActive) return;
        float age = Time.unscaledTime - endStarted;
        if (endBrand != null && popOwnsScale)
        {
            float t = Mathf.Clamp01(age / Mathf.Max(.01f, popDuration));
            float x = t - 1;
            float scale = 1 + 2.70158f * x * x * x + 1.70158f * x * x;
            endBrand.localScale = brandRestScale * scale;
            if (t >= 1) { endBrand.localScale = brandRestScale; popOwnsScale = false; }
        }
        if (endRays != null) endRays.localRotation = raysRestRotation * Quaternion.Euler(0, 0, age * rayDegreesPerSecond);
    }

    public void ShowEndCard()
    {
        if (endActive) return;
        endActive = true;
        endStarted = Time.unscaledTime;
        if (footer != null) footer.gameObject.SetActive(false);
        if (endBrand != null)
        {
            brandRestScale = endBrand.localScale;
            endBrand.localScale = Vector3.zero;
            popOwnsScale = true;
        }
        if (endRays != null) raysRestRotation = endRays.localRotation;
    }

    public void EnsureLayout()
    {
        var canvas = loadingPanel != null ? loadingPanel.GetComponentInParent<Canvas>() : null;
        Vector2 size = Application.isPlaying ? new Vector2(Screen.width, Screen.height)
            : canvas != null ? ((RectTransform)canvas.transform).rect.size * canvas.scaleFactor : Vector2.zero;
        float loadingUnits = Units(loadingPanel), endUnits = Units(endBrand), footerUnits = Units(footer);
        if (size.x <= 0 || size.y <= 0 || (size == lastSize && loadingUnits == lastLoadingUnits && endUnits == lastEndUnits && footerUnits == lastFooterUnits)) return;
        lastSize = size;
        lastLoadingUnits = loadingUnits; lastEndUnits = endUnits; lastFooterUnits = footerUnits;
        ApplyLayout(size.x, size.y);
    }

    public void ApplyLayout(float width, float height)
    {
        float vmin = Mathf.Min(width, height) * .01f;
        float loadingUnits = Units(loadingPanel), endUnits = Units(endBrand), footerUnits = Units(footer);
        if (footer != null)
        {
            footer.anchorMin = footer.anchorMax = footer.pivot = new Vector2(.5f, 0);
            footer.anchoredPosition = Vector2.zero;
            footer.sizeDelta = footerReferenceSize;
            footer.localScale = Vector3.one * (Mathf.Min(width, height) * footerUnits / footerReferenceSize.x);
        }

        float icon = Mathf.Min(32 * vmin, 184), gap = 4 * vmin;
        float logoHeight = 7 * vmin, trackHeight = Mathf.Min(20, 3.2f * vmin);
        float textHeight = 4 * vmin * 1.3f;
        float y = (height - icon - logoHeight - trackHeight - textHeight - 3 * gap) * .5f;
        Place(loadingIcon, width * .5f, y + icon * .5f, icon, icon, loadingUnits);
        y += icon + gap;
        Place(loadingLogo, width * .5f, y + logoHeight * .5f, 56 * vmin, logoHeight, loadingUnits);
        y += logoHeight + gap;
        Place(loadingTrack, width * .5f, y + trackHeight * .5f, Mathf.Min(300, 56 * vmin), trackHeight, loadingUnits);
        y += trackHeight + gap;
        if (loadingText != null)
        {
            loadingText.fontSize = Mathf.Max(1, Mathf.RoundToInt(4 * vmin * loadingUnits));
            Place(loadingText.rectTransform, width * .5f, y + textHeight * .5f, width, textHeight, loadingUnits);
        }

        float rootScale = width / height < 1 ? width / 640 : height / 760;
        float endIconSize = Mathf.Min(.42f * width, .2f * height);
        Place(endBrand, width * .5f, height * .33f, 0, 0, endUnits);
        Place(endRays, width * .5f, height * .33f, rayReferenceSize * rootScale, rayReferenceSize * rootScale, endUnits);
        if (endTitle != null)
        {
            endTitle.anchoredPosition = new Vector2(0, endIconSize * .5f + 74 * rootScale) * endUnits;
            endTitle.sizeDelta = new Vector2(width - 60 * rootScale, 90 * rootScale) * endUnits;
            var label = endTitle.GetComponent<Text>();
            if (label != null) label.fontSize = Mathf.Max(1, Mathf.RoundToInt(62 * rootScale * endUnits));
        }
        if (endIcon != null) { endIcon.anchoredPosition = Vector2.zero; endIcon.sizeDelta = Vector2.one * endIconSize * endUnits; }
        if (endSubtitle != null)
        {
            endSubtitle.anchoredPosition = new Vector2(0, -endIconSize * .5f - 54 * rootScale) * endUnits;
            endSubtitle.sizeDelta = new Vector2(width - 60 * rootScale, 65 * rootScale) * endUnits;
        }
        if (endSubtitleText != null)
        {
            endSubtitleText.fontSize = Mathf.Max(1, Mathf.RoundToInt(32 * rootScale * endUnits));
            // The reference aligns the complete wrapped block before truncating the
            // invisible second line; native Text aligns only its visible first line.
            if (endSubtitleText.preferredWidth > endSubtitle.sizeDelta.x)
                endSubtitle.anchoredPosition += Vector2.up * (70f / 64 * endSubtitleText.fontSize * .5f);
        }
        Place(endButton, width * .5f, height * .72f, 440 * rootScale, 118 * rootScale, endUnits);
        if (endButtonText != null) endButtonText.fontSize = Mathf.Max(1, Mathf.RoundToInt(44 * rootScale * endUnits));
        if (endButton != null)
        {
            var shadow = endButton.GetComponent<Shadow>();
            if (shadow != null) shadow.effectDistance = new Vector2(0, -8 * rootScale * endUnits);
        }
    }

    static float Units(RectTransform target)
    {
        var canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
        return canvas != null ? 1 / Mathf.Max(.001f, canvas.scaleFactor) : 1;
    }

    static void Place(RectTransform target, float x, float y, float width, float height, float units)
    {
        if (target == null) return;
        target.anchorMin = target.anchorMax = new Vector2(0, 1);
        target.pivot = new Vector2(.5f, .5f);
        target.anchoredPosition = new Vector2(x, -y) * units;
        target.sizeDelta = new Vector2(width, height) * units;
    }

    public float FooterContentHeight()
    {
        EnsureLayout();
        if (footer == null || !footer.gameObject.activeInHierarchy || footerContent == null) return 0;
        float top = 0;
        foreach (var item in footerContent)
        {
            if (item == null || !item.gameObject.activeInHierarchy) continue;
            item.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++) top = Mathf.Max(top, RectTransformUtility.WorldToScreenPoint(null, corners[i]).y);
        }
        return top + Mathf.Min(Screen.width, Screen.height) * .01f;
    }

    public bool BlocksFooterPointer(Vector2 position)
    {
        if (footer == null || !footer.gameObject.activeInHierarchy) return false;
        if (footerTapArea != null && RectTransformUtility.RectangleContainsScreenPoint(footerTapArea, position)) return true;
        if (footerContent != null)
            foreach (var item in footerContent)
                if (item != null && item.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(item, position)) return true;
        return false;
    }
}
