using UnityEngine;

namespace HueRelay
{
    /// <summary>Highest unlocked campaign entry; levelCount means the campaign is complete.</summary>
    public sealed class HueCampaignProgress
    {
        private readonly string key;
        private readonly int levelCount;
        private int next;
        public int NextLevelIndex => Mathf.Clamp(next, 0, levelCount - 1);
        public bool Completed => next >= levelCount;
        public static string KeyFor(string campaignId) => "cc_" + campaignId + "_progress_v1";

        public HueCampaignProgress(string storageKey, int count, int initialLevelIndex)
        {
            key = storageKey; levelCount = Mathf.Max(1, count);
            int stored = PlayerPrefs.GetInt(key, Mathf.Clamp(initialLevelIndex, 0, levelCount - 1));
            next = Mathf.Clamp(stored, 0, levelCount);
            if (!PlayerPrefs.HasKey(key) || stored != next) Save();
        }
        public void Complete(int levelIndex)
        {
            int unlocked = Mathf.Clamp(levelIndex + 1, 0, levelCount);
            if (unlocked <= next) return;
            next = unlocked; Save();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD || (IVY_SDK_TT && TTSDK_LOG_DEBUG)
        public void DebugSetProgress(int nextLevelIndex)
        {
            next = Mathf.Clamp(nextLevelIndex, 0, levelCount);
            Save();
        }
#endif
        private void Save() { PlayerPrefs.SetInt(key, next); PlayerPrefs.Save(); }
    }
}
