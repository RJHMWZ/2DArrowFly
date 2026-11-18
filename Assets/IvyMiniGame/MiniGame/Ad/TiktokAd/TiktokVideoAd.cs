#if IVY_SDK_TT
using IvySdk.Tracker;
using System;
using System.Collections.Generic;
using TTSDK;


namespace IvySdk.MiniGame.Ad
{

    internal class TiktokVideoAd : RewardedVideoInterface
    {
        private bool IsRewardShowing = false;
            
        private Dictionary<string, TTRewardedVideoAd> _rewardedAds = new Dictionary<string, TTRewardedVideoAd> ();

        private Action _showSucceedHook;

        public void OnRewardedAdShowSuccess(Action hook)
        {
            _showSucceedHook = hook;
        }

        public void PreloadRewardedVideo(List<AdConfig> _rewardedConfigs)
        {
            foreach (var item in _rewardedConfigs)
            {
                _rewardedAds.TryGetValue(item.adUnitId, out TTRewardedVideoAd _videoAd);
                if (_videoAd == null) {
                    PreloadAd(item.adUnitId);     
                }
            }
        }

        private void PreloadAd(string adUnitId) {
            CreateRewardedVideoAdParam param = new CreateRewardedVideoAdParam
            {
                AdUnitId = adUnitId
            };
            TTRewardedVideoAd reward_video_ad = TT.CreateRewardedVideoAd(param);
            _rewardedAds.Add(adUnitId, reward_video_ad);
        }


        public void ShowRewardedVideo(string id, Action<AdEvent, string, int> rlt, bool multiton, string[] multitonRewardMsg, int multitonRewardTime)
        {
            if (IsRewardShowing)
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"already showing", 0);
                return;
            }

            IsRewardShowing = true;

            _rewardedAds.TryGetValue(id, out TTRewardedVideoAd reward_video_ad);
            _rewardedAds.Remove(id);
            if (reward_video_ad == null) {
                IsRewardShowing = false;
                rlt?.Invoke(AdEvent.SHOW_FAILURE, "ad not ready", 0);

                var _obj = new Dictionary<string, object>();
                _obj.TryAdd("ad_format", "rewarded");
                _obj.TryAdd("ad_unit", id);
                _obj.TryAdd("ad_network", "TT");

                EventTracker.TrackEvent("sdk_ad_show_failed", _obj);
                return;
            }
            reward_video_ad.OnError += (sender, e) =>
            {
                IsRewardShowing = false;
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"{e}", 0);
                PreloadAd(id);

                var obj = new Dictionary<string, object>();
                obj.TryAdd("ad_format", "rewarded");
                obj.TryAdd("ad_unit", id);
                obj.TryAdd("ad_network", "TT");

                EventTracker.TrackEvent("sdk_ad_show_failed", obj);
                return;
            };
            reward_video_ad.OnClose += (bool isEnded) => {
                IsRewardShowing = false;
                if (isEnded)
                {
                    rlt?.Invoke(AdEvent.REWARD_USER, "", 1);
                }
                rlt?.Invoke(AdEvent.CLOSED, "", 0);
                PreloadAd(id);

                var obj = new Dictionary<string, object>();
                obj.TryAdd("ad_format", "rewarded");
                obj.TryAdd("ad_unit", id);
                obj.TryAdd("ad_network", "TT");

                EventTracker.TrackEvent("sdk_ad_closed", obj);
            };
            rlt?.Invoke(AdEvent.SHOW_SUCCESS, "", 0);
            reward_video_ad?.Show();
            _showSucceedHook?.Invoke();

            var obj = new Dictionary<string, object>();
            obj.TryAdd("ad_format", "rewarded");
            obj.TryAdd("ad_unit", id);
            obj.TryAdd("ad_network", "TT");

            EventTracker.TrackEvent("sdk_ad_shown", obj);
        }

    }

}

#endif