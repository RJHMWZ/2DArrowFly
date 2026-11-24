using System;
using UnityEngine;

namespace HueRelay
{
    /// <summary>One rewarded video at a time, shared by result breaks, refills and revives.</summary>
    [DisallowMultipleComponent]
    public sealed class HueAds : MonoBehaviour
    {
        public HueGame game;
        public HueAdPolicy policy;
        public event Action<string> Diagnostic;
        private IHueAdBackend backend;
        private HueAdPacing pacing;
        private Func<double> clock;
        private double foregroundSeconds;
        private bool focused = true, paused, hostVisible = true, bound;
        private readonly bool[] loading = new bool[1];
        private readonly double[] loadStarted = new double[1], nextLoad = new double[1];
        private int request;
        private bool pending, previousAudioPause;
        private HueAdFormat pendingFormat;
        private bool pendingResultVideo;
        private double playbackStarted;
        private Action<HueAdOutcome> finished;
        public bool IsBusy => pending;
        public HueAdPacing Pacing => pacing;
        private double Now => clock != null ? clock() : foregroundSeconds;

        private void Start()
        {
            if (!game) game = GetComponent<HueGame>();
            if (!policy) policy = Resources.Load<HueAdPolicy>("HueRelay/Data/AdPolicy");
            if (backend == null && game && policy) Initialize(game, policy, new HueTikTokAdBackend(policy));
        }

        public void Initialize(HueGame owner, HueAdPolicy settings, IHueAdBackend provider, Func<double> time = null)
        {
            Shutdown();
            game = owner; policy = settings; backend = provider; clock = time;
            foregroundSeconds = 0; pacing = new HueAdPacing(settings);
            HueAudio.SetHostVisible(hostVisible);
            for (int i = 0; i < loading.Length; i++) { loading[i] = false; nextLoad[i] = 0; }
            backend.VisibilityChanged += OnHostVisibility;
            BindRewarded();
        }

        private void BindRewarded()
        {
            bool shouldBind = isActiveAndEnabled && game && policy && policy.adsEnabled;
            if (shouldBind == bound) return;
            if (bound && game) game.RewardedAdRequested -= OnRewardedRequested;
            bound = shouldBind;
            if (bound) game.RewardedAdRequested += OnRewardedRequested;
        }

        private void Update()
        {
            if (focused && !paused && hostVisible) foregroundSeconds += Mathf.Clamp(Time.unscaledDeltaTime, 0, 1);
            Tick();
        }
        private void OnApplicationFocus(bool value) { focused = value; }
        private void OnApplicationPause(bool value) { paused = value; }
        private void OnHostVisibility(bool value)
        {
            hostVisible = value;
            HueAudio.SetHostVisible(value);
            if (game) game.TelemetryHostVisibility(value);
        }

        // Deterministic clock/backend injection keeps checks independent of ads and player saves.
        public void Tick()
        {
            if (backend == null || !policy || !game) return;
            BindRewarded();
            if (pending)
            {
                if (!policy.adsEnabled) Finish(request, HueAdOutcome.Cancelled);
                else if (Now - playbackStarted >= policy.PlaybackTimeout) Finish(request, HueAdOutcome.TimedOut);
                return;
            }
            if (!policy.adsEnabled || !focused || paused || !hostVisible || game.TutorialStepIndex >= 0
                || !game.HasSeenTutorial || game.IsPaused) return;
            Prepare(HueAdFormat.Rewarded, policy.RewardedConfigured);
        }

        private void Prepare(HueAdFormat format, bool configured)
        {
            int i = (int)format;
            if (!configured) return;
            if (backend.IsReady(format))
            {
                if (loading[i]) Trace(format + ":loaded");
                loading[i] = false; return;
            }
            if (loading[i])
            {
                if (Now - loadStarted[i] < policy.LoadTimeout) return;
                ReleaseAd(format); loading[i] = false; nextLoad[i] = Now + policy.RetryDelay;
                Trace(format + ":load_timeout"); return;
            }
            if (Now < nextLoad[i]) return;
            nextLoad[i] = Now + policy.RetryDelay;
            if (!backend.Supports(format)) return;
            pacing.RecordModuleStarted(Now);
            loading[i] = true; loadStarted[i] = Now;
            try { backend.Preload(format); Trace(format + ":preload"); }
            catch (Exception e)
            {
                ReleaseAd(format); loading[i] = false;
                Trace(format + ":load_failed:" + e.Message);
            }
        }

        public void RecordWin() { pacing?.RecordWin(); }

        private void OnRewardedRequested(HuePowerUpKind kind, Action<bool> completed)
        {
            string blocked = pacing.RewardedBlock(Now, pending);
            // Any genuine opt-in, including no-fill/cancellation, protects against a following interstitial.
            if (blocked != "another_ad_active") pacing.RecordRewardedActivity(Now);
            if (blocked == null && (!game.IsPowerUpRefillOpen || !game.IsPowerUpAdPending
                || game.TutorialStepIndex >= 0 || !game.HasSeenTutorial)) blocked = "not_an_opt_in_refill";
            if (blocked == null && !backend.IsReady(HueAdFormat.Rewarded)) blocked = "not_loaded";
            if (blocked != null) { Trace("Rewarded:" + kind + ":skip:" + blocked); completed(false); return; }
            Trace("Rewarded:" + kind + ":opt_in");
            Show(HueAdFormat.Rewarded, result => completed(result == HueAdOutcome.Completed));
        }

