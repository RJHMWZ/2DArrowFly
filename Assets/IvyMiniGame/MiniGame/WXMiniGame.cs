#if IVY_SDK_WX
using IvySdk.Cloud;
using IvySdk.Event;
using IvySdk.Tracker;
using IvySdk.Utils;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WeChatWASM;

namespace IvySdk.MiniGame
{

    public class WXMiniGame : IMiniGame
    {

        private IvySDKConfig.PlatformConfig config;

        private UserInfo userInfo;

        private WeChatPayment _weChatPayment;

        public void Init(Action<bool> action)
        {
            userInfo = new UserInfo();

            IvySDKConfig ivySdkConfig = Resources.Load<IvySDKConfig>("IvySDKConfig");

            if (ivySdkConfig == null)
            {
                IvyLog.e($"unable load config file");
                action?.Invoke(false);
                return;
            }
            config = ivySdkConfig.wx;
            if (config == null)
            {
                IvyLog.e("unable load wx config");
                action?.Invoke(false);
                return;
            }

            if (string.IsNullOrEmpty(config.appId))
            {
                IvyLog.e($"invalid wx config:{config}");
                action?.Invoke(false);
                return;
            }


            WX.InitSDK(code =>
            {
                DecodeUserInfo();
                ReportGameStart();
                //初始化微信云环境
                //InitCloud(code, ret);

                if (userInfo != null)
                {
                    if (userInfo.open_id != null)
                    {
                        EventTracker.AddUserProperty("open_id", userInfo.open_id);
                    }
                }
                var open_times = GetLocalIntSync("open_times", 0) + 1;
                SetLocalInt("open_times", open_times);
                if (open_times == 1)
                {
                    EventTracker.TrackEvent("sdk_first_open", new Dictionary<string, object>());
                }
                EventTracker.TrackEvent("sdk_app_open", new Dictionary<string, object>());

                IsLogin(null);
            });

            WX.OnShow((listenerResult =>
            {
                IvySdk.Instance.onAppShow?.Invoke();
            }));
            WX.OnHide(listenerResult =>
            {
                IvySdk.Instance.onAppHide?.Invoke();
            });

            //归因
            var launchOptionsSync = WX.GetLaunchOptionsSync();
            OceanDataTrack.Instance.OnShow(launchOptionsSync);//巨量
            ADQDataTrack.Instance.OnShow(launchOptionsSync);//广点通

            _weChatPayment = new WeChatPayment();
        }

        public IvySDKConfig.PlatformConfig GetPlatformConfig()
        {
            throw new NotImplementedException();
        }

        public void ReportGameStart()
        {
            WX.ReportGameStart();
        }

        private void DecodeUserInfo()
        {
            string content = GetLocalStringSync("user_info", null);
            if (content == null)
            {
                return;
            }
            try
            {
                var user = JsonUtility.FromJson<LoginCallback>(content);
                userInfo = new UserInfo();
                userInfo.open_id = user.data.uuid;
                userInfo.login_code = user.data.login_code;
                userInfo.union_id = user.data.unionid;
            }
            catch (Exception ex) { }

        }

        public UserInfo Me()
        {
            return userInfo;
        }

        public void IsLogin(Action<bool> preLogin)
        {
            WX.CheckSession(new CheckSessionOption
            {
                success = resp =>
                {
                    IvyLog.d($"CheckSession success:{resp.errMsg}");
                    DecodeUserInfo();

                    if (string.IsNullOrEmpty(userInfo.open_id) || string.IsNullOrEmpty(userInfo.login_code) || string.IsNullOrEmpty(userInfo.union_id))
                    {
                        preLogin?.Invoke(false);
                        DeleteLocalKey("user_info");
                        return;
                    }

                    preLogin?.Invoke(true);
                    EventTracker.InitGravityWithOpenId(userInfo.open_id, config);
                    EventTracker.ReportRegister();

                },
                fail = resp =>
                {
                    IvyLog.d($"CheckSession fail:{resp.errMsg}");
                    preLogin?.Invoke(false);
                },
                complete = resp => { IvyLog.d($"CheckSession complete:{resp.errMsg}"); }
            });
        }

