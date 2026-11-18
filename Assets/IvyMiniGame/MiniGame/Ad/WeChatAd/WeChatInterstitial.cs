using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IvySdk.MiniGame.Ad
{
#if IVY_SDK_WX
    internal class WeChatInterstitial : InterstitialInterface
    {
        private bool isInterstitialShowing = false;

        private Action _showSucceedHook;

        public void OnInterstitialAdShowSuccess(Action hook)
        {
            _showSucceedHook = hook;
        }

        public void ShowInterstitial(string id, Action<AdEvent, string> rlt)
        {
            if (isInterstitialShowing)
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, "Interstitial is showing");
                return;
            }

            isInterstitialShowing = true;
            var ad = WeChatWASM.WX.CreateInterstitialAd(new WeChatWASM.WXCreateInterstitialAdParam
            {
                adUnitId = id
            });

            ad.OnLoad(resp =>
            {
                rlt?.Invoke(AdEvent.SHOW_SUCCESS, "");
                ad.Show();
                _showSucceedHook?.Invoke();

                var obj = new Dictionary<string, object>();
                obj.TryAdd("ad_format", "interstitial");
                obj.TryAdd("ad_unit", id);
                obj.TryAdd("ad_network", "TT");

                EventTracker.TrackEvent("sdk_ad_shown", obj);
            });
            ad.OnError(resp =>
            {
                isInterstitialShowing = false;
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"Interstitial {id} not load");

                var _obj = new Dictionary<string, object>();
                _obj.TryAdd("ad_format", "interstitial");
                _obj.TryAdd("ad_unit", id);
                _obj.TryAdd("ad_network", "WX");

                EventTracker.TrackEvent("sdk_ad_show_failed", _obj);
            });
            ad.OnClose(() =>
            {
                isInterstitialShowing = false;
                rlt?.Invoke(AdEvent.CLOSED, "");

                var obj = new Dictionary<string, object>();
                obj.TryAdd("ad_format", "interstitial");
                obj.TryAdd("ad_unit", id);
                obj.TryAdd("ad_network", "WX");

                EventTracker.TrackEvent("sdk_ad_closed", obj);
            });
        }
    }
#endif
}

