using IvySdk.Cloud;
using IvySdk.MiniGame;
using IvySdk.Remote;
using IvySdk.Tracker;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace IvySdk
{

    public enum AdEvent
    {
        SHOW_SUCCESS = 0,
        SHOW_FAILURE = 1,
        CLOSED = 2,
        REWARD_USER = 3,
    }

    public enum BannerPosition
    {
        /// <summary>
        /// 在左上角显示banner广告参数常量
        /// </summary>
        POS_LEFT_TOP = 1,

        /// <summary>
        /// 在顶部居中显示banner广告参数常量
        /// </summary>
        POS_MIDDLE_TOP = 3,

        /// <summary>
        /// 在右上角显示banner广告参数常量
        /// </summary>
        POS_RIGHT_TOP = 6,

        /// <summary>
        /// 在中间居中显示banner广告参数常量
        /// </summary>
        POS_MIDDLE_MIDDLE = 5,

        /// <summary>
        /// 在左下角显示banner广告参数常量
        /// </summary>
        POS_LEFT_BOTTOM = 2,

        /// <summary>
        /// 在底部居中显示banner广告参数常量
        /// </summary>
        POS_MIDDLE_BOTTOM = 4,

        /// <summary>
        /// 在右下角显示banner广告参数常量
        /// </summary>
        POS_RIGHT_BOTTOM = 7,

        POS_LEFT_MIDDLE = 8,
        POS_RIGHT_MIDDLE = 9,
    }


    public sealed class IvySdk
    {

        public Action<string, PayData> successPayCall;
        public Action<string> failPayCall;

#if IVY_SDK_DY
        public Action<bool> openFromSideBar;
#endif

#if IVY_SDK_TT || IVY_SDK_WX
        public Action onAppShow;
        public Action onAppHide;
#endif

        private static IvySdk _instance;

        public static IvySdk Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IvySdk();
                }
                return _instance;
            }
        }

        private IvySdk()
        {
            //MiniGameProxy.
            // TODO: Add constructor logic here
            IvyRemoteData.Instance.InitRemoteDataWithCache();
        }

        public string GetRoleId()
        {
            return MiniGameProxy.GetRoleId();
        }

        //不需要调用
        public void ReportGameStart()
        {
            MiniGameProxy.ReportGameStart();
        }

        public IvySDKConfig.PlatformConfig GetPlatformConfig()
        {
            return MiniGameProxy.GetPlatformConfig() ?? null;
        }

        #region 登录 

        public UserInfo Me()
        {
            return MiniGameProxy.Me() ?? null;
        }

        public void IsLogin(Action<bool> preLogin)
        {
            MiniGameProxy.IsLogin(preLogin);
        }

        public void Login(Action<string> loginSuccess, Action<string> loginFail)
        {
            MiniGameProxy.Login(loginSuccess, loginFail);
        }
        #endregion


        #region 本地缓存
        public bool HasLocalKey(string key)
        {
            return MiniGameProxy.HasLocalKey(key);
        }

        public void DeleteLocalKey(string key)
        {
            MiniGameProxy.DeleteLocalKey(key);
        }

        public void DeleteAll()
        {
            MiniGameProxy.DeleteAll();
        }

        public void SetLocalString(string key, string value = "")
        {
            MiniGameProxy.SetLocalString(key, value);
        }

        public string GetLocalStringSync(string key, string defaultValue)
        {
            return MiniGameProxy.GetLocalStringSync(key, defaultValue);
        }

        public void SetLocalInt(string key, int value)
        {
            MiniGameProxy.SetLocalInt(key, value);
        }

        public int GetLocalIntSync(string key, int defaultValue)
        {
            return MiniGameProxy.GetLocalIntSync(key, defaultValue);
        }

        public void SetLocalFloat(string key, float value)
        {
            MiniGameProxy.SetLocalFloat(key, value);
        }

        public float GetLocalFloatSync(string key, float defaultValue)
        {
            return MiniGameProxy.GetLocalFloatSync(key, defaultValue);
        }

        #endregion

        #region  远程配置
        public string GetRemoteConfigString(string key, string defaultValue = "")
        {
            return IvyRemoteData.Instance.GetString(key, defaultValue);
        }

        public int GetRemoteConfigInt(string key, int defaultValue = 0)
        {
            return IvyRemoteData.Instance.GetInt(key, defaultValue);
        }

        public long GetRemoteConfigLong(string key, long defaultValue = 0)
        {
            return IvyRemoteData.Instance.GetLong(key, defaultValue);
        }

        public double GetRemoteConfigDouble(string key, double defaultValue = 0)
        {
            return IvyRemoteData.Instance.GetDouble(key, defaultValue);
        }

        public bool GetRemoteConfigBool(string key, bool defaultValue = false)
        {
            return IvyRemoteData.Instance.GetBool(key, defaultValue);
        }

        public bool HasRemoteConfigKey(string key)
        {
            return IvyRemoteData.Instance.HasKey(key);
        }

        #endregion

        //震动
        public void VibratePhone(bool isShort)
        {
            MiniGameProxy.VibratePhone(isShort);
        }

        //剪切板
        public void SetClipboardData(string text, Action<bool> result)
        {
            MiniGameProxy.SetClipboardData(text, result);
        }

        //系统信息
        public Dictionary<string, object> GetSystemInfo()
        {
            return MiniGameProxy.GetSystemInfo() ?? new();
        }

        //系统类型：IOS、Android、Honmoy
        public string GetOS()
        {
            return MiniGameProxy.GetOS();
        }

        public bool IsIOSSystem()
        {
            return MiniGameProxy.IsIOSSystem();
        }

        //横向刘海高度
        public Vector3 GetHorizonSafeAreaOffsetAndMenuOffset()
        {
            return MiniGameProxy.GetHorizonSafeAreaOffsetAndMenuOffset();
        }


        #region 客服
        public void ShowHelp(Action<bool> onComplete)
        {
            MiniGameProxy.OpenCustomerServicePage(onComplete);
        }
        #endregion


        #region 支付

        public Product GetPaymentData(string billId)
        {
            return MiniGameProxy.GetPaymentData(billId);
        }

        public void Pay(string billId, string payload = null)
        {
            MiniGameProxy.Pay(billId, payload);
        }


