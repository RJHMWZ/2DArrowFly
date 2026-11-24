using UnityEngine;

namespace HueRelay
{
    public sealed partial class HueGame
    {
        private HueTelemetry telemetry;
        private bool analyticsFocused = true, analyticsPaused, analyticsHostVisible = true;
        private void TrackGameEvent(string name, params object[] fields) => telemetry?.Track(name, fields);
        public void TrackAdEvent(string value) => TrackGameEvent("ad_event", "detail", value,
            "placement", adBreakActive ? (Won ? "result_win" : "result_fail") : refillOpen ? refillKind.ToString() : "preload");
        private void TickTelemetry()
        {
            if (telemetry == null || P == null) return;
            bool playing = tut < 0 && !Over && !generating && !(mainMenu && mainMenu.IsVisible);
            bool active = playing && !IsPaused && !refillOpen && !adBreakActive && !(Ads && Ads.IsBusy)
                && analyticsFocused && !analyticsPaused && analyticsHostVisible;
            telemetry.Tick(playing, active, LevelIndex + 1, Endless, Score, Taps, Mathf.Max(0, Limit - Taps), HueEngine.PC(Mask));
        }
        private void UpdateTelemetryValues() => telemetry?.UpdateValues(Score, Taps, Mathf.Max(0, Limit - Taps), HueEngine.PC(Mask));
        private void RefreshTelemetryVisibility()
        {
            telemetry?.Visibility(analyticsFocused && !analyticsPaused && analyticsHostVisible, adBreakActive || refillPending);
            ResetIdleMoveGuide();
        }
        public void TelemetryHostVisibility(bool visible)
        {
            analyticsHostVisible = visible; RefreshTelemetryVisibility();
        }
        private void OnApplicationQuit() { UpdateTelemetryValues(); telemetry?.EndSession(); }
    }
}
