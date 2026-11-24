using UnityEngine;

namespace HueRelay
{
    [CreateAssetMenu(menuName = "Hue Relay/Ad Policy", fileName = "AdPolicy")]
    public sealed class HueAdPolicy : ScriptableObject
    {
        [Header("Enable only after configuring and testing active TikTok placements")]
        public bool adsEnabled;
        public bool rewardedEnabled = true;
        [InspectorName("Result Video Enabled")]
        public bool interstitialEnabled = true;
        public string rewardedAdUnitId = "";
        [HideInInspector] public string interstitialAdUnitId = ""; // Unused legacy placement.

        [Header("Result rewarded video: one opportunity per completed group of wins; no time cooldown")]
        [InspectorName("Wins Between Result Videos"), Min(1)] public int winsBetweenInterstitials = 3;
        // Preserve old serialized fields without letting them reintroduce result-video time gates.
        [HideInInspector] public int protectedCampaignLevels;
        [HideInInspector] public float firstInterstitialAfterSeconds;
        [HideInInspector] public float interstitialIntervalSeconds;
        [HideInInspector] public float rewardedGraceSeconds;
        [HideInInspector] public int maxInterstitialsPerSession;

        [Header("Loading and failure recovery")]
        [Min(0)] public float rewardedRequestIntervalSeconds = 10;
        [Min(5)] public float preloadRetrySeconds = 30;
        [Min(5)] public float preloadTimeoutSeconds = 20;
        [Min(120)] public float playbackTimeoutSeconds = 180;
        public bool logDecisions;

        // Runtime clamps also protect builds from invalid serialized/remote values.
        public float InterstitialInterval => 0;
        public float FirstInterstitialDelay => 0;
        public float RetryDelay => Mathf.Max(5, preloadRetrySeconds);
        public float LoadTimeout => Mathf.Max(5, preloadTimeoutSeconds);
        public float PlaybackTimeout => Mathf.Max(120, playbackTimeoutSeconds);
        public bool RewardedConfigured => adsEnabled && rewardedEnabled && !string.IsNullOrWhiteSpace(rewardedAdUnitId);
        public bool InterstitialConfigured => RewardedConfigured && interstitialEnabled;
    }
}