#if IVY_SDK_TT
        //确认发货
        public void ConfirmPay(string merchant_transaction_id, Action<bool> callback)
        {
            MiniGameProxy.ConfirmPay(merchant_transaction_id, callback);
        }
        //查询未发货订单
        public void Query()
        {
            MiniGameProxy.Query();
        }
#endif

        #endregion


        #region  事件

        public void LogEvent(string eventKey, Dictionary<string, object> paramMap)
        {
            if (paramMap != null)
            {
                EventTracker.TrackEvent(eventKey, paramMap);
            } else
            {
                EventTracker.TrackEvent(eventKey, new Dictionary<string, object>());
            }
        }

        public void LogEvent(string eventKey, string paramContent)
        {
            if (paramContent == null)
            {
                LogEvent(eventKey, new Dictionary<string, object>());
                return;
            }
            EventTracker.TrackEvent(eventKey, paramContent);
        }

        #endregion

        #region 云函数

        public void CallTencentCloudFunction(string api, string content, Action<string> onSuccess, Action<string> onFail)
        {
            CloudStorage.CallFunction(api, content, onSuccess, onFail);
        }

        #endregion


        #region 广告
        public void TriggerBannerAd(String placement)
        {
            var obj = new Dictionary<string, object>()
            {
                ["ad_format"] = "banner",
            };
            Tracker.EventTracker.TrackEvent("sdk_ad_triggered", obj);
        }

        public void ShowBanner(string tag, BannerPosition bp, Action<AdEvent, string> rlt = null)
        {
            MiniGameProxy.ShowBanner(tag, bp, rlt);
        }

        public void ShowBanner(string tag, int left, int top, int width, Action<AdEvent, string> rlt = null)
        {
            MiniGameProxy.ShowBanner(tag, left, top, width, rlt);
        }

        public void HideBanner(string tag)
        {
            MiniGameProxy.HideBanner(tag);
        }

        public void TriggerRewardedAd(String placement)
        {
            var obj = new Dictionary<string, object>()
            {
                ["ad_format"] = "rewarded",
            };
            Tracker.EventTracker.TrackEvent("sdk_ad_triggered", obj);
        }

        public void ShowRewardedVideo(string tag, Action<AdEvent, string, int> rtl = null, bool multiton = false, string[] multitonRewardMsg = null, int multitonRewardTime = 0)
        {
            MiniGameProxy.ShowRewardedVideo(tag, rtl, multiton, multitonRewardMsg, multitonRewardTime);
        }

        public void TriggerInterstitialAd(String placement)
        {
            var obj = new Dictionary<string, object>()
            {
                ["ad_format"] = "interstitial",
            };
            Tracker.EventTracker.TrackEvent("sdk_ad_triggered", obj);
        }

        public bool HasInterstitial()
        {
            return MiniGameProxy.HasInterstitial();
        }

        public void ShowInterstitial()
        {
            MiniGameProxy.ShowInterstitial("main");
        }

        public void ShowInterstitial(string tag, Action<AdEvent, string> rlt = null)
        {
            MiniGameProxy.ShowInterstitial(tag, rlt);
        }

        public bool HasRewardAd()
        {
            return MiniGameProxy.HasRewardAd();
        }

        #endregion

        //清除缓存并重启
        public static void CleanAllFileCache()
        {
            MiniGameProxy.CleanAllFileCache();
        }

        #region 键盘
        public void ShowKeyboard(Action<string> onInput, Action<string> onConfirm, Action onComplete, string defaultValue = "", int maxLength = 999)
        {
            MiniGameProxy.ShowKeyboard(onInput, onConfirm, onComplete, defaultValue, maxLength);
        }

        public void HideKeyboard()
        {
            MiniGameProxy.HideKeyboard();
        }
        #endregion




