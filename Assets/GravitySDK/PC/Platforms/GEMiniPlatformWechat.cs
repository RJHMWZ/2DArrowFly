using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GravitySDK.PC.Utils;
using GravitySDK.PC.GravityTurbo;
using System;
using GravitySDK.PC.Constant;


//#define GRAVITY_WECHAT_GAME_MODE
//#define ENABLE_TENCENT_SDK_TRACK

//#define GRAVITY_BILIBILI_GAME_MODE
//#define GRAVITY_MEITUAN_GAME_MODE


#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
using WeChatWASM;
#endif


#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
using System.Runtime.InteropServices;

/**
 * 小游戏 DN SDK 辅助类
 */
public class DnSdkHelper
{
    [DllImport("__Internal")]
    public static extern void SetOpenId(string openid);

    [DllImport("__Internal")]
    public static extern object onPurchase(int purchaseValue);

    [DllImport("__Internal")]
    public static extern object onRegister();

    [DllImport("__Internal")]
    public static extern void onReActive(int backFlowDay);

    [DllImport("__Internal")]
    public static extern object onAddToWishlist(string type);

    [DllImport("__Internal")]
    public static extern void onShare(string target);

    [DllImport("__Internal")]
    public static extern object onCreateRole(string roleName);

    [DllImport("__Internal")]
    public static extern object onTutorialFinish();

    [DllImport("__Internal")]
    public static extern void onUpdateLevel(int level, int power);

    [DllImport("__Internal")]
    public static extern void onViewContent(string item);
}

#endif


namespace GravityEngine.Platform
{
    public class GEMiniPlatformWechat : GEMiniPlatformBase
    {

        public static bool activity()
        {
#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
            return true;
#else
            return false;
#endif
        }

        public static bool IsWechat()
        {
#if GRAVITY_WECHAT_GAME_MODE
            return true;
#else
            return false;
#endif
        }
        public static bool IsBilibili()
        {
#if GRAVITY_BILIBILI_GAME_MODE
            return true;
#else
            return false;
#endif
        }

        public static bool canTrack()
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            return true;
#else
            return false;
#endif
        }

        public override void SetDeviceInfo()
        {
#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
            // 从微信SDK获取属性信息
            var systemInfo = WX.GetSystemInfoSync();
            // 提前设置设备属性信息
            GravitySDKDeviceInfo.SetMiniGameDeviceInfo(new GEMiniGameDeviceInfo()
            {
                SDKVersion = systemInfo.SDKVersion, // 微信SDK版本号
                brand = systemInfo.brand,
                language = systemInfo.language,
                model = systemInfo.model,
                platform = systemInfo.platform,
                screenHeight = systemInfo.screenHeight,
                screenWidth = systemInfo.screenWidth,
                system = systemInfo.system,
                version = systemInfo.version, // 微信版本号
            });
#endif
        }

        public override void OnGravityStartEnd()
        {
#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
            // 记录小程序/小游戏启动事件，在StartEngine并且获取network_type之后调用
            WX.GetNetworkType(new GetNetworkTypeOption()
            {
                success = (result) => { GravitySDKDeviceInfo.SetNetworkType(result.networkType); },
                fail = (result) => { GravitySDKDeviceInfo.SetNetworkType("error"); },
                complete = (result) =>
                {
                    LaunchOptionsGame launchOptionsSync = WX.GetLaunchOptionsSync();
                    GravityEngineAPI.TrackMPLaunch(launchOptionsSync.query, "" + launchOptionsSync.scene);
                    GravityEngineAPI.ReportSuperProperties("" + launchOptionsSync.scene);
                }
            });
#endif
        }



        public override string platformStr()
        {
#if GRAVITY_WECHAT_GAME_MODE
            return "wx";
#elif GRAVITY_BILIBILI_GAME_MODE
            return "bili";
#else
            return null;
#endif
        }


        public override void GetLaunchOptionsSync(Action<Dictionary<string, string>, string> callBack)
        {
#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
            LaunchOptionsGame launchOptionsSync = WX.GetLaunchOptionsSync();
            var launchQuery = launchOptionsSync.query;
            var launchScene = "" + launchOptionsSync.scene;
            callBack(launchQuery,launchScene);
#endif
        }




        public override void addShowAutoTrack(Func<string, Dictionary<string, object>> propertiesCallback)
        {
#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
            WX.OnShow((result =>
            {
                GravitySDKLogger.Print("wechat game on show");
                Dictionary<string, object> properties = propertiesCallback(AUTO_TRACK_EVENTS.MP_SHOW.ToString());
                GravityEngineAPI.TrackMPShow(result.query, "" + result.scene, properties);
            }));
#endif
        }

        public override void addHideAutoTrack(Func<string, Dictionary<string, object>> propertiesCallback)
        {
#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
            WX.OnHide((result =>
            {
                GravitySDKLogger.Print("wechat game on hide");
                Dictionary<string, object> properties = propertiesCallback(AUTO_TRACK_EVENTS.MP_HIDE.ToString());
                GravityEngineAPI.TrackMPHide(properties);
            }));
#endif
        }


        public override void addFavoriteAutoTrack(Func<string, Dictionary<string, object>> propertiesCallback)
        {
//#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
//            WX.OnAddToFavorites((action =>
//            {
//                GravitySDKLogger.Print("wechat game on add to favorites");
//                Dictionary<string, object> properties = propertiesCallback(AUTO_TRACK_EVENTS.MP_ADD_TO_FAVORITES.ToString());
//                GravityEngineAPI.TrackMPAddFavorites(properties);
//            }));
//#endif
        }

