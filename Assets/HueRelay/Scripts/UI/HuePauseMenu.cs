using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>保存于场景中的暂停面板，复用结算卡片与按钮素材。</summary>
    public sealed class HuePauseMenu : MonoBehaviour
    {
        public RectTransform card;
        public Text levelLabel;
        public Button continueButton, tutorialButton, retryButton, homeButton;
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
            return levelLabel && continueButton && tutorialButton && retryButton && homeButton;
        }

        public void Show(HueGame owner, bool canTeach)
        {
            if (!ResolveBindings()) return;
            game = owner;
            Bind(continueButton, game.ResumePause);
            Bind(tutorialButton, game.TutorialFromPause);
            Bind(retryButton, game.RetryFromPause);
            Bind(homeButton, game.ReturnHome);
            levelLabel.text = game.Endless ? "Endless Mode" : "Level " + (game.LevelIndex + 1);
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
            float scale = Mathf.Max(.1f, Mathf.Min(1, (parent.rect.width - 32) / 326,
                (parent.rect.height - 32) / 378));
            card.localScale = Vector3.one * scale;
        }
    }
}