#if IVY_SDK_DY

        //开始录屏
        public void StartRecordScreen(bool isRecordAudio, int maxRecordTime, Action onStartCallback, Action<int, string> onErrorCallback, Action<string> onCompleteCallback)
        {
            MiniGameProxy.StartRecordScreen(isRecordAudio, maxRecordTime, onStartCallback, onErrorCallback, onCompleteCallback);
        }
        //结束录屏
        public void StopRecordScreen(Action<int, string> onErrorCallback, Action<string> onCompleteCallback)
        {
            MiniGameProxy.StopRecordScreen(onErrorCallback, onCompleteCallback);
        }
        //获取录屏时长
        public int GetRecordDuration()
        {
            return MiniGameProxy.GetRecordDuration();
        }
         //抖音分享
        public void ShareMessage(string title, string desc, Action onSuccessCallback, Action<string> onFailedCallback, Action onCanceledCallback)
        {
            MiniGameProxy.ShareMessage(title, desc, onSuccessCallback, onFailedCallback, onCanceledCallback);
        }

        //敏感词替换
        public void ReplaceSensitiveWords(string txt, Action<string> replaceResult)
        {
            MiniGameProxy.ReplaceSensitiveWords(txt, replaceResult);
        }
        //侧边栏
        public void CheckScene(Action<bool> success, Action complete, Action<int, string> error)
        {
            MiniGameProxy.CheckScene(success, complete, error);
        }
        //跳转侧边栏
        public void NavigateToScene(Action success, Action complete, Action<int, string> error)
        {
            MiniGameProxy.NavigateToScene(success, complete, error);
        }
        //是否从侧边栏打开
        public bool IsFromSidebarCard()
        {
            return MiniGameProxy.IsFromSidebarCard();
        }

        #region 缓存

        public void CacheData(string key, string data)
        {
            MiniGameProxy.CacheData(key, data);
        }

        public string LoadCachedData(string key)
        {
            return MiniGameProxy.LoadCachedData(key);
        }

        public void DeleteCachedData(string key)
        {
            MiniGameProxy.DeleteCachedData(key);
        }

        public void ClearAllCachedData()
        {
            MiniGameProxy.ClearAllCachedData();
        }

        #endregion

#elif IVY_SDK_WX

        //订阅 模板列表 tmplIds 的系统消息
        public static void SubscribeSystemMessage(string[] tmplIds, Action<Dictionary<string, string>> success = null, Action<double, string> fail = null)
        {
            MiniGameProxy.SubscribeSystemMessage(tmplIds, success, fail);
        }
        //是否订阅 模板 tmplId 的系统消息
        public static void IsSubscribeSystemMessage(string tmplId, Action<bool> action)
        {
            MiniGameProxy.IsSubscribeSystemMessage(tmplId, action);
        }
#elif IVY_SDK_TT

        public void ShareMessage(string title, string desc, string imageUrl, Action onSuccessCallback, Action<string> onFailedCallback, Action onCanceledCallback)
        {
            MiniGameProxy.ShareMessage(title, desc, imageUrl, onSuccessCallback, onFailedCallback, onCanceledCallback);
        }

        #region 桌面快捷方式

        public void AddShortcut(Action<bool> callback)
        {
            MiniGameProxy.AddShortcut(callback);
        }

        public void GetShortcutMissionReward(Action<bool> callback)
        {
            MiniGameProxy.GetShortcutMissionReward(callback);
        }

        #endregion

        #region 侧边栏
        public void StartEntranceMission(Action<bool> callback)
        {
            MiniGameProxy.StartEntranceMission(callback);
        }

        public void GetEntranceMissionReward(Action<bool> callback)
        {
            MiniGameProxy.GetEntranceMissionReward(callback);
        }
        #endregion


        #region 缓存

        public void CacheData(string key, string data)
        {
            MiniGameProxy.CacheData(key, data);
        }

        public string LoadCachedData(string key)
        {
            return MiniGameProxy.LoadCachedData(key);
        }

        public void DeleteCachedData(string key)
        {
            MiniGameProxy.DeleteCachedData(key);
        }

        public void ClearAllCachedData()
        {
            MiniGameProxy.ClearAllCachedData();
        }

        #endregion

#endif

    }

}