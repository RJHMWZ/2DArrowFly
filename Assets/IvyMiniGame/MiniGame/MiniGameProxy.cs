using IvySdk.Cloud;
using IvySdk.Tracker;
using IvySdk.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace IvySdk.MiniGame
{


    public partial class UserInfo
    {
        public string open_id;
        public string login_code;
        public string union_id;
    }

#if IVY_SDK_TT
    public partial class UserInfo
    {
        public string access_token;
        public string refresh_token;
    }
#endif

    [System.Serializable]
    public class Product
    {
        [SerializeField] public string payId;
        [SerializeField] public string name;
        [SerializeField] public string skuId;
        [SerializeField] public string price;
        [SerializeField] public string currency;
        [SerializeField] public int amount;
        [SerializeField] public string desc;
        [SerializeField] public string imageUrl;
    }


    public partial class MiniGameProxy
    {

        private static IMiniGame _instance;

        //测试服
        public static readonly string payNotifyUrl = "https://service-00fjnzs0-1259425184.gz.tencentapigw.cn/pay/dyNotify";


        private MiniGameProxy() { }

        private static string _role_id;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void OnBaseMiniGameStart()
        {

            InitRoleId();

#if IVY_SDK_DY
                _instance = new DYMiniGame();
#elif IVY_SDK_WX
            _instance = new WXMiniGame();
#elif IVY_SDK_TT
            _instance = new TTMiniGame();
#endif

            //测试服地址
            //var listRequest = "https://service-00fjnzs0-1259425184.gz.tencentapigw.cn/api/list";

            //正式服地址
            var listRequest = "https://service-0knqzyio-1259425184.gz.apigw.tencentcs.com/api/list";

            CloudStorage.InitCloudStorage(listRequest);
            PaymentManager.LoadPayConfig();
            PaymentManager.successPayCall = (string action, PayData content) =>
            {
                //支付成功
                IvySdk.Instance.successPayCall?.Invoke(action, content);
            };

            PaymentManager.failPayCall = (string action) =>
            {
                //支付失败
                IvySdk.Instance?.failPayCall?.Invoke(action);
            };
            EventTracker.InitTrack();
            _instance?.Init(ret => { IvyLog.d($"OnRuntimeInit InitSDK:{ret}"); });

        }

        private static void InitRoleId()
        {
            if (PlayerPrefs.HasKey("user_role_id"))
            {
                _role_id = PlayerPrefs.GetString("user_role_id");
            }
            if (string.IsNullOrEmpty(_role_id))
            {
                _role_id = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("user_role_id", _role_id);
            }
        }

        public static string GetRoleId()
        {
            if (string.IsNullOrEmpty(_role_id))
            {
                _role_id = PlayerPrefs.GetString("user_role_id");
            }

            return _role_id;
        }

        public static void ReportGameStart()
        {
            _instance?.ReportGameStart();
        }

        public static IvySDKConfig.PlatformConfig GetPlatformConfig()
        {
            return _instance?.GetPlatformConfig() ?? null;
        }

        public static UserInfo Me()
        {
            return _instance?.Me() ?? null;
        }

        public static void IsLogin(Action<bool> preLogin)
        {
            if (_instance != null)
            {
                _instance?.IsLogin(preLogin);
            }
            else
            {
                preLogin?.Invoke(false);
            }
        }

        public static void Login(Action<string> loginSuccess, Action<string> loginFail)
        {
            if (loginSuccess != null)
            {
                _instance.Login(loginSuccess, loginFail);
            }
            else
            {
                loginFail?.Invoke("not initialized");
            }
        }

        public static bool HasLocalKey(string key)
        {
            return _instance?.HasLocalKey(key) ?? false;
        }

        public static void DeleteLocalKey(string key)
        {
            _instance?.DeleteLocalKey(key);
        }

        public static void DeleteAll()
        {
            _instance?.DeleteAll();
        }

        public static void SetLocalString(string key, string value = "")
        {
            _instance?.SetLocalString(key, value);
        }

        public static string GetLocalStringSync(string key, string defaultValue)
        {
            return _instance?.GetLocalStringSync(key, defaultValue) ?? defaultValue;
        }

        public static void SetLocalInt(string key, int value)
        {
            _instance.SetLocalInt(key, value);
        }

        public static int GetLocalIntSync(string key, int defaultValue)
        {
            return _instance?.GetLocalIntSync(key, defaultValue) ?? defaultValue;
        }

        public static void SetLocalFloat(string key, float value)
        {
            _instance?.SetLocalFloat(key, value);
        }

        public static float GetLocalFloatSync(string key, float defaultValue)
        {
            return _instance?.GetLocalFloatSync(key, defaultValue) ?? defaultValue;
        }

        public static void VibratePhone(bool isShort)
        {
            _instance?.VibratePhone(isShort);
        }

        public static void SetClipboardData(string text, Action<bool> result)
        {
            _instance?.SetClipboardData(text, result);
        }

        public static Dictionary<string, object> GetSystemInfo()
        {
            return _instance?.GetSystemInfo() ?? new();
        }

        public static string GetOS()
        {
            return _instance?.GetOS() ?? "Android";
        }

        public static bool IsIOSSystem()
        {
            return _instance?.IsIOSSystem() ?? false;
        }

        public static Vector3 GetHorizonSafeAreaOffsetAndMenuOffset()
        {
            return _instance?.GetHorizonSafeAreaOffsetAndMenuOffset() ?? Vector3.zero;
        }

        public static void OpenCustomerServicePage(Action<bool> onComplete)
        {
            if (_instance != null)
            {
                _instance.OpenCustomerServicePage(onComplete);
            }
            else
            {
                onComplete?.Invoke(false);
            }
        }

        public static Product GetPaymentData(string billId)
        {
            return PaymentManager.GetPayConfig(billId);
        }

        #region 支付
        public static void Pay(string billId, string payload = null)
        {
            Product product = PaymentManager.GetPayConfig(billId);
            if (product == null)
            {
                PaymentManager.failPayCall.Invoke(billId);
                return;
            }
            if (_instance != null)
            {
                _instance.Pay(product, payload);
            }
            else
            {
                PaymentManager.failPayCall.Invoke(billId);
            }
        }

#if IVY_SDK_TT

        public static void ConfirmPay(string merchant_transaction_id, Action<bool> callback)
        {
            if (_instance != null)
            {
                _instance.ConfirmPay(merchant_transaction_id, callback);
            }
            else
            {
                callback?.Invoke(false);
            }
        }

        public static void Query()
        {
            _instance?.Query();
        }

#endif
        #endregion

        public static void ShowKeyboard(Action<string> onInput, Action<string> onConfirm, Action onComplete, string defaultValue = "", int maxLength = 999)
        {
            _instance?.ShowKeyboard(onInput, onConfirm, onComplete, defaultValue, maxLength);
        }

        public static void HideKeyboard()
        {
            _instance?.HideKeyboard();
        }

        #region 广告
        public static void ShowBanner(string tag, BannerPosition bp, Action<AdEvent, string> rlt = null)
        {
            if (_instance != null)
            {
                _instance?.ShowBanner(tag, bp, rlt);
            }
            else
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, "not initialized");
            }

        }

        public static void ShowBanner(string tag, int left, int top, int width, Action<AdEvent, string> rlt = null)
        {
            if (_instance != null)
            {
                _instance?.ShowBanner(tag, left, top, width, rlt);
            }
            else
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, "not initialized");
            }
        }

        public static void HideBanner(string tag)
        {
            _instance?.HideBanner(tag);
        }

        public static void ShowRewardedVideo(string tag, Action<AdEvent, string, int> rtl = null, bool multiton = false, string[] multitonRewardMsg = null, int multitonRewardTime = 0)
        {
            if (_instance != null)
            {
                _instance?.ShowRewardedVideo(tag, rtl, multiton, multitonRewardMsg, multitonRewardTime);
            }
            else
            {
                rtl.Invoke(AdEvent.SHOW_FAILURE, "not initialized", 0);
            }
        }

        public static bool HasInterstitial()
        {
            return _instance?.HasInterstitial() ?? true;
        }

        public static void ShowInterstitial()
        {
            _instance?.ShowInterstitial("main");
        }

        public static void ShowInterstitial(string tag, Action<AdEvent, string> rlt = null)
        {
            if (_instance != null)
            {
                _instance?.ShowInterstitial(tag, rlt);
            }
            else
            {
                rlt?.Invoke(AdEvent.SHOW_FAILURE, "not initialized");
            }
        }

        public static bool HasRewardAd()
        {
            return _instance?.HasRewardAd() ?? true;
        }

        #endregion

        public static void CleanAllFileCache()
        {
            _instance?.CleanAllFileCache();
        }



