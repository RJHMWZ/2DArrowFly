using UnityEngine;

namespace HueRelay
{
    public sealed partial class HueGame
    {
        private HueCampaignProgress campaignProgress;
        private string tutorialSeenKey = "cc_seen";
        private bool resumeEndlessAfterTutorial;
        public int ResumeLevelIndex => campaignProgress != null ? campaignProgress.NextLevelIndex : LevelIndex;
        public bool CampaignCompleted => campaignProgress != null && campaignProgress.Completed;
        public bool HasCompletedTutorial => PlayerPrefs.GetInt(tutorialSeenKey, 0) != 0;
        public string ResumeButtonLabel => CampaignCompleted ? "Endless Mode" : "Level " + (ResumeLevelIndex + 1);

        private void LoadCampaignProgress(string campaignId)
        {
            string key = HueCampaignProgress.KeyFor(string.IsNullOrEmpty(campaignId) ? "campaign" : campaignId);
#if UNITY_EDITOR
            // Automated play-mode checks must not advance the user's local save.
            if (Application.isBatchMode) { key += "__validation"; tutorialSeenKey = "cc_seen_validation"; }
#endif
            campaignProgress = new HueCampaignProgress(key, LevelCount,
                UIManager.Instance ? UIManager.Instance.startingLevel - 1 : 0);
        }

        // All levels and endless play share one local record. Import existing
        // per-level records once without deleting or rewriting the old saves.
        private void LoadGlobalBest(string campaignId)
        {
            string suffix = "";
#if UNITY_EDITOR
            if (Application.isBatchMode) suffix = "__validation";
#endif
            bestScoreKey = "cc_global_best_v1" + suffix;
            if (!PlayerPrefs.HasKey(bestScoreKey))
            {
                int previous = Mathf.Max(0, PlayerPrefs.GetInt("cc_best" + suffix, 0));
                string campaignPrefix = string.IsNullOrEmpty(campaignId) ? "cc_b" : "cc_" + campaignId + "_b";
                for (int i = 0; i < LevelCount; i++)
                {
                    previous = Mathf.Max(previous, PlayerPrefs.GetInt(campaignPrefix + i + suffix, 0));
                    previous = Mathf.Max(previous, PlayerPrefs.GetInt("cc_b" + i + suffix, 0));
                }
                PlayerPrefs.SetInt(bestScoreKey, previous);
                PlayerPrefs.Save();
            }
            bestScore = Mathf.Max(0, PlayerPrefs.GetInt(bestScoreKey, 0));
        }
    }
}