        public void Login(Action<string> loginSuccess, Action<string> loginFail)
        {
            WX.Login(new LoginOption
            {
                complete = resp => { IvyLog.d($"login complete:{resp.errMsg}"); },
                success = resp =>
                {
                    IvyLog.d($"login success:{resp.code}");
                    CloudStorage.CallFunctionToSelf("user_login", new Dictionary<string, object>
                    {
                        ["appid"] = config.appId,
                        ["code"] = resp.code
                    }, (functionName, content) =>
                    {
                        var user = JsonUtility.FromJson<LoginCallback>(content);
                        userInfo = new UserInfo();
                        userInfo.open_id = user.data.uuid;
                        userInfo.login_code = user.data.login_code;
                        userInfo.union_id = user.data.unionid;
                        SetLocalString("user_info", content);

                        loginSuccess?.Invoke(content);

                        EventTracker.InitGravityWithOpenId(userInfo.open_id, config);
                        EventTracker.ReportRegister();

                        EventTracker.AddUserProperty("open_id", userInfo.open_id);
                              
                        EventTracker.TrackEvent("sdk_user_login", new Dictionary<string, object>());

                    }, errorMsg => { loginFail?.Invoke(errorMsg); });

                },
                fail = resp =>
                {
                    loginFail?.Invoke(resp.errMsg);
                }
            });
        }

        public bool HasLocalKey(string key)
        {
            return WX.StorageHasKeySync(key);
        }

        public void DeleteLocalKey(string key)
        {
            WX.StorageDeleteKeySync(key);
        }

        public void DeleteAll()
        {
            WX.StorageDeleteAllSync();
        }

        public void SetLocalString(string key, string value = "")
        {
            WX.StorageSetStringSync(key, value);
        }

        public string GetLocalStringSync(string key, string defaultValue)
        {
            return WX.StorageGetStringSync(key, defaultValue);
        }

        public void SetLocalInt(string key, int value)
        {
            WX.StorageSetIntSync(key, value);
        }

        public int GetLocalIntSync(string key, int defaultValue)
        {
            return WX.StorageGetIntSync(key, defaultValue);
        }

        public void SetLocalFloat(string key, float value)
        {
            WX.StorageSetFloatSync(key, value);
        }

        public float GetLocalFloatSync(string key, float defaultValue)
        {
            return WX.StorageGetFloatSync(key, defaultValue);
        }

        public void VibratePhone(bool isShort)
        {
            if (isShort)
            {
                WX.VibrateShort(new VibrateShortOption
                {
                    type = "light"
                });
            }
            else
            {
                WX.VibrateLong(new VibrateLongOption());
            }
        }

        public void SetClipboardData(string text, Action<bool> result)
        {
            WX.SetClipboardData(new SetClipboardDataOption()
            {
                data = text,
                success = resp => { result?.Invoke(true); },
                fail = resp => { result?.Invoke(false); }
            });
        }

        public Dictionary<string, object> GetSystemInfo()
        {
            WeChatWASM.SystemInfo info = WX.GetSystemInfoSync();
            var systemInfo = new Dictionary<string, object>
            {
                ["model"] = info.model,
                ["system"] = info.system,
                ["platform"] = info.platform,
                ["brand"] = info.brand,
                ["language"] = info.language,
                ["pixelRatio"] = info.pixelRatio,
                ["safeArea"] = info.safeArea,
                ["screenHeight"] = info.screenHeight,
                ["screenWidth"] = info.screenWidth,
                ["sdkVersion"] = info.SDKVersion,
                ["statusBarHeight"] = info.statusBarHeight,
                ["version"] = info.version,
            };

            return systemInfo;
        }

        public string GetOS()
        {
            return WX.GetSystemInfoSync().platform;
        }