#if IVY_SDK_DY
        public static void StartRecordScreen(bool isRecordAudio, int maxRecordTime, Action onStartCallback, Action<int, string> onErrorCallback, Action<string> onCompleteCallback)
        {
            if (_instance != null)
            {
                _instance?.StartRecordScreen(isRecordAudio, maxRecordTime, onStartCallback, onErrorCallback, onCompleteCallback);
            }
            else
            {
                onErrorCallback?.Invoke(0, "init error");
            }
        }

        public static void StopRecordScreen(Action<int, string> onErrorCallback, Action<string> onCompleteCallback)
        {
            if (_instance != null)
            {
                _instance?.StopRecordScreen(onErrorCallback, onCompleteCallback);
            }
            else
            {
                onErrorCallback?.Invoke(0, "init error");
            }
        }

        public static int GetRecordDuration()
        {
            return _instance?.GetRecordDuration() ?? 0;
        }

        public static void ShareMessage(string title, string desc, Action onSuccessCallback, Action<string> onFailedCallback, Action onCanceledCallback)
        {
            if (_instance != null)
            {
                _instance?.ShareMessage(title, desc, onSuccessCallback, onFailedCallback, onCanceledCallback);
            }
            else
            {
                onFailedCallback?.Invoke("init error");
            }
        }

        public static void ReplaceSensitiveWords(string txt, Action<string> replaceResult)
        {
            if (_instance != null)
            {
                _instance?.ReplaceSensitiveWords(txt, replaceResult);
            }
            else
            {
                replaceResult?.Invoke(txt);
            }
        }

        public static void CheckScene(Action<bool> success, Action complete, Action<int, string> error)
        {
            if (_instance != null)
            {
                _instance?.CheckScene(success, complete, error);
            }
            else
            {
                error?.Invoke(0, "init error");
            }
        }

        public static void NavigateToScene(Action success, Action complete, Action<int, string> error)
        {
            if (_instance != null)
            {
                _instance?.NavigateToScene(success, complete, error);
            }
            else
            {
                error?.Invoke(0, "init error");
            }
        }

        public static bool IsFromSidebarCard()
        {
            return _instance?.IsFromSidebarCard() ?? false;
        }

        #region 缓存

        public static void CacheData(string key, string data)
        {
            _instance?.CacheData(key, data);
        }

        public static string LoadCachedData(string key)
        {
            return _instance?.LoadCachedData(key) ?? "";
        }

        public static void DeleteCachedData(string key)
        {
            _instance?.DeleteCachedData(key);
        }

        public static void ClearAllCachedData()
        {
            _instance?.ClearAllCachedData();
        }

        #endregion


