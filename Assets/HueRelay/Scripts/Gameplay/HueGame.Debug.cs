#if UNITY_EDITOR
using System;
using UnityEngine;

namespace HueRelay
{
    // Editor-only commands use the active inventory, including validation saves.
    public sealed partial class HueGame
    {
        public bool DebugCanEdit => enabled && P != null && !generating && !adBreakActive
            && !IsPowerUpAdPending && !(Ads && Ads.IsBusy);
        public int DebugBestScore => bestScore;

        private void DebugRequireReady()
        {
            if (!DebugCanEdit) throw new InvalidOperationException("等待广告或棋盘生成结束后再调试。");
        }

        public void DebugSetPowerUps(int hintCount, int eraserCount, int extraCount)
        {
            DebugRequireReady();
            DismissPowerUpRefill(false);
            CancelErase();
            Inventory.DebugSetCounts(hintCount, eraserCount, extraCount);
            Render();
        }

        public void DebugJumpToLevel(int levelNumber)
        {
            DebugRequireReady();
            if (levelNumber < 1 || levelNumber > LevelCount)
                throw new ArgumentOutOfRangeException(nameof(levelNumber));
            if (mainMenu) mainMenu.HideForGameplay();
            StartLevel(levelNumber - 1);
            Render();
        }

        public void DebugFinish(bool win)
        {
            DebugRequireReady();
            // A clean board avoids delayed flights/bonus callbacks modifying the forced result.
            if (tut >= 0) EndTutorial();
            if (mainMenu) mainMenu.HideForGameplay();
            ResetGame();
            if (win) Mask = 0;
            Taps = Limit;
            Finish(win); // Exercise the normal progress and ad-win accounting.
            timed.Clear();
            ShowSheet();
            Render();
        }

        public void DebugSetCampaignProgress(bool completed)
        {
            DebugRequireReady();
            campaignProgress.DebugSetProgress(completed ? LevelCount : 0);
            DebugJumpToLevel(completed ? LevelCount : 1);
        }

        public void DebugTutorial(bool replay)
        {
            DebugRequireReady();
            if (replay)
            {
                DebugJumpToLevel(LevelIndex + 1);
                PlayerPrefs.DeleteKey(tutorialSeenKey);
                StartTutorial();
            }
            else
            {
                if (tut >= 0) EndTutorial();
                PlayerPrefs.SetInt(tutorialSeenKey, 1);
            }
            PlayerPrefs.Save();
            Render();
        }

        public void DebugResetBestScore()
        {
            DebugRequireReady();
            bestScore = 0;
            // Store zero rather than deleting the key, preventing old record migration.
            PlayerPrefs.SetInt(bestScoreKey, 0);
            PlayerPrefs.Save();
            Render();
        }
    }
}
#endif
