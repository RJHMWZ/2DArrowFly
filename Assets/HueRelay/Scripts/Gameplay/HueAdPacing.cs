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
        private double moduleStarted = double.PositiveInfinity;
        private double lastInterstitial = double.NegativeInfinity;
        private double lastInterstitialAttempt = double.NegativeInfinity;
        private double lastRewarded = double.NegativeInfinity;
        public int WinsSinceInterstitial { get; private set; }
        public int InterstitialsThisSession { get; private set; }

        public HueAdPacing(HueAdPolicy settings) { policy = settings; }
        public void RecordModuleStarted(double now) { if (double.IsPositiveInfinity(moduleStarted)) moduleStarted = now; }
        public void RecordWin() { if (WinsSinceInterstitial < int.MaxValue) WinsSinceInterstitial++; }
        public void RecordRewardedActivity(double now) { lastRewarded = now; }
        public void RecordInterstitialAttempt(double now) { lastInterstitialAttempt = now; }
        public void RecordInterstitialClosed(double now)
        {
            lastInterstitial = now;
            InterstitialsThisSession++;
            WinsSinceInterstitial = 0;
        }

        public string InterstitialBlock(double now, HueAdBreakContext context, bool busy, bool ready)
        {
            if (!policy.InterstitialConfigured) return "disabled_or_missing_placement";
            if (busy) return "another_ad_active";
            if (!context.beforeResult || context.tutorialActive) return "not_a_result_boundary";
            if (!context.won) return "not_a_win";
            if (!context.endless && context.levelNumber <= Math.Max(0, policy.protectedCampaignLevels)) return "protected_level";
            if (now < policy.FirstInterstitialDelay) return "session_warmup";
            if (now - moduleStarted < 15) return "sdk_module_warmup";
            if (WinsSinceInterstitial < Math.Max(1, policy.winsBetweenInterstitials)) return "not_enough_wins";
            if (policy.maxInterstitialsPerSession <= 0 || InterstitialsThisSession >= policy.maxInterstitialsPerSession) return "session_cap";
            if (now - lastRewarded < Math.Max(0, policy.rewardedGraceSeconds)) return "rewarded_grace";
            if (now - lastInterstitial < policy.InterstitialInterval) return "interval";
            if (now - lastInterstitialAttempt < policy.RetryDelay) return "attempt_backoff";
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
