using UnityEngine;
using UnityEngine.UI;

/// <summary>Responsive layout for the startup loading screen.</summary>
[ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-850)]
public sealed class LoadingPresentation : MonoBehaviour
{
    public RectTransform loadingPanel, loadingIcon, loadingLogo, loadingTrack;
    public Text loadingText;
    private Vector2 lastSize;
    private float lastLoadingUnits;

    void OnEnable() { lastSize = Vector2.zero; EnsureLayout(); }
    void LateUpdate() { EnsureLayout(); }

    public void EnsureLayout()
    {
        var canvas = loadingPanel != null ? loadingPanel.GetComponentInParent<Canvas>() : null;
        Vector2 size = Application.isPlaying ? new Vector2(Screen.width, Screen.height)
            : canvas != null ? ((RectTransform)canvas.transform).rect.size * canvas.scaleFactor : Vector2.zero;
        float loadingUnits = Units(loadingPanel);
        if (size.x <= 0 || size.y <= 0 || (size == lastSize && loadingUnits == lastLoadingUnits)) return;
        lastSize = size;
        lastLoadingUnits = loadingUnits;
        ApplyLayout(size.x, size.y);
    }

    public void ApplyLayout(float width, float height)
    {
        float vmin = Mathf.Min(width, height) * .01f;
        float loadingUnits = Units(loadingPanel);
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

}
