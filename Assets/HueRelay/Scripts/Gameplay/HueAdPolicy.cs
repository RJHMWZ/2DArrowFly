using UnityEngine;

namespace HueRelay
{
    [CreateAssetMenu(menuName = "Hue Relay/Ad Policy", fileName = "AdPolicy")]
    public sealed class HueAdPolicy : ScriptableObject
    {
        [Header("Enable only after configuring and testing active TikTok placements")]
        public bool adsEnabled;
        public bool rewardedEnabled = true;
        public bool interstitialEnabled = true;
        public string rewardedAdUnitId = "";
        public string interstitialAdUnitId = "";

        [Header("Interstitial pacing (foreground session time)")]
        [Min(0)] public int protectedCampaignLevels = 3;
        [Min(1)] public int winsBetweenInterstitials = 2;
        [Min(15)] public float firstInterstitialAfterSeconds = 30;
        [Min(30)] public float interstitialIntervalSeconds = 60;
        [Min(0)] public float rewardedGraceSeconds = 60;
        [Min(0)] public int maxInterstitialsPerSession = 6;

        [Header("Loading and failure recovery")]
        [Min(0)] public float rewardedRequestIntervalSeconds = 10;
        [Min(5)] public float preloadRetrySeconds = 30;
        [Min(5)] public float preloadTimeoutSeconds = 20;
        [Min(120)] public float playbackTimeoutSeconds = 180;
        public bool logDecisions;

        // Runtime clamps also protect builds from invalid serialized/remote values.
        public float InterstitialInterval => Mathf.Max(30, interstitialIntervalSeconds);
        public float FirstInterstitialDelay => Mathf.Max(15, firstInterstitialAfterSeconds);
        public float RetryDelay => Mathf.Max(5, preloadRetrySeconds);
        public float LoadTimeout => Mathf.Max(5, preloadTimeoutSeconds);
        public float PlaybackTimeout => Mathf.Max(120, playbackTimeoutSeconds);
        public bool RewardedConfigured => adsEnabled && rewardedEnabled && !string.IsNullOrWhiteSpace(rewardedAdUnitId);
        public bool InterstitialConfigured => adsEnabled && interstitialEnabled && !string.IsNullOrWhiteSpace(interstitialAdUnitId);
    }
}
