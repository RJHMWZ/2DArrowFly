using System;

namespace HueRelay
{
    public enum HueAdFormat { Rewarded, Interstitial }
    public enum HueAdOutcome { Completed, Cancelled, Failed, Unavailable, TimedOut }

    public interface IHueAdBackend : IDisposable
    {
        event Action<bool> VisibilityChanged;
        bool Supports(HueAdFormat format);
        bool IsReady(HueAdFormat format);
        void Preload(HueAdFormat format);
        void Show(HueAdFormat format, Action<HueAdOutcome> completed);
        void Release(HueAdFormat format);
    }

    /// <summary>Direct official SDK adapter; creation/load is never counted as an impression.</summary>
    public sealed class HueTikTokAdBackend : IHueAdBackend
    {
        private readonly HueAdPolicy policy;
        public event Action<bool> VisibilityChanged;
        private void NotifyVisibility(bool visible) { VisibilityChanged?.Invoke(visible); }
        public HueTikTokAdBackend(HueAdPolicy settings) { policy = settings; }

#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
        private sealed class Slot
        {
            public TTSDK.TTRewardedVideoAd rewarded;
            public TTSDK.TTInterstitialAd interstitial;
            public TTSDK.AdLoadDelegate loaded;
            public TTSDK.AdErrorDelegate error;
            public TTSDK.RewardedAdClosedDelegate rewardClosed;
            public TTSDK.AdCloseDelegate interstitialClosed;
            public Action<HueAdOutcome> completed;
            public bool ready;
        }
        private readonly Slot[] slots = { new Slot(), new Slot() };
        private TTSDK.TTAppLifeCycle lifeCycle;
        private TTSDK.TTAppLifeCycle.OnShowEventWithDict onShow;
        private TTSDK.TTAppLifeCycle.OnAppHideEvent onHide;

        private void BindLifeCycle()
        {
            if (lifeCycle != null) return;
            lifeCycle = TTSDK.TT.GetAppLifeCycle();
            if (lifeCycle == null) return;
            onShow = _ => NotifyVisibility(true); onHide = () => NotifyVisibility(false);
            lifeCycle.OnShow += onShow; lifeCycle.OnHide += onHide;
        }
#endif
        public bool Supports(HueAdFormat format)
        {
            if (policy == null) return false;
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            try
            {
                if (!TTSDK.TT.InContainerEnv) return false; // Existing Ivy initialization owns TT.InitSDK.
                BindLifeCycle();
                return format == HueAdFormat.Rewarded
                    ? TTSDK.CanIUse.CreateRewardedVideoAd && TTSDK.CanIUse.TTRewardedVideoAd.Load
                        && TTSDK.CanIUse.TTRewardedVideoAd.IsLoaded && TTSDK.CanIUse.TTRewardedVideoAd.Show
                    : TTSDK.CanIUse.CreateInterstitialAd && TTSDK.CanIUse.TTInterstitialAd.Load
                        && TTSDK.CanIUse.TTInterstitialAd.IsLoaded && TTSDK.CanIUse.TTInterstitialAd.Show;
            }
            catch (Exception) { return false; }
#else
            return false;
#endif
        }
        public bool IsReady(HueAdFormat format)
        {
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            var slot = slots[(int)format];
            try
            {
                return slot.ready || (format == HueAdFormat.Rewarded
                    ? slot.rewarded != null && slot.rewarded.IsLoaded()
                    : slot.interstitial != null && slot.interstitial.IsLoaded());
            }
            catch (Exception) { return false; }
#else
            return false;
#endif
        }
        public void Preload(HueAdFormat format)
        {
            if (!Supports(format)) return;
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            var slot = slots[(int)format];
            if (slot.rewarded != null || slot.interstitial != null) return;
            try
            {
                if (format == HueAdFormat.Rewarded)
                {
                    var ad = TTSDK.TT.CreateRewardedVideoAd(new TTSDK.CreateRewardedVideoAdParam { AdUnitId = policy.rewardedAdUnitId.Trim() });
                    slot.rewarded = ad;
                    slot.loaded = () => { if (slot.rewarded == ad) slot.ready = true; };
                    slot.error = (_, __) => { if (slot.rewarded == ad) Finish(format, HueAdOutcome.Failed); };
                    slot.rewardClosed = ended => { if (slot.rewarded == ad) Finish(format, ended ? HueAdOutcome.Completed : HueAdOutcome.Cancelled); };
                    ad.OnLoad += slot.loaded; ad.OnError += slot.error; ad.OnClose += slot.rewardClosed;
                    ad.Load();
                }
                else
                {
                    var ad = TTSDK.TT.CreateInterstitialAd(new TTSDK.CreateInterstitialAdParam { InterstitialAdId = policy.interstitialAdUnitId.Trim() });
                    slot.interstitial = ad;
                    slot.loaded = () => { if (slot.interstitial == ad) slot.ready = true; };
                    slot.error = (_, __) => { if (slot.interstitial == ad) Finish(format, HueAdOutcome.Failed); };
                    slot.interstitialClosed = () => { if (slot.interstitial == ad) Finish(format, HueAdOutcome.Completed); };
                    ad.OnLoad += slot.loaded; ad.OnError += slot.error; ad.OnClose += slot.interstitialClosed;
                    ad.Load();
                }
            }
            catch (Exception) { Finish(format, HueAdOutcome.Failed); }
#endif
        }
        public void Show(HueAdFormat format, Action<HueAdOutcome> completed)
        {
            if (!IsReady(format)) { completed(HueAdOutcome.Unavailable); return; }
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            var slot = slots[(int)format]; slot.completed = completed; slot.ready = false;
            try
            {
                if (format == HueAdFormat.Rewarded) slot.rewarded.Show(); else slot.interstitial.Show();
            }
            catch (Exception) { Finish(format, HueAdOutcome.Failed); }
#endif
        }
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
        private void Finish(HueAdFormat format, HueAdOutcome outcome)
        {
            var callback = slots[(int)format].completed;
            Release(format); // Detach/destroy before callbacks; instances are single-use.
            callback?.Invoke(outcome);
        }
#endif
        public void Release(HueAdFormat format)
        {
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            var slot = slots[(int)format]; slots[(int)format] = new Slot();
            slot.completed = null; slot.ready = false;
            if (slot.rewarded != null)
            {
                slot.rewarded.OnLoad -= slot.loaded; slot.rewarded.OnError -= slot.error; slot.rewarded.OnClose -= slot.rewardClosed;
                try { slot.rewarded.Destroy(); } catch (Exception) { }
                slot.rewarded = null;
            }
            if (slot.interstitial != null)
            {
                slot.interstitial.OnLoad -= slot.loaded; slot.interstitial.OnError -= slot.error; slot.interstitial.OnClose -= slot.interstitialClosed;
                try { slot.interstitial.Destroy(); } catch (Exception) { }
                slot.interstitial = null;
            }
#endif
        }
        public void Dispose()
        {
            Release(HueAdFormat.Rewarded); Release(HueAdFormat.Interstitial);
#if IVY_SDK_TT && UNITY_WEBGL && !UNITY_EDITOR && !UNITY_LUNA
            if (lifeCycle != null) { lifeCycle.OnShow -= onShow; lifeCycle.OnHide -= onHide; lifeCycle = null; }
#endif
            VisibilityChanged = null;
        }
    }
}
