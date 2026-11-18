using UnityEngine;
using UnityEngine.UI;
using Luna.Unity;

// Branding, loading UI and AdSkip are carried over from the arrow template.
// HueGame remains the owner of puzzles, scoring, tutorial and ordinary results.
[DefaultExecutionOrder(-800)]
[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    public GameObject winPanel;
    public RectTransform footer;
    public bool HasRequestedEnd => endRequested;
    public bool BlocksGameplay => loadingActive || endRequested;
    private bool endRequested;
    private bool gameplayStarted;
    private float gameplayStartedAt;
    public PlayablePresentation presentation;

    [Header("Playable Flow (0 seconds disables the timer)")]
    [LunaPlaygroundField("Trial Duration", 1, "Playable Flow")]
    [Min(0)] public float trialDuration = 120f;
    [LunaPlaygroundField("End After Last Level", 2, "Playable Flow")]
    public bool endAfterLastLevel = true;
    [LunaPlaygroundField("End On Failure", 3, "Playable Flow")]
    public bool endOnFailure;
    [LunaPlaygroundField("Starting Level", 4, "Playable Flow")]
    [Range(1, 5)] public int startingLevel = 1;
    [LunaPlaygroundField("Show Tutorial", 5, "Playable Flow")]
    public bool showTutorial = true;
    [Tooltip("Wait for an end-card tap when disabled; call AdSkip after the delay when enabled.")]
    [LunaPlaygroundField("Auto Jump On End", 6, "Playable Flow")]
    public bool autoJumpOnWin;
    [LunaPlaygroundField("Jump Delay", 7, "Playable Flow")]
    [Min(0)] public float jumpDelay = .6f;

    [System.Serializable]
    public sealed class LogoBinding
    {
        public Image image;
        [Tooltip("Optional text shown when no logo is assigned.")]
        public Graphic fallback;
    }

    [System.Serializable]
    public sealed class ButtonImageBinding
    {
        public Image image;
        public Graphic label;
        [Tooltip("Decorations hidden when replacing the button image. Leave empty to keep them.")]
        public Graphic[] decorations = new Graphic[0];
        [System.NonSerialized] bool captured;
        [System.NonSerialized] Sprite originalSprite;
        [System.NonSerialized] Color originalColor;
        [System.NonSerialized] Image.Type originalType;
        [System.NonSerialized] bool originalAspect, originalLabel;
        [System.NonSerialized] bool[] originalDecorations;

        public void Apply(Sprite sprite, bool showText)
        {
            if (image == null) return;
            if (!captured)
            {
                originalSprite = image.sprite; originalColor = image.color;
                originalType = image.type; originalAspect = image.preserveAspect;
                originalLabel = label != null && label.enabled;
                originalDecorations = new bool[decorations.Length];
                for (int i = 0; i < decorations.Length; i++) originalDecorations[i] = decorations[i] != null && decorations[i].enabled;
                captured = true;
            }
            // Middle-platform overrides can replace the same texture asset, so identity cannot detect a replacement.
            bool replaced = sprite != null;
            image.sprite = replaced ? sprite : originalSprite;
            image.color = replaced ? Color.white : originalColor;
            image.type = replaced ? Image.Type.Simple : originalType;
            image.preserveAspect = replaced || originalAspect;
            if (label != null) label.enabled = originalLabel && showText;
            for (int i = 0; i < decorations.Length; i++)
                if (decorations[i] != null) decorations[i].enabled = !replaced && i < originalDecorations.Length && originalDecorations[i];
        }
    }

    [Header("Brand Assets")]
    public Image[] brandIconTargets = new Image[0];
    public LogoBinding[] brandLogoTargets = new LogoBinding[0];
    public ButtonImageBinding[] ctaButtonTargets = new ButtonImageBinding[0];

    [ContextMenu("Apply Brand Assets")]
    public void ApplyBranding()
    {
        Sprite icon = ResolveBrandTexture(BrandIconTexture, ref appliedIconTexture, ref generatedIcon);
        Sprite logo = ResolveBrandTexture(BrandLogoTexture, ref appliedLogoTexture, ref generatedLogo);
        foreach (var image in brandIconTargets)
            if (image != null) { image.sprite = icon; image.preserveAspect = true; image.enabled = icon != null; }
        foreach (var binding in brandLogoTargets)
        {
            if (binding == null || binding.image == null) continue;
            binding.image.sprite = logo;
            binding.image.preserveAspect = true;
            binding.image.enabled = logo != null;
            if (binding.fallback != null) binding.fallback.enabled = logo == null;
        }
        if (!string.IsNullOrEmpty(BrandName))
            foreach (var label in brandNameTargets) if (label != null) label.text = BrandName;
        appliedBrandName = BrandName;
        if (Application.isPlaying)
        {
            Sprite button = ResolveBrandTexture(CtaButtonTexture, ref appliedButtonTexture, ref generatedButton);
            // A transparent 1x1 slot keeps the export replacement field available while retaining authored button styles.
            if (CtaButtonTexture != null && CtaButtonTexture.width == 1 && CtaButtonTexture.height == 1) button = null;
            foreach (var binding in ctaButtonTargets)
                if (binding != null) binding.Apply(button, ShowCtaButtonText);
            appliedButtonText = ShowCtaButtonText;
        }
    }

    [Header("Mid-platform branding")]
    [LunaPlaygroundAsset("Brand Icon", 1, "Branding")]
    [LunaPlaygroundField("Brand Icon", 1, "Branding")]
    public Texture2D BrandIconTexture;
    [LunaPlaygroundAsset("Brand Logo", 2, "Branding")]
    [LunaPlaygroundField("Brand Logo", 2, "Branding")]
    public Texture2D BrandLogoTexture;
    [LunaPlaygroundField("Brand Name", 3, "Branding")]
    public string BrandName = "";
    [LunaPlaygroundAsset("CTA Button Image", 4, "Branding")]
    [LunaPlaygroundField("CTA Button Image", 4, "Branding")]
    [Tooltip("Replace gameplay and end-card button images while preserving color and aspect ratio. Leave empty to keep the scene defaults.")]
    public Texture2D CtaButtonTexture;
    [LunaPlaygroundField("Show CTA Button Text", 5, "Branding")]
    [Tooltip("Disable when the button image already contains its label.")]
    public bool ShowCtaButtonText = true;
    public Text[] brandNameTargets = new Text[0];
    public GameObject loadingPanel;
    [Min(0f)] public float loadingDuration = 1f;
    Texture2D appliedIconTexture, appliedLogoTexture, appliedButtonTexture;
    Sprite generatedIcon, generatedLogo, generatedButton;
    bool appliedButtonText;
    string appliedBrandName;

    public Image loadingProgressFill;
    public CanvasGroup loadingGroup;
    [Min(0f)] public float loadingFadeDuration;
    [Header("Loading animation")]
    public Text loadingStatusText;
    public RectTransform loadingIndicator;
    public string loadingLabel = "Loading Playable Ads";
    [Min(.05f)] public float loadingDotInterval = .2f;
    public float loadingBounceDistance = 20f;
    private float loadingStarted;
    private bool loadingActive;
    private bool loadingClockStarted;
    private Vector2 loadingIndicatorOrigin;
    public bool IsLoading => loadingActive;

    Sprite ResolveBrandTexture(Texture2D texture, ref Texture2D applied, ref Sprite generated)
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            return texture != null ? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GetAssetPath(texture)) : null;
#else
            return null;
