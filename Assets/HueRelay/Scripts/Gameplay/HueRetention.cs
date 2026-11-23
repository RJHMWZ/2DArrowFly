using System;
using UnityEngine;

namespace HueRelay
{
    /// <summary>Survives hidden menu canvases so cold starts and platform onShow both verify rewards.</summary>
    [DisallowMultipleComponent]
    public sealed class HueRetention : MonoBehaviour
    {
        public sealed class EntryState
        {
            public bool available, rewardAvailable, claimed, opening, checking, requested;
            public string message = "";
            internal int openRequest, rewardRequest;
            internal float openDeadline, rewardDeadline;
        }

        public HueGame game;
        public event Action Changed;
        private IHueRetentionPlatform platform;
        private readonly EntryState[] states = { new EntryState(), new EntryState() };
        private bool refreshNextFrame;
        private bool initialized;
        private const float TimeoutSeconds = 20;
        public static int RewardAmount(HueRetentionKind kind) => kind == HueRetentionKind.Profile ? 1 : 2;
        public EntryState State(HueRetentionKind kind) => states[(int)kind];

        private void Start()
        {
            if (!game) game = FindObjectOfType<HueGame>();
            if (platform == null) Initialize(new HueRetentionPlatform());
        }

        // Also used by editor checks with a deterministic platform; production never simulates success.
        public void Initialize(IHueRetentionPlatform adapter)
        {
            if (platform != null) { platform.Resumed -= QueueRefresh; platform.Dispose(); }
            foreach (var state in states)
            {
                state.openRequest++; state.rewardRequest++;
                state.opening = state.checking = state.requested = false;
                state.message = "";
            }
            platform = adapter;
            platform.Resumed += QueueRefresh;
            initialized = true;
            Refresh();
        }

        public void Refresh()
        {
            if (!initialized || !game) return;
            foreach (HueRetentionKind kind in Enum.GetValues(typeof(HueRetentionKind)))
            {
                var state = State(kind);
                state.available = platform.Supports(kind, false);
                state.rewardAvailable = platform.Supports(kind, true);
                state.claimed = game.HasClaimedRetentionReward(kind);
                CheckReward(kind);
            }
            Changed?.Invoke();
        }

        public void Activate(HueRetentionKind kind)
        {
            if (!initialized || !game) return;
            var state = State(kind);
            if (state.opening) return;
            state.available = platform.Supports(kind, false);
            if (!state.available)
            {
                state.message = "This entry is unavailable on this device.\nYou can keep playing as usual.";
                Changed?.Invoke(); return;
            }
            state.opening = state.requested = true;
            state.message = "Opening TikTok...";
            state.openDeadline = Time.realtimeSinceStartup + TimeoutSeconds;
            int request = ++state.openRequest;
            Changed?.Invoke();
            platform.Open(kind, result =>
            {
                if (!this || request != state.openRequest) return;
                state.opening = false;
                if (state.claimed) { Changed?.Invoke(); return; }
                state.message = result == HueRetentionResult.Completed
                    ? (kind == HueRetentionKind.Profile ? "Return through Profile > Minis to get your hint."
                        : "Open the game from your home screen to get your hints.")
                    : "Not completed. You can try again or keep playing.";
                Changed?.Invoke();
                // Only the separate mission-reward response can authorize the grant.
                if (result == HueRetentionResult.Completed) CheckReward(kind);
            });
        }

        private void CheckReward(HueRetentionKind kind)
        {
            var state = State(kind);
            if (state.claimed || state.checking || !state.rewardAvailable) return;
            state.checking = true;
            state.rewardDeadline = Time.realtimeSinceStartup + TimeoutSeconds;
            int request = ++state.rewardRequest;
            platform.CheckReward(kind, result =>
            {
                if (!this || request != state.rewardRequest) return;
                state.checking = false;
                if (result == HueRetentionResult.Eligible)
                {
                    game.TryClaimRetentionReward(kind);
                    state.claimed = game.HasClaimedRetentionReward(kind);
                    if (state.claimed)
                    {
                        state.opening = false;
                        state.openRequest++;
                        state.message = "+" + RewardAmount(kind) + (kind == HueRetentionKind.Profile
                            ? " hint added. Welcome back!" : " hints added. Welcome back!");
                    }
                }
                else if (result == HueRetentionResult.Failed)
                    state.message = "Couldn't check your reward. Try again when you return.";
                Changed?.Invoke();
            });
        }

        private void QueueRefresh() { refreshNextFrame = true; }
        private void OnApplicationFocus(bool focused) { if (focused) QueueRefresh(); }
        private void OnApplicationPause(bool paused) { if (!paused) QueueRefresh(); }
        private void Update()
        {
            if (refreshNextFrame && initialized)
            {
                refreshNextFrame = false;
                // A resume invalidates a query started before backgrounding. An
                // old ineligible response must not mask the new launch's reward.
                foreach (var state in states)
                    if (state.checking) { state.checking = false; state.rewardRequest++; }
                Refresh();
            }
            foreach (var state in states)
            {
                bool expired = false;
                if (state.opening && Time.realtimeSinceStartup >= state.openDeadline)
                {
                    state.opening = false; state.openRequest++; expired = true;
                }
                if (state.checking && Time.realtimeSinceStartup >= state.rewardDeadline)
                {
                    state.checking = false; state.rewardRequest++; expired = true;
                }
                if (expired)
                {
                    state.message = "No response yet. You can retry or keep playing.";
                    Changed?.Invoke();
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var state in states) { state.openRequest++; state.rewardRequest++; }
            if (platform != null) { platform.Resumed -= QueueRefresh; platform.Dispose(); }
        }
    }
}
