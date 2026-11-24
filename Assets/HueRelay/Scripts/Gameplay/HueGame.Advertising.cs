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
        public bool IsAdBreakActive => adBreakActive;
        private HueAds Ads => adController ? adController : (adController = GetComponent<HueAds>());

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
