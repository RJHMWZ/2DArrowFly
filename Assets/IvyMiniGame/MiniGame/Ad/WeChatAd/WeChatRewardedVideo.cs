using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IvySdk.MiniGame.Ad
{
#if IVY_SDK_WX
    internal class WeChatRewardedVideo : RewardedVideoInterface
    {
        private bool IsRewardShowing = false;

        private Action _showSucceedHook;

        public void OnRewardedAdShowSuccess(Action hook)
        {
            _showSucceedHook = hook;
        }


        public void ShowRewardedVideo(string id, Action<AdEvent, string, int> rtl, bool multiton = false, string[] multitonRewardMsg = null, int multitonRewardTime = 0)
        {
            if (IsRewardShowing)
            {
                rtl?.Invoke(AdEvent.SHOW_FAILURE, "rewarded video is showing", 0);
                return;
            }

            IsRewardShowing = true;
            var ad = WeChatWASM.WX.CreateRewardedVideoAd(new WeChatWASM.WXCreateRewardedVideoAdParam
            {
                adUnitId = id
            });
            ad.OnLoad(resp =>
            {
                rtl?.Invoke(AdEvent.SHOW_SUCCESS, "", 0);
                ad.Show();
                _showSucceedHook?.Invoke();

                var obj = new Dictionary<string, object>();
                obj.TryAdd("ad_format", "rewarded");
                obj.TryAdd("ad_unit", id);
                obj.TryAdd("ad_network", "WX");

                EventTracker.TrackEvent("sdk_ad_shown", obj);

            });
            ad.OnError(resp =>
            {
                IsRewardShowing = false;
                rtl?.Invoke(AdEvent.SHOW_FAILURE, $"rewarded video {id} not load", 0);

                var _obj = new Dictionary<string, object>();
                _obj.TryAdd("ad_format", "rewarded");
                _obj.TryAdd("ad_unit", id);
                _obj.TryAdd("ad_network", "WX");

                EventTracker.TrackEvent("sdk_ad_show_failed", _obj);
            });
            ad.OnClose(resp =>
            {
                IsRewardShowing = false;
                if (resp != null && resp.isEnded)
                {
                    rtl?.Invoke(AdEvent.REWARD_USER, "", 1);
                }
                rtl?.Invoke(AdEvent.CLOSED, "", 0);

                var obj = new Dictionary<string, object>();
                obj.TryAdd("ad_format", "rewarded");
                obj.TryAdd("ad_unit", id);
                obj.TryAdd("ad_network", "WX");

                EventTracker.TrackEvent("sdk_ad_closed", obj);
            });
        }

       
    }
#endif

}


