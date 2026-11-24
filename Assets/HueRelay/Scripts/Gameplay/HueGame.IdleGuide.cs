using UnityEngine;

namespace HueRelay
{
    public sealed partial class HueGame
    {
        private float idleMoveSeconds;
        private bool idleMoveGuide;
        private int moveGuideInputFrame = -1;
        private int moveGuideHapticCycle = -1;

        private void ResetIdleMoveGuide()
        {
            idleMoveSeconds = 0;
            idleMoveGuide = false;
            moveGuideHapticCycle = -1;
            if (sceneView) sceneView.SetMoveNudge(false, 0);
        }

        private void UpdateIdleMoveGuide()
        {
            bool input = Input.anyKey || Input.GetMouseButton(0) || Input.GetMouseButton(1)
                || Input.touchCount > 0 || Input.mouseScrollDelta.sqrMagnitude > 0;
            bool available = Application.isFocused && analyticsHostVisible && !analyticsPaused && !adBreakActive && !(Ads && Ads.IsBusy)
                && !(mainMenu && mainMenu.IsVisible) && CanRequestExtraMoves;
            TickIdleMoveGuide(available, input, Time.unscaledDeltaTime);
        }

        private void TickIdleMoveGuide(bool available, bool input, float delta)
        {
            if (input && idleMoveGuide) moveGuideInputFrame = Time.frameCount;
            if (input || !available) { ResetIdleMoveGuide(); return; }
            idleMoveSeconds += Mathf.Max(0, delta);
            bool guide = idleMoveSeconds >= 5;
            if (guide && !idleMoveGuide) TrackGameEvent("idle_move_nudge");
            idleMoveGuide = guide;
            sceneView.SetMoveNudge(guide, idleMoveSeconds - 5);
            if (guide)
            {
                // One short pulse per shake, never one request per frame or a catch-up burst.
                int cycle = Mathf.FloorToInt((idleMoveSeconds - 5) / HueView.MoveNudgeInterval);
                if (cycle != moveGuideHapticCycle)
                {
                    moveGuideHapticCycle = cycle;
                    HueAudio.VibrateShort();
                }
            }
        }
    }
}
