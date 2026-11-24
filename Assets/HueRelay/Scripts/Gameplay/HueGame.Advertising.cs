using System;
using UnityEngine;

namespace HueRelay
{
    public sealed partial class HueGame
    {
        private HueAds adController;
        private bool adBreakActive;
        private float adBreakStarted;
        private int adNavigation;
        private bool revivePending, reviveUnavailable;
        private int reviveRequest;
        private float reviveStarted;
        public bool IsAdBreakActive => adBreakActive;
        public bool IsReviveAdPending => revivePending;
        public bool CanWatchReviveAd => enabled && sheetVisible && Over && !Won && Mask != 0 && tut < 0
            && !generating && !adBreakActive && !revivePending && !(Ads && Ads.IsBusy);
        private HueAds Ads => adController ? adController : (adController = GetComponent<HueAds>());

        public void WatchReviveAd()
        {
            if (!CanWatchReviveAd) return;
            revivePending = true; reviveUnavailable = false; reviveStarted = Time.unscaledTime;
            int token = ++reviveRequest;
            ResetIdleMoveGuide();
            TrackGameEvent("revive_request", "moves_added", 3);
            Render();
            Action<bool> completed = watched =>
            {
                if (!this || !revivePending || token != reviveRequest) return;
                reviveRequest++; revivePending = false;
                pausedDuration += Time.unscaledTime - reviveStarted;
                resumedFrame = Time.frameCount; now = GameTime;
                if (!watched)
                {
                    reviveUnavailable = true;
                    TrackGameEvent("revive_unavailable");
                    Render(); return;
                }
                if (!Over || Won || !sheetVisible || Mask == 0) return;
                Limit += 3;
                AnimateAddedMoves(3);
                Over = Won = sheetVisible = false;
                finishAt = -1; endAt = modalAt = -100; cachedModal = -1;
                chipOverride = null; hintId = -1;
                sceneView.HideTransient();
                UpdateTelemetryValues();
                telemetry?.Revive(3);
                Render();
            };
            if (!Ads) { completed(false); return; }
            try { Ads.TryRevive(completed); }
            catch (Exception e) { Debug.LogWarning("Revive ad request failed: " + e.Message, this); completed(false); }
        }

        private void CancelRevive()
        {
            reviveRequest++; reviveUnavailable = false;
            if (!revivePending) return;
            revivePending = false;
            pausedDuration += Time.unscaledTime - reviveStarted;
        }

        private void ShowResultAfterAd(Action continued)
        {
            if (adBreakActive) return;
            if (!Ads || !Over || tut >= 0 || !HasCompletedTutorial) { continued(); return; }
            adBreakActive = true; adBreakStarted = Time.unscaledTime;
            int token = ++adNavigation;
            Ads.TryInterstitial(new HueAdBreakContext { won = Won, beforeResult = true,
                tutorialActive = tut >= 0, endless = Endless, levelNumber = LevelIndex + 1 }, () =>
            {
                if (!this || token != adNavigation) return;
                adNavigation++; adBreakActive = false;
                pausedDuration += Time.unscaledTime - adBreakStarted;
                resumedFrame = Time.frameCount;
                now = GameTime;
                if (isActiveAndEnabled) continued();
            });
        }

        private void CancelAdNavigation()
        {
            adNavigation++;
            if (!adBreakActive) return;
            adBreakActive = false;
            pausedDuration += Time.unscaledTime - adBreakStarted;
            sceneView.resultPrimary.interactable = sceneView.resultSecondary.interactable = true;
        }
    }
}
