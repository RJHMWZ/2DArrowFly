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

        private void NavigateAfterResultAd(Action continued)
        {
            if (adBreakActive) return;
            if (!Ads || !Won || !sheetVisible || tut >= 0 || !HasCompletedTutorial) { continued(); return; }
            adBreakActive = true; adBreakStarted = Time.unscaledTime;
            int token = ++adNavigation;
            bool primary = sceneView.resultPrimary.interactable, secondary = sceneView.resultSecondary.interactable;
            sceneView.resultPrimary.interactable = sceneView.resultSecondary.interactable = false;
            Ads.TryInterstitial(new HueAdBreakContext { won = Won, resultAction = sheetVisible,
                tutorialActive = tut >= 0, endless = Endless, levelNumber = LevelIndex + 1 }, () =>
            {
                if (!this || token != adNavigation) return;
                adNavigation++; adBreakActive = false;
                pausedDuration += Time.unscaledTime - adBreakStarted;
                resumedFrame = Time.frameCount;
                sceneView.resultPrimary.interactable = primary; sceneView.resultSecondary.interactable = secondary;
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
