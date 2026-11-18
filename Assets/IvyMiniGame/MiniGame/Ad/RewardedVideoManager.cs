using IvySdk.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IvySdk.MiniGame.Ad
{

    internal interface RewardedVideoInterface
    {
        ///显示视频广告 
        void ShowRewardedVideo(string id, Action<AdEvent, string, int> rtl, bool multiton = false, string[] multitonRewardMsg = null, int multitonRewardTime = 0);

#if IVY_SDK_TT
        void PreloadRewardedVideo(List<AdConfig> _rewardedConfigs);
#endif

        void OnRewardedAdShowSuccess(Action hook);
    }

    internal class RewardedVideoManager
    {
        private readonly List<AdConfig> _rewardedConfigs;
        private readonly RewardedVideoInterface I;
        internal RewardedVideoManager(List<AdConfig> rewardedConfigs)
        {
            _rewardedConfigs = rewardedConfigs;
#if IVY_SDK_DY
            I = new ByteVideoAd();
#elif IVY_SDK_WX
            I = new WeChatRewardedVideo();
#elif IVY_SDK_TT
            I = new TiktokVideoAd();
            I.PreloadRewardedVideo(_rewardedConfigs);
#endif
            I.OnRewardedAdShowSuccess(OnAdShowSucceed);
        }

        private AdConfig GetRewardedConfig(string tag)
        {
            AdConfig ret = null;
            foreach (var ad in _rewardedConfigs)
            {
                if (ad == null) continue;
                ret = ad;
                if (ad.tag == tag)
                {
                    break;
                }
            }
            return ret;
        }

        private float _lastShowTime = 0f;

        public void ShowRewardedVideo(string tag, Action<AdEvent, string, int> rtl = null, bool multiton = false, string[] multitonRewardMsg = null, int multitonRewardTime = 0)
        {
            var config = GetRewardedConfig(tag);
            if (config == null)
            {
                rtl?.Invoke(AdEvent.SHOW_FAILURE, $"{tag} not found", 0);
                return;
            }

            int ad_interval = GetAdInterval(config);
            if (Time.time - _lastShowTime < ad_interval)
            {
                IvyLog.d($"距离上次展示不足 {ad_interval} 秒，请稍后再试");
                rtl?.Invoke(AdEvent.SHOW_FAILURE, $"ad not ready", 0);
                return;
            }

            I?.ShowRewardedVideo(config.adUnitId, rtl, multiton, multitonRewardMsg, multitonRewardTime);
        }

        internal int GetAdInterval(AdConfig config)
        {
            return config.adIntervals;
        }

        private void OnAdShowSucceed()
        {
            _lastShowTime = Time.time;
        }


    }
}

