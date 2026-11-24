using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>保存于场景中的暂停面板，复用结算卡片与按钮素材。</summary>
    public sealed class HuePauseMenu : MonoBehaviour
    {
        public RectTransform card;
        public Text levelLabel;
        public Button continueButton, tutorialButton, retryButton, settingsButton, homeButton;
        private HueGame game;

        public bool ResolveBindings()
        {
            if (!card) card = transform.Find("PauseCard") as RectTransform;
            if (!card) return false;
            if (!levelLabel) levelLabel = card.Find("LevelLabel")?.GetComponent<Text>();
            if (!continueButton) continueButton = card.Find("ContinueButton")?.GetComponent<Button>();
            if (!tutorialButton) tutorialButton = card.Find("TutorialButton")?.GetComponent<Button>();
            if (!retryButton) retryButton = card.Find("RetryButton")?.GetComponent<Button>();
            if (!homeButton) homeButton = card.Find("HomeButton")?.GetComponent<Button>();
            if (levelLabel) levelLabel.gameObject.SetActive(false);
            if (!settingsButton) settingsButton = card.Find("SettingsButton")?.GetComponent<Button>();
            // Recover an already-open scene after script reload. Authored scenes
            // contain this button; only older scenes need the one-time upgrade.
            if (!settingsButton && homeButton && continueButton && tutorialButton && retryButton)
            {
                settingsButton = Instantiate(homeButton, card, false);
                settingsButton.name = "SettingsButton";
                settingsButton.onClick = new Button.ButtonClickedEvent();
                settingsButton.GetComponentInChildren<Text>(true).text = "Settings";
                var buttons = new[] { continueButton, tutorialButton, retryButton, settingsButton, homeButton };
                for (int i = 0; i < buttons.Length; i++)
                {
                    var rect = (RectTransform)buttons[i].transform;
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -(82 + i * 56));
                }
            }
            return continueButton && tutorialButton && retryButton && settingsButton && homeButton;
        }

        public void Show(HueGame owner, bool canTeach)
        {
            if (!ResolveBindings()) return;
            game = owner;
            Bind(continueButton, game.ResumePause);
            Bind(tutorialButton, game.TutorialFromPause);
            Bind(retryButton, game.RetryFromPause);
            Bind(settingsButton, game.OpenSettingsFromPause);
            Bind(homeButton, game.ReturnHome);
            tutorialButton.interactable = canTeach;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Layout();
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }

        public void Hide() { gameObject.SetActive(false); }
        private void Update() { Layout(); }
        public void Layout()
        {
            if (!card) return;
            var parent = transform as RectTransform;
            float scale = Mathf.Max(.1f, Mathf.Min(1, Mathf.Min((parent.rect.width - 32) / 326,
                (parent.rect.height - 32) / 378)));
            card.localScale = Vector3.one * scale;
        }
    }
}
