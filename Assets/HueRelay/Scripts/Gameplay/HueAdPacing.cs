using System;

namespace HueRelay
{
    public struct HueAdBreakContext
    {
        public bool won, tutorialActive, beforeResult, endless;
        public int levelNumber;
    }

    /// <summary>SDK-independent policy. A declined opportunity is never queued for later.</summary>
    public sealed class HueAdPacing
    {
        private readonly HueAdPolicy policy;
        private double lastRewarded = double.NegativeInfinity;
        public int WinsSinceInterstitial { get; private set; }
        public int InterstitialsThisSession { get; private set; }

        public HueAdPacing(HueAdPolicy settings) { policy = settings; }
        // Legacy method names remain compatible with editor tools. Result videos have no time gate.
        public void RecordModuleStarted(double now) { }
        public void RecordWin() { if (WinsSinceInterstitial < int.MaxValue) WinsSinceInterstitial++; }
        public void RecordRewardedActivity(double now) { lastRewarded = now; }
        public void RecordInterstitialAttempt(double now) { WinsSinceInterstitial = 0; }
        public void RecordInterstitialClosed(double now)
        {
            if (InterstitialsThisSession < int.MaxValue) InterstitialsThisSession++;
            WinsSinceInterstitial = 0;
        }

        public string InterstitialBlock(double now, HueAdBreakContext context, bool busy, bool ready)
        {
            if (!policy.InterstitialConfigured) return "disabled_or_missing_placement";
            if (busy) return "another_ad_active";
            if (!context.beforeResult || context.tutorialActive) return "not_a_result_boundary";
            if (!context.won) return "not_a_win";
            if (!context.endless && context.levelNumber <= Math.Max(0, policy.protectedCampaignLevels)) return "protected_level";
            if (WinsSinceInterstitial < Math.Max(1, policy.winsBetweenInterstitials)) return "not_enough_wins";
            if (!ready) return "not_loaded";
            return null;
        }

        public string RewardedBlock(double now, bool busy)
        {
            if (!policy.RewardedConfigured) return "disabled_or_missing_placement";
            if (busy) return "another_ad_active";
            if (now - lastRewarded < Math.Max(0, policy.rewardedRequestIntervalSeconds)) return "rewarded_cooldown";
            return null;
        }
    }
}
