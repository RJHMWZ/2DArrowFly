using UnityEngine;

namespace HueRelay
{
    public sealed partial class HueGame
    {
        public bool IsPaused { get; private set; }
        private HuePauseMenu pauseMenu;
        private float pauseStarted, pausedDuration;
        private int resumedFrame = -1;
        private float GameTime => Time.unscaledTime - pausedDuration;

        public void OpenPause()
        {
            if (!enabled || !root || P == null || generating || Over || Mask == 0 || IsPaused || refillOpen || adBreakActive
                || (mainMenu && mainMenu.IsVisible)) return;
            if (!pauseMenu) pauseMenu = sceneView.canvas.GetComponentInChildren<HuePauseMenu>(true);
            if (!pauseMenu || !pauseMenu.ResolveBindings()) return;
            pauseStarted = Time.unscaledTime;
            IsPaused = true;
            pauseMenu.Show(this, tut < 0 && Idle);
        }

        public void ResumePause()
        {
            if (!IsPaused || (mainMenu && mainMenu.IsPauseSettingsOpen)) return;
            pausedDuration += Time.unscaledTime - pauseStarted;
            IsPaused = false;
            resumedFrame = Time.frameCount;
            if (pauseMenu) pauseMenu.Hide();
        }

        public void RetryFromPause()
        {
            if (!IsPaused || (mainMenu && mainMenu.IsPauseSettingsOpen)) return;
            ResumePause();
            if (tut >= 0) EndTutorial();
            Retry();
        }

        public void TutorialFromPause()
        {
            if (!IsPaused || tut >= 0 || !Idle || (mainMenu && mainMenu.IsPauseSettingsOpen)) return;
            ResumePause();
            OpenHelp();
        }

        public void OpenSettingsFromPause()
        {
            if (!IsPaused) return;
            if (!mainMenu) mainMenu = FindObjectOfType<HueMainMenu>(true);
            if (!mainMenu) return;
            mainMenu.gameObject.SetActive(true);
            mainMenu.OpenSettingsFromPause();
        }
    }
}
