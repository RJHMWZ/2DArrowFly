#if IVY_SDK_WX
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeChatWASM;

namespace IvySdk.MiniGame.Ad
{

    internal class WeChatBanner : BannerInterface
    {
        private Dictionary<string, WXCustomAd> _banner_ads = new Dictionary<string, WXCustomAd>();


        private BannerStyle ToBannerStyle(BannerPosition bp)
        {
            return new BannerStyle
            {
                width = 400,
            };
        }

        ///显示banner广告
        public void ShowBanner(AdConfig config, int left, int top, int width, System.Action<AdEvent, string> rlt)
        {
            HideBanner(config.tag);

            var param = new WXCreateCustomAdParam();
            param.adUnitId = config.adUnitId;
            if (config.adIntervals > 0)
            {
                param.adIntervals = config.adIntervals;
            }
            param.style = new CustomStyle { left = left, top = top, width = width };
            var nBanner = WeChatWASM.WX.CreateCustomAd(param);
            _banner_ads.Add(config.tag, nBanner);


            nBanner.OnLoad(resp =>
            {
                nBanner.Show();
                rlt?.Invoke(AdEvent.SHOW_SUCCESS, "");
            });

            nBanner.OnError(resp =>
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"banner {config.adUnitId} not load");
            });
        }

        ///显示banner广告
        public void ShowBanner(AdConfig conf, BannerPosition bp, System.Action<AdEvent, string> rlt)
        {
            var style = ToBannerStyle(bp);
            ShowBanner(conf, style.left, style.top, style.width, rlt);
        }

        ///隐藏banner广告
        public void HideBanner(string tag)
        {
            if (_banner_ads.ContainsKey(tag))
            {
                var banner = _banner_ads[tag];
                if (banner == null) return;
                _banner_ads.Remove(tag);
                banner.Hide();
                banner.Destroy();
                banner = null;
            }
        }
    }

}

#endif