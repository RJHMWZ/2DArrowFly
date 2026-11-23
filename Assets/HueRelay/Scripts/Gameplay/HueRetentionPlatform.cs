using System;

namespace HueRelay
{
    public enum HueRetentionKind { Profile, Shortcut }
    public enum HueRetentionResult { Completed, Eligible, Ineligible, Unavailable, Failed }

    public interface IHueRetentionPlatform : IDisposable
    {
        event Action Resumed;
        bool Supports(HueRetentionKind kind, bool reward);
        void Open(HueRetentionKind kind, Action<HueRetentionResult> callback);
        void CheckReward(HueRetentionKind kind, Action<HueRetentionResult> callback);
    }

    /// <summary>Use the installed TikTok SDK's canIUse checks and mission APIs.</summary>
    public sealed class HueRetentionPlatform : IHueRetentionPlatform
    {
        public event Action Resumed;
        private void NotifyResumed() { Resumed?.Invoke(); }
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
        private TTSDK.TTAppLifeCycle lifeCycle;
        private TTSDK.TTAppLifeCycle.OnShowEventWithDict onShow;

        public HueRetentionPlatform()
        {
            try
            {
                lifeCycle = TTSDK.TT.GetAppLifeCycle();
                onShow = _ => NotifyResumed();
                if (lifeCycle != null) lifeCycle.OnShow += onShow;
            }
            catch (Exception) { /* Unity focus/pause callbacks remain available. */ }
        }
#endif
        public bool Supports(HueRetentionKind kind, bool reward)
        {
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            try
            {
                if (kind == HueRetentionKind.Profile)
                    return reward ? TTSDK.CanIUse.GetEntranceMissionReward : TTSDK.CanIUse.StartEntranceMission;
                return reward ? TTSDK.CanIUse.GetShortcutMissionReward : TTSDK.CanIUse.AddShortcut;
            }
            catch (Exception) { return false; }
#else
            return false;
#endif
        }

        public void Open(HueRetentionKind kind, Action<HueRetentionResult> callback)
        {
            if (!Supports(kind, false)) { callback(HueRetentionResult.Unavailable); return; }
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            bool finished = false;
            Action<HueRetentionResult> finish = result => { if (finished) return; finished = true; callback(result); };
            try
            {
                if (kind == HueRetentionKind.Shortcut)
                    TTSDK.TT.AddShortcut(ok => finish(ok ? HueRetentionResult.Completed : HueRetentionResult.Failed));
                else
                    TTSDK.TT.StartEntranceMission(new TTSDK.TTStartEntranceMissionParam
                    {
                        // A successful navigation is not reward eligibility. In particular,
                        // the SDK's is_sufficient field must never grant a game reward.
                        success = _ => finish(HueRetentionResult.Completed),
                        fail = _ => finish(HueRetentionResult.Failed),
                        complete = _ => { }
                    });
            }
            catch (Exception) { finish(HueRetentionResult.Failed); }
#endif
        }

        public void CheckReward(HueRetentionKind kind, Action<HueRetentionResult> callback)
        {
            if (!Supports(kind, true)) { callback(HueRetentionResult.Unavailable); return; }
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            bool finished = false;
            Action<HueRetentionResult> finish = result => { if (finished) return; finished = true; callback(result); };
            try
            {
                if (kind == HueRetentionKind.Profile)
                    TTSDK.TT.GetEntranceMissionReward(new TTSDK.TTGetEntranceMissionRewardParam
                    {
                        success = result => finish(result != null && result.canReceiveReward
                            ? HueRetentionResult.Eligible : HueRetentionResult.Ineligible),
                        fail = _ => finish(HueRetentionResult.Failed),
                        complete = _ => { }
                    });
                else
                    TTSDK.TT.GetShortcutMissionReward(new TTSDK.GetShortcutMissionRewardParam
                    {
                        Success = result => finish(result != null && result.CanReceiveReward
                            ? HueRetentionResult.Eligible : HueRetentionResult.Ineligible),
                        Fail = _ => finish(HueRetentionResult.Failed),
                        Complete = () => { }
                    });
            }
            catch (Exception) { finish(HueRetentionResult.Failed); }
#endif
        }

        public void Dispose()
        {
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            if (lifeCycle != null && onShow != null) lifeCycle.OnShow -= onShow;
            lifeCycle = null;
#endif
            Resumed = null;
        }
    }
}
