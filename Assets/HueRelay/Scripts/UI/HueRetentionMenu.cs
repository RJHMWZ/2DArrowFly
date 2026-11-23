using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>Scene-authored home entries and one dismissible three-step guide.</summary>
    [ExecuteAlways]
    public sealed class HueRetentionMenu : MonoBehaviour
    {
        public HueMainMenu menu;
        public HueRetention retention;
        public RectTransform entries;
        public Button profileButton, shortcutButton;
        public GameObject overlay;
        public RectTransform card;
        public Button closeButton, laterButton, actionButton;
        public Text title, firstStep, secondStep, thirdStep, rewardLabel, message, actionLabel;
        public Text[] stepNumbers = new Text[3];
        public RectTransform profilePreview, minisPreview, rewardGroup;
        public Image rewardIcon;
        private HueRetentionKind selected;
        public bool IsOpen => overlay && overlay.activeSelf;

        private void OnEnable()
        {
            if (!Application.isPlaying || !profileButton || !retention) return;
            Unbind();
            profileButton.onClick.AddListener(OpenProfile);
            shortcutButton.onClick.AddListener(OpenShortcut);
            closeButton.onClick.AddListener(Close);
            laterButton.onClick.AddListener(Close);
            actionButton.onClick.AddListener(Activate);
            retention.Changed += Refresh;
            Refresh();
        }
        private void OnDisable() { Unbind(); }
        private void Unbind()
        {
            if (profileButton) profileButton.onClick.RemoveListener(OpenProfile);
            if (shortcutButton) shortcutButton.onClick.RemoveListener(OpenShortcut);
            if (closeButton) closeButton.onClick.RemoveListener(Close);
            if (laterButton) laterButton.onClick.RemoveListener(Close);
            if (actionButton) actionButton.onClick.RemoveListener(Activate);
            if (retention) retention.Changed -= Refresh;
        }

        public void OpenProfile() { Open(HueRetentionKind.Profile); }
        public void OpenShortcut() { Open(HueRetentionKind.Shortcut); }
        private void Open(HueRetentionKind kind)
        {
            if (Application.isPlaying && (!menu || !menu.IsVisible)) return;
            menu.CloseSettings();
            selected = kind;
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
            if (Application.isPlaying) retention.Refresh();
            Refresh();
            menu.Layout(Screen.width, Screen.height, Screen.safeArea);
            if (Application.isPlaying && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        public void Close()
        {
            if (overlay) overlay.SetActive(false);
            if (Application.isPlaying && menu && menu.IsVisible && EventSystem.current)
                EventSystem.current.SetSelectedGameObject((selected == HueRetentionKind.Profile ? profileButton : shortcutButton).gameObject);
        }

        private void Activate()
        {
            var state = retention.State(selected);
            if (state.claimed && !state.available) { Close(); return; }
            retention.Activate(selected);
        }

        public void Refresh()
        {
            if (!retention || !title) return;
            bool profile = selected == HueRetentionKind.Profile;
            var state = retention.State(selected);
            title.text = profile ? "Revisit from Profile" : "Add to Home Screen";
            firstStep.text = profile ? "Open your TikTok profile\nmenu" : "Tap Add";
            secondStep.text = profile ? "Open Minis, then this game" : "Add to your home screen";
            thirdStep.text = state.claimed ? "Reward received" : profile ? "Claim 1 hint" : "Claim 2 hints";
            rewardLabel.text = "+" + HueRetention.RewardAmount(selected);
            if (profilePreview) profilePreview.gameObject.SetActive(profile);
            if (minisPreview) minisPreview.gameObject.SetActive(profile);
            message.text = !Application.isPlaying ? ""
                : state.claimed ? (string.IsNullOrEmpty(state.message) ? "Your hints are ready for your next puzzle." : state.message)
                : !state.available ? "Available in TikTok on supported devices.\nYou can keep playing as usual."
                : !string.IsNullOrEmpty(state.message) ? state.message
                : !state.rewardAvailable ? "Reward checks are unavailable on this device."
                : state.checking ? "Checking your revisit reward..." : "";
            message.gameObject.SetActive(!string.IsNullOrEmpty(message.text));
            actionLabel.text = state.opening ? "Opening..." : state.claimed
                ? (!state.available ? "Done" : profile ? "Open Profile" : "Add again")
                : Application.isPlaying && !state.available ? "Unavailable" : profile ? "Get started" : "Add";
            actionButton.interactable = !Application.isPlaying || !state.opening && (state.claimed || state.available);
            // Closing is always possible, even while TikTok is opening or a callback is missing.
            ArrangeGuide(profile);
        }

        private void ArrangeGuide(bool profile)
        {
            if (stepNumbers == null || stepNumbers.Length != 3 || !stepNumbers[0] || !rewardGroup) return;
            Local(title.rectTransform, 0, profile ? 254 : 151, 282, 34);
            Local((RectTransform)closeButton.transform, 143, profile ? 273 : 173, 40, 40);
            float[] rows = profile ? new[] { 208f, 65f, -98f } : new[] { 94f, 44f, -6f };
            var descriptions = new[] { firstStep, secondStep, thirdStep };
            for (int i = 0; i < 3; i++)
            {
                Local(stepNumbers[i].rectTransform, -121, rows[i], 22, 40);
                Local(descriptions[i].rectTransform, 17, rows[i], 226, 40);
            }
            Local(rewardGroup, 0, profile ? -144 : -66, 100, 40);
            Local(message.rectTransform, 0, profile ? -188 : -106, 278, 32);
            Local((RectTransform)actionButton.transform, 0, profile ? -237 : -148, 254, 50);
        }

        private static void Local(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        public void Layout(float width, float height, float top, float bottom, float left = 0, float right = 0)
        {
            if (!entries || !card) return;
            if (!HueMainMenu.Finite(width) || !HueMainMenu.Finite(height) || width < 1 || height < 1
                || !HueMainMenu.Finite(top) || !HueMainMenu.Finite(bottom)
                || !HueMainMenu.Finite(left) || !HueMainMenu.Finite(right)) return;
            float usable = height - top - bottom;
            float contentWidth = width - left - right;
            if (usable <= 0 || contentWidth <= 0) return;
            var size = new Vector2(52, 52);
            float inset = 40;
            if (menu && menu.settingsButton)
            {
                var settings = (RectTransform)menu.settingsButton.transform;
                size = settings.rect.size;
                inset = Mathf.Max(size.x * .5f + 12,
                    settings.anchorMin.x * contentWidth + settings.anchoredPosition.x + (.5f - settings.pivot.x) * size.x);
            }
            if (!HueMainMenu.Finite(size) || size.x <= 0 || size.y <= 0) size = new Vector2(52, 52);
            if (!HueMainMenu.Finite(inset)) inset = 40;
            inset = Mathf.Clamp(inset, size.x * .5f, Mathf.Max(size.x * .5f, contentWidth * .5f));
            float center = left + contentWidth * .5f;
            Place(entries, center, top + usable * .465f, Mathf.Max(0, contentWidth - inset * 2), size.y);
            var gear = menu && menu.settingsButton ? menu.settingsButton.GetComponentInChildren<HueSettingsIcon>(true) : null;
            StyleEntry(profileButton, size, gear, 0);
            StyleEntry(shortcutButton, size, gear, 1);
            float cardHeight = selected == HueRetentionKind.Profile ? 596 : 392;
            Place(card, center, top + usable * .5f, 326, cardHeight);
            card.localScale = Vector3.one * Mathf.Max(.1f, Mathf.Min(1, (contentWidth - 32) / 326, (usable - 24) / cardHeight));
        }

        private static void StyleEntry(Button button, Vector2 size, HueSettingsIcon gear, float side)
        {
            if (!button) return;
            var group = (RectTransform)button.transform.parent;
            group.anchorMin = group.anchorMax = new Vector2(side, .5f);
            group.pivot = new Vector2(.5f, .5f);
            group.sizeDelta = size;
            group.anchoredPosition = Vector2.zero;
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            var icon = button.GetComponentInChildren<HueRetentionIcon>(true);
            if (icon && gear)
            {
                icon.color = gear.color;
                icon.rectTransform.sizeDelta = gear.rectTransform.sizeDelta;
                HueMainMenu.RepairRect(icon.rectTransform, Vector2.zero, new Vector2(28, 28));
            }
            HueMainMenu.RepairDescendants(rect);
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, -y);
        }
        private void Update()
        {
            if (Application.isPlaying && IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }
    }
}
