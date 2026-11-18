#if IVY_SDK_DY
using System;

using TTSDK;


namespace IvySdk.MiniGame.Ad
{

    internal class ByteInterstitialAd : InterstitialInterface
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
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"already showing");
                return;
            }

            isInterstitialShowing = true;

            var param = new CreateInterstitialAdParam();
            param.InterstitialAdId = id;
            TTInterstitialAd _interstitial_ad = TT.CreateInterstitialAd(param);
            _interstitial_ad.OnError += (sender, e) =>
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, $"load failed");
                isInterstitialShowing = false;

                var _obj = new Dictionary<string, object>();
                _obj.TryAdd("ad_format", "interstitial");
                _obj.TryAdd("ad_unit", id);
                _obj.TryAdd("ad_network", "DY");

                EventTracker.TrackEvent("sdk_ad_show_failed", _obj);

            };
            _interstitial_ad.OnClose += () =>
            {
                isInterstitialShowing = false;
                _interstitial_ad.Destroy();
                _interstitial_ad = null;
                rlt?.Invoke(AdEvent.CLOSED, "");

                var obj = new Dictionary<string, object>();
                obj.TryAdd("ad_format", "interstitial");
                obj.TryAdd("ad_unit", id);
                obj.TryAdd("ad_network", "TT");

                EventTracker.TrackEvent("sdk_ad_closed", obj);
            };
            _interstitial_ad.OnLoad += () =>
            {
                rlt?.Invoke(AdEvent.SHOW_SUCCESS, "");
                _interstitial_ad.Show();
                _showSucceedHook?.Invoke();

                 var obj = new Dictionary<string, object>();
                obj.TryAdd("ad_format", "interstitial");
                obj.TryAdd("ad_unit", id);
                obj.TryAdd("ad_network", "TT");

                EventTracker.TrackEvent("sdk_ad_shown", obj);
            };
            _interstitial_ad.Load();

        }
    }

}
#endif