        public bool IsIOSSystem()
        {
            string platform = WX.GetSystemInfoSync().platform;
            if ("ios".Equals(platform, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public Vector3 GetHorizonSafeAreaOffsetAndMenuOffset()
        {
            Vector3 result = Vector3.zero;
            var rect = WX.GetMenuButtonBoundingClientRect();
            var systemInfo = WX.GetSystemInfoSync();
            var rate = systemInfo.pixelRatio;
            result.x = (int)(systemInfo.safeArea.left * rate);
            result.y = (int)((systemInfo.screenWidth - systemInfo.safeArea.right) * rate);

            result.z = (int)((systemInfo.screenWidth - rect.left) * rate);
            return result;
        }

        public void OpenCustomerServicePage(Action<bool> onComplete)
        {
            WX.OpenCustomerServiceConversation(new OpenCustomerServiceConversationOption()
            {
                complete = (res) => { IvyLog.d("OpenCustomerServicePage complete"); },
                fail = (res) => { onComplete?.Invoke(false); },
                success = (res) => { onComplete?.Invoke(true); }
            });
        }

        public void Pay(Product product, string payload = null)
        {
            if (_weChatPayment != null)
            {
                _weChatPayment.RequestPayment(product.payId, product.desc, product.amount, payload);
            }
            else
            {
                PaymentManager.failPayCall?.Invoke(product.payId);
            }
        }

        #region 键盘相关

        private Action<string> _onInput;
        private Action<string> _onConfirm;
        private Action _onComplete;

        public void ShowKeyboard(Action<string> onInput, Action<string> onConfirm, Action onComplete, string defaultValue = "", int maxLength = 999)
        {
            WX.ShowKeyboard(new ShowKeyboardOption()
            {
                defaultValue = defaultValue,
                maxLength = maxLength,
                confirmType = "done"
            });
            _onInput = onInput;
            _onConfirm = onConfirm;
            _onComplete = onComplete;
            WX.OnKeyboardConfirm(KeyboardEvent_OnConfirm);
            WX.OnKeyboardComplete(KeyboardEvent_OnComplete);
            WX.OnKeyboardInput(KeyboardEvent_OnInput);
        }

        public void HideKeyboard()
        {
            WX.HideKeyboard(new HideKeyboardOption());
            _onInput = null;
            _onConfirm = null;
            _onComplete = null;
            WX.OffKeyboardInput(KeyboardEvent_OnInput);
            WX.OffKeyboardConfirm(KeyboardEvent_OnConfirm);
            WX.OffKeyboardComplete(KeyboardEvent_OnComplete);
        }

        private void KeyboardEvent_OnInput(OnKeyboardInputListenerResult result)
        {
            _onInput?.Invoke(result.value);
        }

        private void KeyboardEvent_OnConfirm(OnKeyboardInputListenerResult result)
        {
            _onConfirm?.Invoke(result.value);
        }

        private void KeyboardEvent_OnComplete(OnKeyboardInputListenerResult result)
        {
            _onComplete?.Invoke();
        }

        #endregion


        #region 广告
        public void ShowBanner(string tag, BannerPosition bp, Action<AdEvent, string> rlt = null)
        {
            AdManager.ShowBanner(tag, bp, rlt);
        }

        public void ShowBanner(string tag, int left, int top, int width, Action<AdEvent, string> rlt = null)
        {
            AdManager.ShowBanner(tag, left, top, width, rlt);
        }

        public void HideBanner(string tag)
        {
            AdManager.HideBanner(tag);
        }

        public void ShowRewardedVideo(string tag, Action<AdEvent, string, int> rtl = null, bool multiton = false, string[] multitonRewardMsg = null, int multitonRewardTime = 0)
        {
            AdManager.ShowRewardedVideo(tag, rtl, multiton, multitonRewardMsg, multitonRewardTime);
        }

        public bool HasInterstitial()
        {
            return AdManager.HasInterstitial();
        }

        public void ShowInterstitial()
        {
            AdManager.ShowInterstitial("main");
        }

        public void ShowInterstitial(string tag, Action<AdEvent, string> rlt = null)
        {
            AdManager.ShowInterstitial(tag, rlt);
        }


        public bool HasRewardAd()
        {
            return AdManager.HasRewardAd();
        }

        #endregion


        #region 清理文件缓存

        public void CleanAllFileCache()
        {
            WX.CleanAllFileCache((res) =>
            {
                IvyLog.d($"CleanAllFileCache {res}");
                if (res)
                {
                    WX.RestartMiniProgram(new RestartMiniProgramOption()
                    {

                    });
                }
            });
        }

        public void SubscribeSystemMessage(string[] tmplIds, Action<Dictionary<string, string>> success = null, Action<double, string> fail = null)
        {
            if (tmplIds == null || tmplIds.Length == 0)
            {
                IvyLog.d("SubscribeSystemMessage tmplIds is null");
                return;
            }
            RequestSubscribeMessageOption option = new RequestSubscribeMessageOption();
            option.tmplIds = tmplIds;
            option.success = (res) =>
            {
                var result = new Dictionary<string, string>();

                // 将原 Dictionary 中的每个元素转换为 string 类型
                foreach (var item in res)
                {
                    result[item.Key] = item.Value.ToString();
                }

                // 添加 errMsg，如果需要的话
                if (!string.IsNullOrEmpty(res.errMsg))
                {
                    result["errMsg"] = res.errMsg;
                }

                success?.Invoke(result);
            };
            option.fail = (res) =>
            {
                fail?.Invoke(res.errCode, res.errMsg);
            };
            WX.RequestSubscribeMessage(option);
        }

        public void IsSubscribeSystemMessage(string tmplId, Action<bool> action)
        {
            GetSettingOption option = new GetSettingOption();
            option.withSubscriptions = true;
            option.success = (res) =>
            {
                if (res.subscriptionsSetting.mainSwitch && res.subscriptionsSetting.itemSettings != null)
                {
                    if (res.subscriptionsSetting.itemSettings.TryGetValue(tmplId, out string isSubscribed))
                    {
                        if ("accept".Equals(isSubscribed))
                        {
                            action?.Invoke(true);
                        }
                        else
                        {
                            action?.Invoke(false);
                        }
                    }
                    else
                    {
                        action?.Invoke(false);
                    }
                }
                else
                {
                    action?.Invoke(false);
                }
            };
            option.fail = (res) =>
            {
                action?.Invoke(false);
            };
            WX.GetSetting(option);
        }

    }

    #endregion


}
#endif