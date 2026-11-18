using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "IvySDKConfig", menuName = "IvySDK/Config")]
public class IvySDKConfig : ScriptableObject
{
    [System.Serializable]
    public class PlatformConfig
    {
        public bool enable;
        public string appName;
        public string appId;
        public string channel;
        public string versionCode;
        public string versionName;
        public string packageName;

        public bool debug;

        // 仅微信使用（抖音忽略）
        public string miMasterAppId; // 米大师应用ID
        //引力access token
        public string gravityAccessToken;


        public List<AdConfig> bannerAds = new List<AdConfig>();
        public List<AdConfig> interstitialAds = new List<AdConfig>();
        public List<AdConfig> rewardedAds = new List<AdConfig>();

    }

    public PlatformConfig wx = new PlatformConfig();
    public PlatformConfig dy = new PlatformConfig();
    public PlatformConfig tt = new PlatformConfig();
}