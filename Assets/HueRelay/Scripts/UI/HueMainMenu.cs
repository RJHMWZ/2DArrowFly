using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>场景中保存的主菜单，独立 Canvas 避免改动游戏布局。</summary>
    [ExecuteAlways]
    public sealed class HueMainMenu : MonoBehaviour
    {
        public HueGame game;
        public HueView view;
        public Canvas menuCanvas;
        public CanvasScaler scaler;
        public RectTransform logo;
        [Tooltip("Logo display size in logical pixels; the image keeps its aspect ratio.")]
        public Vector2 logoSize = new Vector2(280, 140);
        public Button levelButton;
        public Text levelLabel;
        public Button settingsButton;
        public RectTransform settingsSafeArea;
        public Text settingsLabel;
        public GameObject settingsOverlay;
        public RectTransform settingsCard;
        public Button soundButton;
        public Text soundState;
        public Button vibrationButton;
        public Text vibrationState;
        public Button closeButton;
        [SerializeField, HideInInspector] private bool settingsFromPause;
        [SerializeField, HideInInspector] private List<GameObject> hiddenForPauseSettings = new List<GameObject>();
        private RectTransform hiddenPauseCard;
        private Vector2 validSettingsPosition = new Vector2(40, -55);
        private Vector2 validSettingsSize = new Vector2(52, 52);
        private HueRetentionMenu retentionMenu;
        private HueRetentionMenu RetentionMenu => retentionMenu ? retentionMenu : (retentionMenu = GetComponent<HueRetentionMenu>());
        public bool IsPauseSettingsOpen => settingsFromPause;
        public bool IsVisible => menuCanvas && menuCanvas.enabled && !settingsFromPause;

        // Unity may import the scene before a newly added script has compiled.
        // Recover the authored nodes in that already-open scene after script reload.
        public bool ResolveBindings()
        {
            if (!game) game = FindObjectOfType<HueGame>();
            if (!view) view = FindObjectOfType<HueView>();
            if (!menuCanvas) menuCanvas = GetComponent<Canvas>();
            if (!scaler) scaler = GetComponent<CanvasScaler>();
            if (!logo) logo = transform.Find("TitleLogoPlaceholder") as RectTransform;
            // Also refresh the node label in scenes already open during a logo asset rename.
            var titleImage = logo ? logo.GetComponentInChildren<Image>(true) : null;
            if (titleImage && titleImage.sprite && titleImage.name != titleImage.sprite.name)
                titleImage.name = titleImage.sprite.name;
            if (!levelButton) levelButton = Find<Button>("LevelButton");
            if (!levelLabel) levelLabel = Find<Text>("LevelButton/Label");
            if (!settingsSafeArea) settingsSafeArea = transform.Find("SettingsSafeArea") as RectTransform;
            if (!settingsButton) settingsButton = settingsSafeArea
                ? settingsSafeArea.GetComponentInChildren<Button>(true) : Find<Button>("SettingsButton");
            // Upgrade an already-open scene after script reload. Only the parent
            // receives the inset, so the button's authored transform stays editable.
            if (!settingsSafeArea && settingsButton && settingsButton.transform.parent == transform)
            {
                settingsSafeArea = new GameObject("SettingsSafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
                settingsSafeArea.gameObject.layer = settingsButton.gameObject.layer;
                settingsSafeArea.SetParent(transform, false);
                settingsSafeArea.SetSiblingIndex(settingsButton.transform.GetSiblingIndex());
                settingsSafeArea.anchorMin = Vector2.zero;
                settingsSafeArea.anchorMax = Vector2.one;
                settingsSafeArea.offsetMin = settingsSafeArea.offsetMax = Vector2.zero;
                var settingsRect = settingsButton.transform as RectTransform;
                Vector2 authoredPosition = settingsRect.anchoredPosition;
                settingsRect.SetParent(settingsSafeArea, false);
                settingsRect.anchoredPosition = authoredPosition;
            }
            if (!settingsLabel) settingsLabel = Find<Text>("SettingsLabel");
            if (!settingsOverlay)
            {
                var overlay = transform.Find("SettingsOverlay");
                if (overlay) settingsOverlay = overlay.gameObject;
            }
            if (!settingsCard) settingsCard = transform.Find("SettingsOverlay/SettingsCard") as RectTransform;
            if (!soundButton) soundButton = Find<Button>("SettingsOverlay/SettingsCard/SoundToggle");
            if (!soundState) soundState = Find<Text>("SettingsOverlay/SettingsCard/SoundToggle/Label");
            if (!vibrationButton) vibrationButton = Find<Button>("SettingsOverlay/SettingsCard/VibrationToggle");
            if (!vibrationState) vibrationState = Find<Text>("SettingsOverlay/SettingsCard/VibrationToggle/Label");
            if (!closeButton) closeButton = Find<Button>("SettingsOverlay/SettingsCard/CloseSettingsButton");
            var gearNode = settingsButton ? settingsButton.transform.Find("GearIcon") : null;
            var gear = gearNode ? gearNode.GetComponent<HueSettingsIcon>() : null;
            if (gearNode && !gear) gear = gearNode.gameObject.AddComponent<HueSettingsIcon>();
            if (gear && !gear.GetComponent<CanvasRenderer>())
            {
                gear.gameObject.AddComponent<CanvasRenderer>();
            }
            if (gear) { gear.color = HueGame.Mute; gear.raycastTarget = false; gear.SetAllDirty(); }
            return game && view && menuCanvas && scaler && logo && levelButton && levelLabel
                && settingsButton && settingsSafeArea && settingsOverlay && settingsCard
                && soundButton && soundState && vibrationButton && vibrationState && closeButton;
        }

        private T Find<T>(string path) where T : Component
        {
            var node = transform.Find(path);
            return node ? node.GetComponent<T>() : null;
        }

        private void OnEnable()
        {
            if (!ResolveBindings()) return;
            BindButtons();
            RefreshSettings();
        }

        private void BindButtons()
        {
            if (!Application.isPlaying) return;
            // Rebind after scene/script reload; a cached flag can survive while
            // UnityEvent's runtime listeners have already been discarded.
            UnbindButtons();
            levelButton.onClick.AddListener(Play);
            settingsButton.onClick.AddListener(OpenSettings);
            closeButton.onClick.AddListener(CloseSettings);
            soundButton.onClick.AddListener(ToggleSound);
            vibrationButton.onClick.AddListener(ToggleVibration);
        }

        private void OnDisable()
        {
            CloseSettings();
            UnbindButtons();
        }

        private void UnbindButtons()
        {
            if (levelButton) levelButton.onClick.RemoveListener(Play);
            if (settingsButton) settingsButton.onClick.RemoveListener(OpenSettings);
            if (closeButton) closeButton.onClick.RemoveListener(CloseSettings);
            if (soundButton) soundButton.onClick.RemoveListener(ToggleSound);
            if (vibrationButton) vibrationButton.onClick.RemoveListener(ToggleVibration);
        }

        public void Show()
        {
            if (!ResolveBindings()) return;
            HueSafeArea.Refresh();
            BindButtons();
            CloseSettings();
            if (RetentionMenu) { RetentionMenu.Close(); RetentionMenu.retention.Refresh(); }
            levelLabel.text = game.ResumeButtonLabel;
            SetCanvas(view.canvas, false);
            SetCanvas(menuCanvas, true);
            Layout(Screen.width, Screen.height, Screen.safeArea);
        }

        public void Play()
        {
            if (!IsVisible || settingsOverlay.activeSelf || RetentionMenu && RetentionMenu.IsOpen) return;
            game.BeginFromMenu();
        }

        public void HideForGameplay()
        {
            if (!ResolveBindings()) return;
            CloseSettings();
            if (RetentionMenu) RetentionMenu.Close();
            SetCanvas(menuCanvas, false);
            SetCanvas(view.canvas, true);
        }

        private static void SetCanvas(Canvas canvas, bool visible)
        {
            canvas.enabled = visible;
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster) raycaster.enabled = visible;
        }

        public void OpenSettings()
        {
            if (!IsVisible || !settingsOverlay) return;
            if (RetentionMenu) RetentionMenu.Close();
            settingsOverlay.transform.SetAsLastSibling();
            settingsOverlay.SetActive(true); RefreshSettings();
        }

        public void OpenSettingsFromPause()
        {
            if (!ResolveBindings() || !game.IsPaused || settingsFromPause) return;
            BindButtons();
            settingsFromPause = true;
            hiddenForPauseSettings.Clear();
            var pause = view.canvas.GetComponentInChildren<HuePauseMenu>(true);
            if (pause && pause.card && pause.card.gameObject.activeSelf)
            {
                hiddenPauseCard = pause.card;
                hiddenPauseCard.gameObject.SetActive(false);
            }
            // Present only the existing settings overlay on the menu's higher
            // canvas. Keep the paused game visible beneath it.
            foreach (Transform child in transform)
                if (child.gameObject != settingsOverlay && child.gameObject.activeSelf)
                {
                    hiddenForPauseSettings.Add(child.gameObject);
                    child.gameObject.SetActive(false);
                }
            SetCanvas(menuCanvas, true);
            var gameRaycaster = view.canvas.GetComponent<GraphicRaycaster>();
            if (gameRaycaster) gameRaycaster.enabled = false;
            settingsOverlay.SetActive(true);
            RefreshSettings();
            Layout(Screen.width, Screen.height, Screen.safeArea);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        public void CloseSettings()
        {
            if (settingsOverlay) settingsOverlay.SetActive(false);
            if (!settingsFromPause) return;
            settingsFromPause = false;
            if (menuCanvas) SetCanvas(menuCanvas, false);
            foreach (var child in hiddenForPauseSettings) if (child) child.SetActive(true);
            hiddenForPauseSettings.Clear();
            if (hiddenPauseCard) hiddenPauseCard.gameObject.SetActive(true);
            hiddenPauseCard = null;
            if (view && view.canvas)
            {
                var raycaster = view.canvas.GetComponent<GraphicRaycaster>();
                if (raycaster) raycaster.enabled = true;
                var pause = view.canvas.GetComponentInChildren<HuePauseMenu>(true);
                if (pause && pause.settingsButton && game && game.IsPaused && EventSystem.current)
                    EventSystem.current.SetSelectedGameObject(pause.settingsButton.gameObject);
            }
        }
        public void ToggleSound()
        {
            HueAudio.SoundEnabled = !HueAudio.SoundEnabled;
            PlayerPrefs.SetInt("cc_sound", HueAudio.SoundEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSettings();
        }
        public void ToggleVibration()
        {
            HueAudio.VibrationEnabled = !HueAudio.VibrationEnabled;
            PlayerPrefs.SetInt("cc_vibration", HueAudio.VibrationEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSettings();
        }

        private void RefreshSettings()
        {
            if (soundState) SetState(soundState, HueAudio.SoundEnabled);
            if (vibrationState) SetState(vibrationState, HueAudio.VibrationEnabled);
        }

        private static void SetState(Text text, bool on)
        {
            text.text = on ? "ON" : "OFF";
            text.color = on ? HueGame.Blue : HueGame.Mute;
        }

        private void Update()
        {
            if (!menuCanvas || !scaler || !logo || !levelButton || !settingsButton || !settingsSafeArea || !settingsCard)
            {
                if (!ResolveBindings()) return;
                BindButtons();
            }
            Layout(Screen.width, Screen.height, Screen.safeArea);
            if (Application.isPlaying && Input.GetKeyDown(KeyCode.Escape) && IsVisible && settingsOverlay.activeSelf)
                CloseSettings();
        }

        public void Layout(float width, float height, Rect safeArea)
        {
            // Device Simulator and Game View can briefly report an empty viewport.
            // Never let division by zero poison RectTransform's persistent geometry.
            if (!Finite(width) || !Finite(height) || width < 1 || height < 1) return;
            if (!Finite(safeArea.position) || !Finite(safeArea.size) || safeArea.width <= 0 || safeArea.height <= 0)
                safeArea = new Rect(0, 0, width, height);
            else
            {
                float xMin = Mathf.Clamp(safeArea.xMin, 0, width), xMax = Mathf.Clamp(safeArea.xMax, 0, width);
                float yMin = Mathf.Clamp(safeArea.yMin, 0, height), yMax = Mathf.Clamp(safeArea.yMax, 0, height);
                safeArea = xMax > xMin && yMax > yMin ? Rect.MinMaxRect(xMin, yMin, xMax, yMax)
                    : new Rect(0, 0, width, height);
            }
            float scale = Mathf.Min(width / 390f, height / 640f);
            scaler.scaleFactor = scale;
            float w = width / scale, h = height / scale;
            float top = HueSafeArea.TopPixels(width, height, safeArea) / scale, bottom = safeArea.yMin / scale;
            float left = safeArea.xMin / scale, right = (width - safeArea.xMax) / scale;
            float usable = h - top - bottom, contentWidth = w - left - right;
            float center = left + contentWidth * .5f;
            Place(logo, center, top + usable * .275f,
                Mathf.Clamp(logoSize.x, 1, Mathf.Max(1, contentWidth - 48)), Mathf.Max(1, logoSize.y));
            logo.localScale = Vector3.one;
            Place(levelButton.GetComponent<RectTransform>(), center, top + usable * .72f, Mathf.Max(1, Mathf.Min(296, contentWidth - 32)), 72);
            // Write absolute values: offsetMin/offsetMax setters calculate a delta
            // from the previous geometry and cannot recover after it becomes NaN.
            settingsSafeArea.anchorMin = Vector2.zero;
            settingsSafeArea.anchorMax = Vector2.one;
            settingsSafeArea.pivot = new Vector2(.5f, .5f);
            settingsSafeArea.sizeDelta = new Vector2(-left - right, -top);
            settingsSafeArea.anchoredPosition = new Vector2((left - right) * .5f, -top * .5f);
            var settings = (RectTransform)settingsButton.transform;
            if (Finite(settings.sizeDelta) && settings.sizeDelta.x > 0 && settings.sizeDelta.y > 0)
                validSettingsSize = settings.sizeDelta;
            if (Finite(settings.anchoredPosition)) validSettingsPosition = settings.anchoredPosition;
            RepairRect(settings, validSettingsPosition, validSettingsSize);
            var gear = settingsButton.GetComponentInChildren<HueSettingsIcon>(true);
            if (gear) RepairRect(gear.rectTransform, Vector2.zero, new Vector2(28, 28));
            RepairDescendants(settings);
            if (settingsLabel && settingsLabel.gameObject.activeSelf) settingsLabel.gameObject.SetActive(false);
            Place(settingsCard, center, top + usable * .5f, 326, 292);
            settingsCard.localScale = Vector3.one * Mathf.Max(.1f, Mathf.Min(1, (contentWidth - 32) / 326, (usable - 24) / 292));
            if (RetentionMenu) RetentionMenu.Layout(w, h, top, bottom, left, right);
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);

        internal static void RepairRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (!Finite(rect.anchorMin) || !Finite(rect.anchorMax)) rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            if (!Finite(rect.pivot)) rect.pivot = new Vector2(.5f, .5f);
            var currentSize = rect.sizeDelta;
            if (!Finite(currentSize)) rect.sizeDelta = new Vector2(Finite(currentSize.x) ? currentSize.x : size.x,
                Finite(currentSize.y) ? currentSize.y : size.y);
            var currentPosition = rect.anchoredPosition;
            if (!Finite(currentPosition)) rect.anchoredPosition = new Vector2(Finite(currentPosition.x) ? currentPosition.x : position.x,
                Finite(currentPosition.y) ? currentPosition.y : position.y);
            var local = rect.localScale;
            if (!Finite(local.x) || !Finite(local.y) || !Finite(local.z)) rect.localScale = Vector3.one;
        }

        internal static void RepairDescendants(RectTransform parent)
        {
            foreach (Transform child in parent)
            {
                var rect = child as RectTransform;
                if (!rect) continue;
                var fallbackSize = new Vector2(rect.anchorMin.x == rect.anchorMax.x ? parent.rect.width : 0,
                    rect.anchorMin.y == rect.anchorMax.y ? parent.rect.height : 0);
                RepairRect(rect, Vector2.zero, fallbackSize);
                RepairDescendants(rect);
            }
        }

        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, -y);
        }
    }
}
