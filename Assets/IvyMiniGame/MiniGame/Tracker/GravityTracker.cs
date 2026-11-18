#if IVY_SDK_WX || IVY_SDK_DY || IVY_SDK_TT
using IvySdk.Tracker;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GravityEngine;
using IvySdk.Utils;
using GravitySDK.PC.Constant;




#if  GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
using WeChatWASM;
#elif GRAVITY_BYTEDANCE_GAME_MODE || GRAVITY_BYTEDANCE_CLOUD_MODE
using StarkSDKSpace;
#elif GRAVITY_BYTEDANCE_TT_GAME_MODE
using TTSDK;
#endif

//引力
namespace IvySdk.Attribute
{
    public class GravityTracker
    {

        private class MyInitializeCallback : IInitializeCallback
        {
            public void onFailed(string errorMsg)
            {
                Debug.LogError("初始化失败: " + errorMsg);
            }

            public void onSuccess(Dictionary<string, object> responseJson)
            {
                Debug.Log("初始化成功");
                foreach (var kv in responseJson)
                {
                    Debug.Log($"{kv.Key}: {kv.Value}");
                }
            }
        }

        private bool is_initialized = false;


        public void InitWithOpenId(string openId, IvySDKConfig.PlatformConfig platformConfig)
        {
            if (is_initialized) return;
#if GRAVITY_WECHAT_GAME_MODE || GRAVITY_BILIBILI_GAME_MODE || GRAVITY_MEITUAN_GAME_MODE
            //微信小游戏示例
            //设置实例参数并启动引擎，将以下三个参数修改成您应用对应的参数，参数可以在引力后台--管理中心--应用管理中查看
             string accessToken = platformConfig.gravityAccessToken;
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(openId))
            {
                IvyLog.e($"invalid param for init gravity engine: {openId}; {accessToken}");
                return;
            }

             // 启动引力引擎
            GravityEngineAPI.Token token = new GravityEngineAPI.Token(accessToken, openId, platformConfig.channel);
            //设置前台会话阀值（频繁切换前后台，AppEnd时间会很小，可以设置这个值忽略这种操作）当AppEnd发生时，事件少于设置的time，会忽略计时
            token.foregroundSessionThreshold = 10;
            token.mode = GravityEngineAPI.SDKRunMode.NORMAL;
            token.enableIMEI = true;
            token.enableOAID = true;
            token.enableAndroidId = true;
            token.enableMAC = true;
            // 启动引力引擎
            GravityEngineAPI.StartGravityEngine(token);
            // 微信小游戏开启自动采集，并设置自定属性
            GravityEngineAPI.EnableAutoTrack(AUTO_TRACK_EVENTS.MINI_GAME_ALL, new Dictionary<string, object>()
            {
                {"auto_track_key", "auto_track_value"} // 静态属性
            });
            is_initialized = true;
            GravityEngineAPI.Initialize(openId, openId, int.Parse(platformConfig.versionCode), openId, false, new MyInitializeCallback());
#elif GRAVITY_BYTEDANCE_GAME_MODE || GRAVITY_BYTEDANCE_CLOUD_MODE || GRAVITY_BYTEDANCE_TT_GAME_MODE
            //抖音小游戏示例
            //设置实例参数并启动引擎，将以下三个参数修改成您应用对应的参数，参数可以在引力后台--管理中心--应用管理中查看
            string accessToken = platformConfig.gravityAccessToken;
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(openId))
            {
                IvyLog.e($"invalid param for init gravity engine: {openId}; {accessToken}");
                return;
            }

            // 启动引力引擎
            GravityEngineAPI.Token token = new GravityEngineAPI.Token(accessToken, openId, platformConfig.channel);
            //设置前台会话阀值（频繁切换前后台，AppEnd时间会很小，可以设置这个值忽略这种操作）当AppEnd发生时，事件少于设置的time，会忽略计时
            token.foregroundSessionThreshold = 10;
            token.mode = GravityEngineAPI.SDKRunMode.NORMAL;
            token.enableIMEI = true;
            token.enableOAID = true;
            token.enableAndroidId = true;
            token.enableMAC = true;
            token.clientId = openId;
#if IVY_SDK_DY
            token.isChina = true;
#elif IVY_SDK_TT
            token.isChina = false;
#endif
            // 启动引力引擎
            GravityEngineAPI.StartGravityEngine(token);

            // 抖音小游戏开启自动采集，并设置自定属性
            GravityEngineAPI.EnableAutoTrack(AUTO_TRACK_EVENTS.MINI_GAME_ALL, new Dictionary<string, object>()
            {
                {"auto_track_key", "auto_track_value"} // 静态属性
            });
            is_initialized = true;
            GravityEngineAPI.Initialize(openId, openId, int.Parse(platformConfig.versionCode), openId, false, new MyInitializeCallback());
#endif
        }

        public void Login(string open_id)
        {
            if (is_initialized)
            {
                GravityEngineAPI.Login(open_id);
            }
        }

        public void ReportRegister()
        {
            if (!is_initialized) return;
            GravityEngineAPI.TrackMPRegister();
            GravityEngineAPI.TrackMPLogin();
        }

        public void ReportPayment(int payAmount, string payType, string orderId, string payReason, string payMethod)
        {
            if (!is_initialized) return;
            GravityEngineAPI.TrackPayEvent(payAmount, payType, orderId, payReason, payMethod);
        }

        public void ReportAdShow(string adUnionType, string adPlacementId, string adSourceId, string adType, string adnType, float ecpm)
        {
            if (!is_initialized) return;
            GravityEngineAPI.TrackMiniGameAdShowEvent(adUnionType, adPlacementId);
        }

        public void Report(string eventKey, Dictionary<string, object> paramMap)
        {
            if (!is_initialized) return;
            GravityEngineAPI.Track(eventKey, paramMap);
        }

    }
}
#endif