        public void TryInterstitial(HueAdBreakContext context, Action continued)
            => TryResultVideo(context, continued);

        public void TryResultVideo(HueAdBreakContext context, Action continued)
        {
            if (backend == null || pacing == null || !isActiveAndEnabled) { continued(); return; }
            string blocked = pacing.InterstitialBlock(Now, context, pending, backend.IsReady(HueAdFormat.Rewarded));
            if (blocked != null)
            {
                // An unloaded victory opportunity is skipped, never queued onto navigation.
                if (blocked == "not_loaded") pacing.RecordInterstitialAttempt(Now);
                Trace("Rewarded:result_win:skip:" + blocked); continued(); return;
            }
            pacing.RecordInterstitialAttempt(Now);
            // This replaces the former interstitial. It deliberately grants no reward.
            Show(HueAdFormat.Rewarded, _ => continued(), true);
        }

        public void TryRevive(Action<bool> completed)
        {
            if (backend == null || pacing == null || !isActiveAndEnabled) { completed(false); return; }
            string blocked = pacing.RewardedBlock(Now, pending);
            if (blocked == null && (!game.IsReviveAdPending || !game.Over || game.Won || !game.ModalVisible
                || game.TutorialStepIndex >= 0 || !game.HasSeenTutorial)) blocked = "not_a_revive_opt_in";
            if (blocked != "another_ad_active" && game.IsReviveAdPending) pacing.RecordRewardedActivity(Now);
            if (blocked == null && !backend.IsReady(HueAdFormat.Rewarded)) blocked = "not_loaded";
            if (blocked != null) { Trace("Rewarded:revive:skip:" + blocked); completed(false); return; }
            Trace("Rewarded:revive:opt_in");
            Show(HueAdFormat.Rewarded, result => completed(result == HueAdOutcome.Completed));
        }

        private void Show(HueAdFormat format, Action<HueAdOutcome> completed, bool resultVideo = false)
        {
            pending = true; pendingFormat = format; finished = completed; playbackStarted = Now;
            pendingResultVideo = resultVideo;
            int token = ++request;
            previousAudioPause = AudioListener.pause; AudioListener.pause = true;
            HueAudio.SetAdPlayback(true);
            Trace(format + (resultVideo ? ":result_win" : "") + ":show_requested");
            try { backend.Show(format, result => Finish(token, result)); }
            catch (Exception e) { Trace(format + ":show_failed:" + e.Message); Finish(token, HueAdOutcome.Failed); }
        }

        private void Finish(int token, HueAdOutcome outcome)
        {
            if (!pending || token != request) return;
            var callback = finished; var format = pendingFormat; bool resultVideo = pendingResultVideo;
            finished = null; pending = false; pendingResultVideo = false; request++;
            ReleaseAd(format); loading[(int)format] = false;
            // SDK destruction can synchronously run host callbacks; restore after it.
            AudioListener.pause = previousAudioPause;
            HueAudio.SetAdPlayback(false);
            nextLoad[(int)format] = Now + (outcome == HueAdOutcome.Completed || outcome == HueAdOutcome.Cancelled
                ? (resultVideo ? 0 : 1) : policy.RetryDelay);
            if (!resultVideo) pacing.RecordRewardedActivity(Now);
            else if (outcome == HueAdOutcome.Completed || outcome == HueAdOutcome.Cancelled) pacing.RecordInterstitialClosed(Now);
            Trace(format + (resultVideo ? ":result_win" : "") + ":finished:" + outcome);
            callback?.Invoke(outcome);
        }

        private void Trace(string value)
        {
            if (game) game.TrackAdEvent(value);
            if (policy && policy.logDecisions) Debug.Log("[HueAds] " + value, this);
            try { Diagnostic?.Invoke(value); } catch (Exception e) { Debug.LogWarning("Ad diagnostic listener failed: " + e.Message, this); }
        }
        private void ReleaseAd(HueAdFormat format)
        {
            try { backend?.Release(format); }
            catch (Exception e) { Trace(format + ":release_failed:" + e.Message); }
        }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (backend == null && game && policy) Initialize(game, policy, new HueTikTokAdBackend(policy));
            else if (backend != null) BindRewarded();
        }
        private void OnDisable() { Shutdown(); }
        private void Shutdown()
        {
            if (bound && game) game.RewardedAdRequested -= OnRewardedRequested;
            bound = false;
            if (pending) Finish(request, HueAdOutcome.Cancelled);
            if (backend != null)
            {
                backend.VisibilityChanged -= OnHostVisibility;
                try { backend.Dispose(); } catch (Exception e) { Trace("dispose_failed:" + e.Message); }
                backend = null;
            }
        }
    }
}