#elif IVY_SDK_WX

        public static void SubscribeSystemMessage(string[] tmplIds, Action<Dictionary<string, string>> success = null, Action<double, string> fail = null)
        {
            if (_instance != null)
            {
                _instance?.SubscribeSystemMessage(tmplIds, success, fail);
            }
            else
            {
                fail?.Invoke(0, "init error");
            }
        }

        public static void IsSubscribeSystemMessage(string tmplId, Action<bool> action)
        {
            if (_instance != null)
            {
                _instance?.IsSubscribeSystemMessage(tmplId, action);
            }
            else
            {
                action?.Invoke(false);
            }
        }

#elif IVY_SDK_TT

        public static void ShareMessage(string title, string desc, string imageUrl, Action onSuccessCallback, Action<string> onFailedCallback, Action onCanceledCallback)
        {
            if (_instance != null)
            {
                _instance?.ShareMessage(title, desc, imageUrl, onSuccessCallback, onFailedCallback, onCanceledCallback);
            }
            else
            {
                onFailedCallback?.Invoke("init error");
            }
        }

        #region 桌面快捷方式

        public static void AddShortcut(Action<bool> callback)
        {
            if (_instance != null)
            {
                _instance?.AddShortcut(callback);
            }
            else
            {
                callback?.Invoke(false);
            }
        }

        public static void GetShortcutMissionReward(Action<bool> callback)
        {
            if (_instance != null)
            {
                _instance.GetShortcutMissionReward(callback);
            }
            else
            {
                callback?.Invoke(false);
            }
        }

        #endregion

        #region 侧边栏
        public static void StartEntranceMission(Action<bool> callback)
        {
            if (_instance != null)
            {
                _instance?.StartEntranceMission(callback);
            }
            else
            {
                callback?.Invoke(false);
            }
        }

        public static void GetEntranceMissionReward(Action<bool> callback)
        {
            if (_instance != null)
            {
                _instance?.GetEntranceMissionReward(callback);
            }
            else
            {
                callback?.Invoke(false);
            }
        }
        #endregion

        #region 缓存

        public static void CacheData(string key, string data)
        {
            _instance?.CacheData(key, data);
        }

        public static string LoadCachedData(string key)
        {
            return _instance?.LoadCachedData(key) ?? "";
        }

        public static void DeleteCachedData(string key)
        {
            _instance?.DeleteCachedData(key);
        }

        public static void ClearAllCachedData()
        {
            _instance?.ClearAllCachedData();
        }

        #endregion

#endif



    }

}