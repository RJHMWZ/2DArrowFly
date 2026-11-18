#if IVY_SDK_TT
using UnityEngine;
using System.Collections.Generic;

using TTSDK;


namespace IvySdk.MiniGame.Ad
{

    internal class TiktokBannerAd : BannerInterface
    {
        //private int px2dp(int px) => (int)(px * (160 / Screen.dpi));

        private Dictionary<string, TTBannerAd> _banner_ads = new Dictionary<string, TTBannerAd>();


        //private TTBannerStyle ToBannerStyle(BannerPosition bp)
        //{
        //    return new TTBannerStyle
        //    {
        //        top = 100,
        //        left = 10,
        //        width = 320,
        //    };
        //}
        
        ///显示banner广告
        public void ShowBanner(AdConfig conf, int left, int top, int width, System.Action<AdEvent, string> rlt)
        {

            rlt?.Invoke(AdEvent.SHOW_FAILURE, "");

            //HideBanner(conf.tag);
            
            //var param = new CreateBannerAdParam();
            //param.BannerAdId = conf.adUnitId;

            //param.Style = new TTBannerStyle
            //{
            //    top = top,
            //    left = left,
            //    width = width,
            //};

            //if (conf.adIntervals > 0)
            //{
            //    param.AdIntervals = conf.adIntervals;
            //}
            
            //TTBannerAd _banner_ad =  TT.CreateBannerAd(param);

            //_banner_ads.Add(conf.tag, _banner_ad);
            //_banner_ad.OnLoad += () =>
            //{
            //    if (_banner_ad != null)
            //    {
            //        _banner_ad.Show();
            //    }
            //};
        }

        ///显示banner广告
        public void ShowBanner(AdConfig conf, BannerPosition bp, System.Action<AdEvent, string> rlt)
        {
            rlt?.Invoke(AdEvent.SHOW_FAILURE, "");
            //var style = ToBannerStyle(bp);
            //ShowBanner(conf, style.left, style.top, style.width, rlt);
        }

        ///隐藏banner广告
        public void HideBanner(string tag)
        {
            
            //TTBannerAd _banner_ad = _banner_ads[tag];
            //if (_banner_ad == null) return;
            //_banner_ads.Remove(tag);
            //_banner_ad.Hide();
            //_banner_ad.Destroy();
            //_banner_ad = null;
        }
    }

}

#endif