using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>保存于场景中的暂停面板，复用结算卡片与按钮素材。</summary>
    public sealed class HuePauseMenu : MonoBehaviour
    {
        public RectTransform card;
        public Text levelLabel;
        public Button continueButton, retryButton, settingsButton, homeButton;
        private HueGame game;
        private readonly HuePanelEntrance entrance = new HuePanelEntrance();

        public bool ResolveBindings()
        {
            if (!card) card = transform.Find("PauseCard") as RectTransform;
            if (!card) return false;
            if (!levelLabel) levelLabel = card.Find("LevelLabel")?.GetComponent<Text>();
            if (!continueButton) continueButton = card.Find("ContinueButton")?.GetComponent<Button>();
            // An already-open older scene may still contain the removed entry.
            var oldTutorial = card.Find("TutorialButton");
            if (oldTutorial) oldTutorial.gameObject.SetActive(false);
            if (!retryButton) retryButton = card.Find("RetryButton")?.GetComponent<Button>();
            if (!homeButton) homeButton = card.Find("HomeButton")?.GetComponent<Button>();
            if (levelLabel) levelLabel.gameObject.SetActive(false);
            if (!settingsButton) settingsButton = card.Find("SettingsButton")?.GetComponent<Button>();
            // Recover an already-open scene after script reload. Authored scenes
            // contain this button; only older scenes need the one-time upgrade.
            if (!settingsButton && homeButton && continueButton && retryButton)
            {
                settingsButton = Instantiate(homeButton, card, false);
                settingsButton.name = "SettingsButton";
                settingsButton.onClick = new Button.ButtonClickedEvent();
                settingsButton.GetComponentInChildren<Text>(true).text = "Settings";
            }
            if (!(continueButton && retryButton && settingsButton && homeButton)) return false;
            card.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 322);
            Place(continueButton, 82); Place(retryButton, 138);
            Place(settingsButton, 194); Place(homeButton, 250);
            return true;
        }

        private static void Place(Button button, float y)
        {
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
        }

        public void Show(HueGame owner)
        {
            if (entrance.IsActive && gameObject.activeSelf && card && card.gameObject.activeSelf) return;
            if (!ResolveBindings()) return;
            entrance.Reset();
            game = owner;
            Bind(continueButton, game.ResumePause);
            Bind(retryButton, game.RetryFromPause);
            Bind(settingsButton, game.OpenSettingsFromPause);
            Bind(homeButton, game.ReturnHome);
            gameObject.SetActive(true);
            card.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Layout();
            entrance.Begin(card, GetComponent<Image>(), Time.unscaledTime);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }

        public void Hide() { entrance.Reset(); gameObject.SetActive(false); }
        private void OnDisable() { entrance.Reset(); }
        public void ReplayEntrance()
        {
            entrance.Begin(card, GetComponent<Image>(), Time.unscaledTime);
            Layout();
        }
        private void Update() { Layout(); }
        public void Layout()
        {
            if (!card) return;
            var parent = transform as RectTransform;
            float scale = Mathf.Max(.1f, Mathf.Min(1, Mathf.Min((parent.rect.width - 32) / 326,
                (parent.rect.height - 32) / 322)));
            if (entrance.IsActive) entrance.SetLayout(entrance.RestPosition, scale, Time.unscaledTime);
            else card.localScale = Vector3.one * scale;
        }
    }
}
