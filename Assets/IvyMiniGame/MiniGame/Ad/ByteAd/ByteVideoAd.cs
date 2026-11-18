#if IVY_SDK_DY
using System;

using TTSDK;


namespace IvySdk.MiniGame.Ad
{

    internal class ByteVideoAd : RewardedVideoInterface
    {
        private bool IsRewardShowing = false;

        private Action _showSucceedHook;

        public void OnRewardedAdShowSuccess(Action hook)
        {
            _showSucceedHook = hook;
        }

        public void ShowRewardedVideo(string id, Action<AdEvent, string, int> rlt, bool multiton, string[] multitonRewardMsg, int multitonRewardTime)
        {
            if (IsRewardShowing)
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"already showing", 0);
                return;
            }

            IsRewardShowing = true;

            TTRewardedVideoAd reward_video_ad = TT.CreateRewardedVideoAd(id, (bool isEnded, int count) =>
            {
                IsRewardShowing = false;
                if (isEnded)
                {
                    rlt?.Invoke(AdEvent.REWARD_USER, "", count);
                }
                rlt?.Invoke(AdEvent.CLOSED, "", 0);
            }, (int code, string errMsg) =>
            {
                IsRewardShowing = false;
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"{errMsg}", 0);

                var _obj = new Dictionary<string, object>();
                _obj.TryAdd("ad_format", "rewarded");
                _obj.TryAdd("ad_unit", id);
                _obj.TryAdd("ad_network", "DY");

                EventTracker.TrackEvent("sdk_ad_show_failed", _obj);

            }, multiton, multitonRewardMsg, multitonRewardTime, false);

            reward_video_ad.OnLoad += () =>
            {
                rlt?.Invoke(AdEvent.SHOW_SUCCESS, "", 0);
                reward_video_ad.Show();
                _showSucceedHook?.Invoke();

                var obj = new Dictionary<string, object>();
                obj.TryAdd("ad_format", "rewarded");
                obj.TryAdd("ad_unit", id);
                obj.TryAdd("ad_network", "DY");

                EventTracker.TrackEvent("sdk_ad_shown", obj);
            };

            reward_video_ad.Load();

        }

    }

}

#endif