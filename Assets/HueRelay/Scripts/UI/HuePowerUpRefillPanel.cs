using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>Authored prefab; only scaled to fit smaller screens at runtime.</summary>
    public sealed class HuePowerUpRefillPanel : MonoBehaviour
    {
        public RectTransform card;
        public Text title, amountLabel, message;
        public Image itemIcon;
        public Button watchButton, closeButton;
        private readonly HuePanelEntrance entrance = new HuePanelEntrance();

        public void Show(HueGame game, HuePowerUpKind kind, Sprite sprite, Color tint, int amount)
        {
            string name = kind == HuePowerUpKind.Hint ? "Undos" : kind == HuePowerUpKind.Eraser ? "Erasers" : "Extra Moves";
            title.text = "Out of " + name;
            itemIcon.sprite = sprite; itemIcon.color = tint;
            amountLabel.text = "+" + amount;
            string rewardName = name.ToLowerInvariant();
            if (amount == 1) rewardName = rewardName.TrimEnd('s');
            message.text = "Watch a video to get " + amount + " " + rewardName + ".";
            watchButton.onClick = new Button.ButtonClickedEvent();
            watchButton.onClick.AddListener(game.WatchPowerUpAd);
            closeButton.onClick = new Button.ButtonClickedEvent();
            closeButton.onClick.AddListener(game.ClosePowerUpRefill);
            SetPending(false);
            entrance.Reset();
            gameObject.SetActive(true); transform.SetAsLastSibling(); Layout();
            entrance.Begin(card, GetComponent<Image>(), Time.unscaledTime);
        }
        public void SetPending(bool waiting)
        {
            watchButton.interactable = closeButton.interactable = !waiting;
            if (waiting) message.text = "Loading ad...";
        }
        public void ShowUnavailable()
        {
            SetPending(false);
            message.text = "No ad available. Please try again later.";
        }
        public void Hide() { entrance.Reset(); gameObject.SetActive(false); }
        private void OnDisable() { entrance.Reset(); }
        private void Update() { Layout(); }
        public void Layout()
        {
            if (!card) return;
            var parent = (RectTransform)transform;
            float scale = Mathf.Max(.1f, Mathf.Min(1,
                Mathf.Min((parent.rect.width - 32) / 326, (parent.rect.height - 32) / 362)));
            if (entrance.IsActive) entrance.SetLayout(entrance.RestPosition, scale, Time.unscaledTime);
            else card.localScale = Vector3.one * scale;
        }
    }
}
