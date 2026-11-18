
using System.Collections.Generic;
using UnityEngine;
using System;
using IvySdk.Tracker;

namespace IvySdk.MiniGame.Ad
{

    internal class BannerStyle
    {
        internal int left;
        internal int top;
        internal int width;
        internal int height;
    }

    internal interface BannerInterface
    {
        ///显示banner广告
        void ShowBanner(AdConfig conf, int left, int top, int width, Action<AdEvent, string> rlt);

        ///显示banner广告
        void ShowBanner(AdConfig conf, BannerPosition bp, Action<AdEvent, string> rlt);

        ///隐藏banner广告
        void HideBanner(string tag);
    }

    internal class BannerManager
    {
        private readonly List<AdConfig> _bannerConfigs;
        private readonly BannerInterface I;

        public BannerManager(List<AdConfig> bannerConfigs)
        {
            _bannerConfigs = bannerConfigs;
#if IVY_SDK_DY
            I = new ByteBannerAd();
#elif IVY_SDK_WX
            I = new WeChatBanner();
#elif IVY_SDK_TT
            I = new TiktokBannerAd();
#endif
        }

        private AdConfig GetBannerConfig(string tag)
        {
            AdConfig ret = null;
            foreach (var ad in _bannerConfigs)
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

        internal void ShowBanner(string tag, BannerPosition bp, Action<AdEvent, string> rlt = null)
        {
            var config = GetBannerConfig(tag);
            if (config == null)
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"{tag} not found");
                return;
            }
            I?.ShowBanner(config, bp, rlt);
        }


        internal void ShowBanner(string tag, int left, int top, int width, Action<AdEvent, string> rlt = null)
        {
            var config = GetBannerConfig(tag);
            if (config == null)
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"{tag} not found");
                return;
            }
            I?.ShowBanner(config, left, top, width, rlt);
        }

        internal void HideBanner(string tag)
        {
            I?.HideBanner(tag);
        }
    }
}

