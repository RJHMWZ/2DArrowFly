using System;
using System.Collections.Generic;
using UnityEngine;

namespace IvySdk.MiniGame
{

    internal partial interface IMiniGame
    {

        public void Init(Action<bool> action);

        public IvySDKConfig.PlatformConfig GetPlatformConfig();

        public void ReportGameStart();

        #region 登录

        public UserInfo Me();

        public void IsLogin(Action<bool> preLogin);

        public void Login(Action<string> loginSuccess, Action<string> loginFail);


        #endregion

        #region 本地存储

        bool HasLocalKey(string key);

        void DeleteLocalKey(string key);
        public void DeleteAll();
        void SetLocalString(string key, string value = "");

        string GetLocalStringSync(string key, string defaultValue);

        void SetLocalInt(string key, int value);

        int GetLocalIntSync(string key, int defaultValue);

        void SetLocalFloat(string key, float value);

        float GetLocalFloatSync(string key, float defaultValue);

        #endregion

        #region 震动

        public void VibratePhone(bool isShort);

        #endregion

        public void SetClipboardData(string text, Action<bool> result);

        public Dictionary<string, object> GetSystemInfo();

        public string GetOS();

        public bool IsIOSSystem();

        public Vector3 GetHorizonSafeAreaOffsetAndMenuOffset();

        public void OpenCustomerServicePage(Action<bool> onComplete);

        //支付
        public void Pay(Product product, string payload = null);

        #region 广告
        public void ShowBanner(string tag, BannerPosition bp, Action<AdEvent, string> rlt = null);

        public void ShowBanner(string tag, int left, int top, int width, Action<AdEvent, string> rlt = null);

        public void HideBanner(string tag);

        public void ShowRewardedVideo(string tag, Action<AdEvent, string, int> rtl = null, bool multiton = false, string[] multitonRewardMsg = null, int multitonRewardTime = 0);

        public bool HasInterstitial();

        public void ShowInterstitial();

        public void ShowInterstitial(string tag, Action<AdEvent, string> rlt = null);


        public bool HasRewardAd();

        #endregion

        public void CleanAllFileCache();

        public void ShowKeyboard(Action<string> onInput, Action<string> onConfirm, Action onComplete, string defaultValue = "", int maxLength = 999);

        public void HideKeyboard();

 



    }

#if IVY_SDK_DY
    //抖音特定接口
    internal partial interface IMiniGame
    {
        public void StartRecordScreen(bool isRecordAudio, int maxRecordTime, Action onStartCallback, Action<int, string> onErrorCallback,
            Action<string> onCompleteCallback);

        public void StopRecordScreen(Action<int, string> onErrorCallback, Action<string> onCompleteCallback);

        public int GetRecordDuration();

        public void ShareMessage(string title, string desc, Action onSuccessCallback, Action<string> onFailedCallback, Action onCanceledCallback);

        //敏感词
        public void ReplaceSensitiveWords(string txt, Action<string> replaceResult);

        //侧边栏
        public void CheckScene(Action<bool> success, Action complete, Action<int, string> error);

        public void NavigateToScene(Action success, Action complete, Action<int, string> error);

        public bool IsFromSidebarCard();


        #region 缓存

        public void CacheData(string key, string data);

        public string LoadCachedData(string key);

        public void DeleteCachedData(string key);

        public void ClearAllCachedData();

        #endregion

    }

#elif IVY_SDK_WX
    //微信特定接口
    internal partial interface IMiniGame
    {

        public void SubscribeSystemMessage(string[] tmplIds, Action<Dictionary<string, string>> success = null, Action<double, string> fail = null);

        public void IsSubscribeSystemMessage(string tmplId, Action<bool> action);

    }

#elif IVY_SDK_TT

    internal partial interface IMiniGame
    {

        #region 支付
        public void ConfirmPay(string merchant_transaction_id, Action<bool> callback);

        public void Query();

        #endregion


        public void ShareMessage(string title, string desc, string imageUrl, Action onSuccessCallback, Action<string> onFailedCallback, Action onCanceledCallback);

        #region 桌面快捷方式

        public void AddShortcut(Action<bool> callback);

        public void GetShortcutMissionReward(Action<bool> callback);

        #endregion

        #region 侧边栏
        public void StartEntranceMission(Action<bool> callback);

        public void GetEntranceMissionReward(Action<bool> callback);
        #endregion


        #region 缓存

        public void CacheData(string key, string data);

        public string LoadCachedData(string key);

        public void DeleteCachedData(string key);

        public void ClearAllCachedData();

        #endregion

    }

#endif





}