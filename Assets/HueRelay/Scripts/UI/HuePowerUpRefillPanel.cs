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

        public void Show(HueGame game, HuePowerUpKind kind, Sprite sprite, Color tint, int amount)
        {
            string name = kind == HuePowerUpKind.Hint ? "Hints" : kind == HuePowerUpKind.Eraser ? "Erasers" : "Extra Moves";
            title.text = "Out of " + name;
            itemIcon.sprite = sprite; itemIcon.color = tint;
            amountLabel.text = "+" + amount;
            message.text = "Watch a video to get " + amount + " " + name.ToLowerInvariant() + ".";
            watchButton.onClick = new Button.ButtonClickedEvent();
            watchButton.onClick.AddListener(game.WatchPowerUpAd);
            closeButton.onClick = new Button.ButtonClickedEvent();
            closeButton.onClick.AddListener(game.ClosePowerUpRefill);
            SetPending(false);
            gameObject.SetActive(true); transform.SetAsLastSibling(); Layout();
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
        public void Hide() { gameObject.SetActive(false); }
        private void Update() { Layout(); }
        public void Layout()
        {
            if (!card) return;
            var parent = (RectTransform)transform;
            card.localScale = Vector3.one * Mathf.Max(.1f, Mathf.Min(1,
                (parent.rect.width - 32) / 326, (parent.rect.height - 32) / 362));
        }
    }
}