#endif
        }
        if (applied != texture)
        {
            if (generated != null) { if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated); }
            generated = texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
            applied = texture;
        }
        return generated;
    }


    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        if (winPanel != null) winPanel.SetActive(false);
        loadingActive = loadingPanel != null && loadingDuration > 0;
        if (loadingPanel != null) loadingPanel.SetActive(loadingActive);
        loadingStarted = Time.unscaledTime;
        if (loadingIndicator != null) loadingIndicatorOrigin = loadingIndicator.anchoredPosition;
        ApplyBranding();
    }

    void Update()
    {
        if (appliedIconTexture != BrandIconTexture || appliedLogoTexture != BrandLogoTexture || appliedBrandName != BrandName || appliedButtonTexture != CtaButtonTexture || appliedButtonText != ShowCtaButtonText)
            ApplyBranding();
        if (loadingActive)
        {
            if (!loadingClockStarted) { loadingStarted = Time.unscaledTime; loadingClockStarted = true; }
            float age = Time.unscaledTime - loadingStarted;
            if (loadingProgressFill != null) loadingProgressFill.fillAmount = Mathf.Lerp(.06f, 1, Mathf.Clamp01(age / Mathf.Max(.01f, loadingDuration)));
            if (loadingStatusText != null)
            {
                int tick = Mathf.FloorToInt(age / Mathf.Max(.05f, loadingDotInterval));
                loadingStatusText.text = loadingLabel + new string('.', tick == 0 ? 3 : tick % 4);
            }
            if (loadingGroup != null) loadingGroup.alpha = 1 - Mathf.Clamp01((age - loadingDuration) / Mathf.Max(.001f, loadingFadeDuration));
            if (age >= loadingDuration + loadingFadeDuration)
            {
                loadingActive = false;
                loadingPanel.SetActive(false);
            }
        }
        if (gameplayStarted && !endRequested && trialDuration > 0 && Time.unscaledTime - gameplayStartedAt >= trialDuration)
            CompletePlayable();
    }

    public void NotifyGameplayStarted()
    {
        if (gameplayStarted || BlocksGameplay) return;
        gameplayStarted = true;
        gameplayStartedAt = Time.unscaledTime;
    }

    public void NotifyLevelResult(bool won, bool lastLevel)
    {
        if ((won && lastLevel && endAfterLastLevel) || (!won && endOnFailure)) CompletePlayable();
    }

    public bool BlocksPointer(Vector2 screenPosition)
    {
        return BlocksGameplay || (presentation != null && presentation.BlocksFooterPointer(screenPosition));
    }

    public float FooterHeightInPixels()
    {
        if (presentation != null) return presentation.FooterContentHeight();
        if (footer == null || !footer.gameObject.activeInHierarchy) return 0;
        return footer.rect.height * Mathf.Abs(footer.lossyScale.y);
    }

    public void CompletePlayable()
    {
        if (endRequested) return;
        endRequested = true;
        LifeCycle.GameEnded();
        if (loadingPanel != null) loadingPanel.SetActive(false);
        loadingActive = false;
        if (winPanel != null) winPanel.SetActive(true);
        if (presentation != null) presentation.ShowEndCard();
        if (autoJumpOnWin) Invoke(nameof(AdSkip), Mathf.Max(0, jumpDelay));
    }

    void OnDestroy()
    {
        if (generatedIcon != null) Destroy(generatedIcon);
        if (generatedLogo != null) Destroy(generatedLogo);
        if (generatedButton != null) Destroy(generatedButton);
        if (Instance == this) Instance = null;
    }
    public void AdSkip()
    {
        CancelInvoke(nameof(AdSkip));
        endRequested = true;
        Debug.Log("AdSkip invoked.");
        LifeCycle.GameEnded();
        Playable.InstallFullGame();
    }

}
