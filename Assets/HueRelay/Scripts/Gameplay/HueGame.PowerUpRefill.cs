using System;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    public sealed partial class HueGame
    {
        [SerializeField] private HuePowerUpConfig powerUpConfig;
        private HuePowerUpInventory inventory;
        private HuePowerUpRefillPanel refillPanel;
        private HuePowerUpKind refillKind;
        private bool refillOpen, refillPending;
        private int refillRequest;
        public bool IsPowerUpRefillOpen => refillOpen;
        public bool IsPowerUpAdPending => refillPending;
        public HuePowerUpConfig PowerUpSettings
        {
            get
            {
                if (!powerUpConfig) powerUpConfig = Resources.Load<HuePowerUpConfig>("HueRelay/Data/PowerUps");
                if (!powerUpConfig) throw new InvalidOperationException("Missing global PowerUps configuration.");
                return powerUpConfig;
            }
        }
        private HuePowerUpInventory Inventory => inventory ?? (inventory = new HuePowerUpInventory(PowerUpSettings));
        public bool HasClaimedRetentionReward(HueRetentionKind kind) => Inventory.HasClaimedRetentionReward(kind);
        public bool TryClaimRetentionReward(HueRetentionKind kind) => Inventory.TryClaimRetentionReward(kind);
        // Adapter contract: callback(true) only after confirmed full playback.
        // Cancellation, unavailable ad or playback failure must call callback(false).
        public event Action<HuePowerUpKind, Action<bool>> RewardedAdRequested;

        private void EnsureRefillPanel()
        {
            if (refillPanel) return;
            refillPanel = root.GetComponentInChildren<HuePowerUpRefillPanel>(true);
            if (!refillPanel)
            {
                var prefab = Resources.Load<HuePowerUpRefillPanel>("HueRelay/UI/PowerUpRefill");
                if (prefab) refillPanel = Instantiate(prefab, root.transform, false);
            }
            if (refillPanel) refillPanel.Hide();
        }
        private void OpenPowerUpRefill(HuePowerUpKind kind)
        {
            if (!PowerUpsReady || Inventory.Count(kind) != 0) return;
            EnsureRefillPanel();
            if (!refillPanel) { Debug.LogError("Missing PowerUpRefill prefab.", this); return; }
            var source = kind == HuePowerUpKind.Hint ? sceneView.hintButton
                : kind == HuePowerUpKind.Eraser ? sceneView.eraseButton : sceneView.extraMovesButton;
            var icon = source.transform.Find("Icon").GetComponent<Image>();
            refillKind = kind; refillPending = false; refillOpen = true;
            pauseStarted = Time.unscaledTime;
            refillPanel.Show(this, kind, icon.sprite, icon.color, Mathf.Max(1, PowerUpSettings.rewardedAmount));
            Render();
        }
        public void ClosePowerUpRefill()
        {
            if (refillPending) return;
            DismissPowerUpRefill();
        }
        private void DismissPowerUpRefill(bool repaint = true)
        {
            refillRequest++;
            refillPending = false;
            if (refillPanel) refillPanel.Hide();
            if (!refillOpen) return;
            refillOpen = false;
            pausedDuration += Time.unscaledTime - pauseStarted;
            resumedFrame = Time.frameCount;
            if (repaint && root && P != null) Render();
        }
        public void WatchPowerUpAd()
        {
            if (!refillOpen || refillPending || !refillPanel) return;
            refillPending = true;
            int request = ++refillRequest;
            var kind = refillKind;
            int amount = Mathf.Max(1, PowerUpSettings.rewardedAmount);
            refillPanel.SetPending(true);
            Action<bool> completed = watched =>
            {
                if (!this || !refillOpen || !refillPending || request != refillRequest) return;
                // Invalidate before any inventory/UI callbacks; duplicates cannot pay twice.
                refillRequest++; refillPending = false;
                if (watched) { Inventory.Add(kind, amount); DismissPowerUpRefill(); }
                else refillPanel.ShowUnavailable();
            };
            var handler = RewardedAdRequested;
            if (handler != null)
            {
                try { handler(kind, completed); }
                catch (Exception e) { Debug.LogWarning("Rewarded ad request failed: " + e.Message, this); completed(false); }
                return;
            }
#if UNITY_EDITOR
            if (PowerUpSettings.simulateAdsInEditor)
            {
                Debug.Log("[HueRelay] Simulated completed rewarded ad in the Editor only.", this);
                completed(true); return;
            }
#endif
            completed(false);
        }
    }
}