        public override void addShareAutoTrack(Func<string, Dictionary<string, object>> propertiesCallback)
        {
//#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
//            WXShareAppMessageParam param = new WXShareAppMessageParam();
//            WX.OnShareAppMessage(param, action =>
//            {
//                action.Invoke(param);
//                Dictionary<string, object> properties = propertiesCallback(AUTO_TRACK_EVENTS.MP_SHARE.ToString());
//                GravityEngineAPI.TrackMPShare("APP_MESSAGE", properties);
//            });
//            WX.OnShareTimeline(action =>
//            {
//                Dictionary<string, object> properties = propertiesCallback(AUTO_TRACK_EVENTS.MP_SHARE.ToString());
//                GravityEngineAPI.TrackMPShare("TIME_LINE", properties);
//            });
//#endif
        }


        public override void SetOpenId(string openId)
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            DnSdkHelper.SetOpenId(openId);
            GravityHelper.QueryAppConfigInfo(GravityHelper.UseParams());
#endif
        }

        /// <summary>
        /// 在用户分享小游戏的时候上报，需区分是【转发给朋友】还是【分享到朋友圈】
        /// </summary>
        /// <param name="target">shareType 分享类型：APP_MESSAGE / TIME_LINE</param>
        /// <param name="properties">默认传null</param>
        public override void TrackMPShare(string target, Dictionary<string, object> properties = null)
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            DnSdkHelper.onShare(target);
            Debug.Log("tencent sdk onShare " + target);
#endif
        }

        /// <summary>
        /// 在用户收藏小游戏的时候上报，包括收藏、添加到我的小程序、添加到桌面以及小游戏自己定义的收藏逻辑。
        /// </summary>
        /// <param name="wishType">wishType 类型枚举值：普通收藏（default）/添加到我的小程序（my）/添加到桌面（desktop）/其他</param>
        public override object TrackMPAddFavorites(string wishType = "default")
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            Debug.Log("tencent sdk onAddToWishlist " + wishType);
            return DnSdkHelper.onAddToWishlist(wishType);
#else
            return null;
#endif
        }

        /// <summary>
        /// 用户在小游戏内创建角色成功后上报 CREATE_ROLE 行为，可添加创建角色相关的自定义参数，如角色名等。
        /// </summary>
        /// <param name="roleName">角色名</param>
        public override object TrackMPCreateRole(string roleName)
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            Debug.Log("tencent sdk onCreateRole " + roleName);
            return DnSdkHelper.onCreateRole(roleName);
#else
            return null;
#endif
        }

        /// <summary>
        /// 在用户完成游戏新手指引教程或者完成教程关卡后上报 TUTORIAL_FINISH 行为。
        /// </summary>
        public override object TrackMPTutorialFinish()
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            Debug.Log("tencent sdk onTutorialFinish");
            return DnSdkHelper.onTutorialFinish();
#else
            return null;
#endif
        }


        /// <summary>
        /// 用户在小游戏内的完成游戏等级提升时上报 UPDATE_LEVEL 行为，可添加自定义参数，如当前游戏等级、游戏能量等。
        /// </summary>
        /// <param name="level">游戏等级</param>
        /// <param name="power">游戏能量</param>
        public override void TrackMPUpdateLevel(int level, int power)
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            DnSdkHelper.onUpdateLevel(level, power);
            Debug.Log("tencent sdk onUpdateLevel " + level + " " + power);
#endif
        }


        /// <summary>
        /// 用户在小游戏内浏览商城页面时上报。
        /// </summary>
        public override void TrackMPViewMallContent()
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            DnSdkHelper.onViewContent("Mall");
            Debug.Log("tencent sdk onViewContentMall");
#endif
        }

        /// <summary>
        /// 用户在小游戏内浏览活动页面时上报。
        /// </summary>
        public override void TrackMPViewActivityContent()
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            DnSdkHelper.onViewContent("Activity");
            Debug.Log("tencent sdk onViewContentActivity");
#endif
        }

        /// <summary>
        /// 上报付费事件到腾讯SDK
        /// </summary>
        /// <param name="amount">付费金额 单位为分</param>
        public override object onPurchase(int amount)
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            // 付费
            Debug.Log("tencent sdk onPurchase " + amount);
            return DnSdkHelper.onPurchase(amount);
#else
            return null;
#endif
        }

        public override object onRegister()
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            Debug.Log("tencent sdk onRegister");
            return DnSdkHelper.onRegister();
#else
            return null;
#endif
        }

        public override void onReActive(int backFlowDay)
        {
#if GRAVITY_WECHAT_GAME_MODE && ENABLE_TENCENT_SDK_TRACK
            DnSdkHelper.onReActive(backFlowDay);
            Debug.Log("tencent sdk onReActive " + backFlowDay);
#endif
        }


        public override void SaveData(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            if (value == null)
            {
                return;
            }
#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
            if (value.GetType() == typeof(int))
            {
                PlayerPrefs.SetInt(key, (int)value);
            }
            else if (value.GetType() == typeof(float))
            {
                PlayerPrefs.SetFloat(key, (float)value);
            }
            else if (value.GetType() == typeof(string))
            {
                PlayerPrefs.SetString(key, (string)value);
            }
            PlayerPrefs.Save();
#endif
        }

        public override object GetData(string key, Type type)
        {
#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
            if (!string.IsNullOrEmpty(key) && PlayerPrefs.HasKey(key))
            {
                if (type == typeof(int))
                {
                    return PlayerPrefs.GetInt(key);
                }
                else if (type == typeof(float))
                {
                    return PlayerPrefs.GetFloat(key);
                }
                else if (type == typeof(string))
                {
                    return PlayerPrefs.GetString(key);
                }
                PlayerPrefs.Save();
            }
#endif
            return null;
        }
        public override void DeleteData(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
            }
#endif
        }
        public override bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
            return PlayerPrefs.HasKey(key);
#else
            return false;
#endif
        }
    }
}