#if IVY_SDK_TT
using IvySdk.Tracker;
using IvySdk.Utils;
using System;
using System.Collections.Generic;
using TTSDK;
using UnityEngine;


namespace IvySdk.MiniGame.Ad
{

    internal class TikTokInterstitialAd : InterstitialInterface
    {
        private bool isInterstitialShowing = false;

        private Dictionary<string, TTInterstitialAd> _interstitialAds = new Dictionary<string, TTInterstitialAd>();

  
        private Action _showSucceedHook;

        public void OnInterstitialAdShowSuccess(Action hook)
        {
                _showSucceedHook = hook;
        }


        public void PreloadInterstitialAd(List<AdConfig> _interstitialConfigs)
        {
            foreach (var item in _interstitialConfigs)
            {
                _interstitialAds.TryGetValue(item.adUnitId, out TTInterstitialAd _ad);
                if (_ad == null)
                {
                    PreloadAd(item.adUnitId);
                }
            }
        }


        private void PreloadAd(string adUnitId)
        {
            CreateInterstitialAdParam param = new CreateInterstitialAdParam
            {
                InterstitialAdId = adUnitId
            };
            TTInterstitialAd _ad = TT.CreateInterstitialAd(param);
            _interstitialAds.Add(adUnitId, _ad);
        }


        public void ShowInterstitial(string id, Action<AdEvent, string> rlt)
        {
            if (isInterstitialShowing)
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"already showing");
                return;
            }

            isInterstitialShowing = true;

            _interstitialAds.TryGetValue(id, out TTInterstitialAd _interstitial_ad);
            _interstitialAds.Remove(id);
            if ( _interstitial_ad == null)
            {
                isInterstitialShowing = false;
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"ad not ready");

                var _obj = new Dictionary<string, object>();
                _obj.TryAdd("ad_format", "interstitial");
                _obj.TryAdd("ad_unit", id);
                _obj.TryAdd("ad_network", "TT");

                EventTracker.TrackEvent("sdk_ad_show_failed", _obj);
                return;
            }

            _interstitial_ad.OnError += (sender, e) =>
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"load failed");
                isInterstitialShowing = false;
                PreloadAd(id);
                var obj = new Dictionary<string, object>();
                obj.TryAdd("ad_format", "interstitial");
                obj.TryAdd("ad_unit", id);
                obj.TryAdd("ad_network", "TT");

                EventTracker.TrackEvent("sdk_ad_show_failed", obj);
            };
            _interstitial_ad.OnClose += () =>
            {
                isInterstitialShowing = false;
                _interstitial_ad.Destroy();
                _interstitial_ad = null;
                rlt?.Invoke(AdEvent.CLOSED, "");
                PreloadAd(id);
                var obj = new Dictionary<string, object>();
                obj.TryAdd("ad_format", "interstitial");
                obj.TryAdd("ad_unit", id);
                obj.TryAdd("ad_network", "TT");

                EventTracker.TrackEvent("sdk_ad_closed", obj);
            };
            rlt?.Invoke(AdEvent.SHOW_SUCCESS, "");
            _interstitial_ad.Show();
            _showSucceedHook?.Invoke();
            var obj = new Dictionary<string, object>();
            obj.TryAdd("ad_format", "interstitial");
            obj.TryAdd("ad_unit", id);
            obj.TryAdd("ad_network", "TT");

            EventTracker.TrackEvent("sdk_ad_shown", obj);
        }
    }

}
#endif