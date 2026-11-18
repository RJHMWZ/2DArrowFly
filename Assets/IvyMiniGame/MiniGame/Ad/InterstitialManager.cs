
using IvySdk.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace IvySdk.MiniGame.Ad
{
 
    internal interface InterstitialInterface
    {
        ///显示插屏广告
        void ShowInterstitial(string id, System.Action<AdEvent, string> rlt);
#if IVY_SDK_TT           
        void PreloadInterstitialAd(List<AdConfig> _interstitialConfigs);
#endif

        void OnInterstitialAdShowSuccess(Action hook);

    }

    internal class InterstitialManager
    {
        private readonly List<AdConfig> _interstitialConfigs;
        private readonly InterstitialInterface I;


        internal InterstitialManager(List<AdConfig> interstitialConfigs)
        {
            _interstitialConfigs = interstitialConfigs;
#if IVY_SDK_DY
            I = new ByteInterstitialAd();
#elif IVY_SDK_WX
            I = new WeChatInterstitial();
#elif IVY_SDK_TT
            I = new TikTokInterstitialAd();
            I.PreloadInterstitialAd(_interstitialConfigs);
#endif
            I.OnInterstitialAdShowSuccess(OnAdShowSucceed);
        }

        private AdConfig GetInterstitialConfig(string tag)
        {
            AdConfig ret = null;
            foreach (var ad in _interstitialConfigs)
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

        internal void ShowInterstitial(string tag, Action<AdEvent, string> rlt = null)
        {
            var config = GetInterstitialConfig(tag);
            if (config == null)
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"{tag} not found");
                return;
            }
            int ad_interval = GetAdInterval(config);

            if (Time.time - _lastShowTime < ad_interval)
            {
                IvyLog.d($"距离上次展示不足 {ad_interval} 秒，请稍后再试");
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"ad not ready");
                return;
            }

            I?.ShowInterstitial(config.adUnitId, rlt);
        }

        internal int GetAdInterval(AdConfig config)
        {
#if IVY_SDK_DY || IVY_SDK_TT
            if (config.adIntervals > 30)
            {
                return config.adIntervals;
            }
            return 30;
#else
            return config.adIntervals;
#endif
        }

        private void OnAdShowSucceed()
        {
            _lastShowTime = Time.time;
        }


    }
}


