using IvySdk.Tracker;
using System;
using System.Collections.Generic;


namespace IvySdk.MiniGame
{
    public static class AdManager
    {
        private static Ad.BannerManager _bannerManager;
        private static Ad.RewardedVideoManager _rewardedVideoManager;
        private static Ad.InterstitialManager _interstitialManager;

        public static void InitAdManager(IvySDKConfig.PlatformConfig config)
        {
            _bannerManager = new Ad.BannerManager(config.bannerAds);
            _rewardedVideoManager = new Ad.RewardedVideoManager(config.rewardedAds);
            _interstitialManager = new Ad.InterstitialManager(config.interstitialAds);

            EventTracker.TrackEvent("sdk_ads_init", new Dictionary<string, object>());
        }

        #region 广告

        public static void ShowBanner(string tag, BannerPosition bp, Action<AdEvent, string> rlt = null)
        {
            _bannerManager?.ShowBanner(tag, bp, rlt);
        }

        public static void ShowBanner(string tag, int left, int top, int width, Action<AdEvent, string> rlt = null)
        {
            _bannerManager?.ShowBanner(tag, left, top, width, rlt);
        }

        public static void HideBanner(string tag)
        {
            _bannerManager?.HideBanner(tag);
        }

        public static void ShowRewardedVideo(string tag, Action<AdEvent, string, int> rtl = null, bool multiton = false, string[] multitonRewardMsg = null, int multitonRewardTime = 0)
        {
            _rewardedVideoManager?.ShowRewardedVideo(tag, rtl, multiton, multitonRewardMsg, multitonRewardTime);
        }

        public static bool HasInterstitial()
        {
            return true;
        }

        public static void ShowInterstitial()
        {
            _interstitialManager?.ShowInterstitial("main");
        }

        public static void ShowInterstitial(string tag, Action<AdEvent, string> rlt = null)
        {
            _interstitialManager?.ShowInterstitial(tag, rlt);
        }


        public static bool HasRewardAd()
        {
            return true;
        }

       

        #endregion
    }
}