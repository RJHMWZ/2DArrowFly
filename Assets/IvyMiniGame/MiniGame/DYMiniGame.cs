#if IVY_SDK_DY
using IvySdk.Cloud;
using IvySdk.MiniGame.Ad;
using IvySdk.Tracker;
using IvySdk.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;
using UnityEditor;
using UnityEngine;

namespace IvySdk.MiniGame
{


    public class DYMiniGame : IMiniGame
    {
        private IvySDKConfig.PlatformConfig config;

        private UserInfo userInfo;

        private bool isFromSidebarCard = false;


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
            config = ivySdkConfig.dy;
            if (config == null)
            {
                IvyLog.e("unable load dy config");
                action?.Invoke(false);
                return;
            }

            if (string.IsNullOrEmpty(config.appId))
            {
                IvyLog.e($"invalid dy config:{config}");
                action?.Invoke(false);
                return;
            }
            
            TT.InitSDK((int code, ContainerEnv env) =>
            {
                IvyLog.d($"DY init result:{code}");
                DecodeUserInfo();
                //进入前台
                TT.GetAppLifeCycle().OnShow -= OnAppShow;
                TT.GetAppLifeCycle().OnShow += OnAppShow;
                //进入后台
                TT.GetAppLifeCycle().OnHide -= OnAppHide;
                TT.GetAppLifeCycle().OnHide += OnAppHide;

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

                AdManager.InitAdManager(config);
                IsLogin(null);
                action?.Invoke(true);
            });
        }

        private void OnAppShow(Dictionary<string, object> param)
        {
            if (param == null || param.Count < 1)
            {
                IvyLog.d("ByteGame OnShowWithDict 回调-->无数据");
                return;
            }

            IvyLog.d($"ByteGame OnShowWithDict 回调-->${JsonConvert.SerializeObject(param)}");
            var launchFrom = param.ContainsKey("launch_from") && "homepage".Equals(param["launch_from"]);
            var location = param.ContainsKey("location") && "sidebar_card".Equals(param["location"]);

            if (launchFrom && location)
            {
                isFromSidebarCard = true;
            }

            IvyLog.d($"ByteGame OnShowWithDict 回调-->isFromSidebarCard:{isFromSidebarCard}");
            IvySdk.Instance.openFromSideBar?.Invoke(isFromSidebarCard);
        }

        private void OnAppHide()
        {
            IvyLog.d("ByteGame OnAppHide");
        }

        public void DeleteAll()
        {
            TT.PlayerPrefs.DeleteAll();
        }

        public void DeleteLocalKey(string key)
        {
            TT.PlayerPrefs.DeleteKey(key);
        }

        public void SetLocalFloat(string key, float value)
        {
            TT.PlayerPrefs.SetFloat(key, value);
        }

        public void SetLocalInt(string key, int value)
        {
            TT.PlayerPrefs.SetInt(key, value);
        }

        public void SetLocalString(string key, string value = "")
        {
            TT.PlayerPrefs.SetString(key, value);
        }

        public float GetLocalFloatSync(string key, float defaultValue)
        {
            return TT.PlayerPrefs.GetFloat(key, defaultValue);
        }

        public int GetLocalIntSync(string key, int defaultValue)
        {
            return TT.PlayerPrefs.GetInt(key, defaultValue);
        }

        public string GetLocalStringSync(string key, string defaultValue)
        {
            return TT.PlayerPrefs.GetString(key, defaultValue);
        }

        public bool HasLocalKey(string key)
        {
            return TT.PlayerPrefs.HasKey(key);
        }

        public Vector3 GetHorizonSafeAreaOffsetAndMenuOffset()
        {
            Vector3 result = Vector3.zero;
            var rect = TT.GetMenuButtonLayout();
            var systemInfo = TT.GetSystemInfo();
            var rate = systemInfo.pixelRatio;
            result.x = (int)(systemInfo.safeArea.left * rate);
            result.y = (int)((systemInfo.screenWidth - systemInfo.safeArea.right) * rate);
            int leftPos = rect.OptGetInt("left");
            int rightPos = rect.OptGetInt("right");
            double offset = (rightPos - leftPos) / 2 * 0.6;
            if (IsIOSSystem())
            {
                offset = -25;
            }
            result.z = (float)((systemInfo.screenWidth - leftPos + offset) * rate);
            return result;
        }

        public Dictionary<string, object> GetSystemInfo()
        {
            var systemInfo = new Dictionary<string, object>
            {
                ["model"] = TT.GetSystemInfo().model,
                ["system"] = TT.GetSystemInfo().system,
                ["platform"] = TT.GetSystemInfo().platform,
                ["brand"] = TT.GetSystemInfo().brand,
                ["language"] = TT.GetSystemInfo().language,
                ["deviceScore"] = TT.GetSystemInfo().deviceScore.ToString(),
                ["hostName"] = TT.GetSystemInfo().hostName,
                ["hostVersion"] = TT.GetSystemInfo().hostVersion,
                ["pixelRatio"] = TT.GetSystemInfo().pixelRatio,
                ["safeArea"] = TT.GetSystemInfo().safeArea,
                ["screenHeight"] = TT.GetSystemInfo().screenHeight,
                ["screenWidth"] = TT.GetSystemInfo().screenWidth,
                ["sdkVersion"] = TT.GetSystemInfo().sdkVersion,
                ["statusBarHeight"] = TT.GetSystemInfo().statusBarHeight
            };

            return systemInfo;
        }

          public string GetOS()
        {
            return TT.GetSystemInfo().platform;
        }

        public bool IsIOSSystem()
        {
            if ("ios".Equals(TT.GetSystemInfo().platform, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private void DecodeUserInfo()
        {
            string content = GetLocalStringSync("user_info", null);
            if (content == null) { 
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

        public void IsLogin(Action<bool> preLogin)
        {
            TT.CheckSession(() =>
            {
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

            }, (string errMsg) =>
            {
                userInfo = new UserInfo();
                preLogin?.Invoke(false);
            });
           
        }

        public void Login(Action<string> loginSuccess, Action<string> loginFail)
        {
            TT.Login((string code, string anonymousCode, bool isLogin) =>
            {
                if (isLogin)
                {
                    //如果需要用户名、头像，使用TT.GetUserInfo
                    CloudStorage.CallFunctionToSelf("user_login", new Dictionary<string, object>
                    {
                        ["appid"] = config.appId,
                        ["code"] = code
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
                }
                else
                {
                    loginFail?.Invoke($"Serve Error");
                }
            }, (string errMsg) =>
            {
                loginFail?.Invoke(errMsg);
            });
        }

        public UserInfo Me()
        {
            return userInfo;
        }


        public void SetClipboardData(string text, Action<bool> result)
        {
            TT.SetClipboardData(text, (status, msg) =>
            {
                IvyLog.d($"set clipboard result:{status};{msg}");
                result?.Invoke(status);
            });
        }

        public void VibratePhone(bool isShort)
        {
            if (isShort)
            {
                TT.VibrateShort(new VibrateShortParam()
                {
                    Success = (res) =>
                    {
                        IvyLog.d("TT.VibrateShort Success");
                    },
                    Fail = (errInfo) =>
                    {
                        IvyLog.d($"TT.VibrateShort Fail, {errInfo.ToString()}");
                    },
                    Complete = () =>
                    {
                        IvyLog.d("TT.VibrateShort Complete");
                    }
                });
            }
            else
            {
                TT.VibrateLong(new VibrateLongParam()
                {
                    Success = (res) =>
                    {
                        IvyLog.d("TT.VibrateShort Success");
                    },
                    Fail = (errInfo) =>
                    {
                        IvyLog.d($"TT.VibrateShort Fail, {errInfo.ToString()}");
                    },
                    Complete = () =>
                    {
                        IvyLog.d("TT.VibrateShort Complete");
                    }
                });
            }
        }

        public void StartRecordScreen(bool isRecordAudio, int maxRecordTime, Action onStartCallback, Action<int, string> onErrorCallback,
            Action<string> onCompleteCallback)
        {
            TT.GetGameRecorder().Start(isRecordAudio, maxRecordTime, () =>
            {
                onStartCallback?.Invoke();
            }, (int errCode, string errMsg) =>
            {
                onErrorCallback?.Invoke(errCode, errMsg);
            }, (string videoPath) =>
            {
                onCompleteCallback?.Invoke(videoPath);
            });
        }

        public void StopRecordScreen(Action<int, string> onErrorCallback, Action<string> onCompleteCallback)
        {
            TT.GetGameRecorder().Stop((string videoPath) =>
            {
                onCompleteCallback?.Invoke(videoPath);
            }, (int errCode, string errMsg) =>
            {
                onErrorCallback?.Invoke(errCode, errMsg);
            });
        }

        public int GetRecordDuration()
        {
            return TT.GetGameRecorder().GetRecordDuration();
        }

        public void ShareMessage(string title, string desc, Action onSuccessCallback, Action<string> onFailedCallback, Action onCanceledCallback)
        {
            JsonData jsonData = new JsonData();
            jsonData["title"] = title;
            jsonData["desc"] = desc;
            TT.ShareAppMessage(jsonData, (Dictionary<string, object> data) =>
            {
                onSuccessCallback?.Invoke();
            }, (string errMsg) =>
            {
                onFailedCallback?.Invoke(errMsg);
            }, () =>
            {
                onCanceledCallback?.Invoke();
            });
        }

        public void OpenCustomerServicePage(Action<bool> onComplete)
        {
            TT.OpenCustomerServicePage((status) =>
            {
                onComplete?.Invoke(status);
            });
        }

        public IvySDKConfig.PlatformConfig GetPlatformConfig()
        {
            return config;
        }

        #region 键盘相关

        private Action<string> _onInput;
        private Action<string> _onConfirm;
        private Action _onComplete;

        public void ShowKeyboard(Action<string> onInput, Action<string> onConfirm, Action onComplete, string defaultValue = "", int maxLength = 999)
        {
            //input
            TT.OnKeyboardInput -= OnTTKeyBoardInput;
            TT.OnKeyboardInput += OnTTKeyBoardInput;
            //confirm
            TT.OnKeyboardConfirm -= OnTTKeyBoardConfirm;
            TT.OnKeyboardConfirm += OnTTKeyBoardConfirm;

            TT.ShowKeyboard();
        }

        public void HideKeyboard()
        {
            _onComplete?.Invoke();
            TT.HideKeyboard();
            _onInput = null;
            _onConfirm = null;
            _onComplete = null;
            TT.OnKeyboardInput -= OnTTKeyBoardInput;
            TT.OnKeyboardConfirm -= OnTTKeyBoardConfirm;
        }

        private void OnTTKeyBoardInput(string value)
        {
            _onInput?.Invoke(value);
        }

        private void OnTTKeyBoardConfirm(string value)
        {
            _onConfirm?.Invoke(value);
        }

        #endregion

        //敏感词替换
        public void ReplaceSensitiveWords(string txt, Action<string> replaceResult)
        {
            TT.ReplaceSensitiveWords(txt, (int status, string errMsg, JsonData jsonData) =>
            {
                if (status == 0)
                {
                    string data = jsonData.OptGetString("audit_content", txt);
                    if (data != null)
                    {
                        replaceResult?.Invoke(data);
                    }
                    else
                    {
                        replaceResult?.Invoke(txt);
                    }
                }
                else
                {
                    IvyLog.e($"replace sensitive words failed:{errMsg}");
                    replaceResult?.Invoke(txt);
                }
            });
        }

        //侧边栏
        public void CheckScene(Action<bool> success, Action complete, Action<int, string> error)
        {
            TT.CheckScene(TTSideBar.SceneEnum.SideBar, success, complete, error);
        }

        public void NavigateToScene(Action success, Action complete, Action<int, string> error)
        {
            var data = new JsonData
            {
                ["scene"] = "sidebar"
            };
            TT.NavigateToScene(data, success, complete, error);
        }

        public bool IsFromSidebarCard()
        {
            return isFromSidebarCard;
        }

        public void Pay(Product product, string payload = null)
        {
            TT.CheckSession(() => {
                var outTradeNo = PaymentManager.GenerateOrderNo(config.appId, config.channel);
                var orderInfoParams = new Dictionary<string, object>
                {
                    ["mode"] = "game", //支付的类型, 目前仅为"game"
                    ["env"] = "0", //环境配置，目前合法值仅为"0"
                    ["currencyType"] = product.currency, // 固定值: CNY。币种
                    ["platform"] = "android", //申请接入时的平台，目前仅为"android"
                    ["buyQuantity"] = product.amount,
                    //金币购买数量，金币数量必须满足：金币数量*金币单价 = 限定价格等级（详见下方 buyQuantity 限制说明。开发者可以在抖音小游戏平台的“支付”tab 设置游戏币单价）
                    ["customId"] = outTradeNo,
                    //todo 待测试 尝试重写支付通知地址
                    ["notify_url"] = MiniGameProxy.payNotifyUrl,
                    ["extraInfo"] = JsonUtility.ToJson(new ExtraAttach
                    {
                        uuid = userInfo.open_id,
                        pkg = config.packageName,
                        pid = product.payId,
                        desc = product.desc,
                        code = config.versionCode,
                    })
                };

                TT.RequestGamePayment(orderInfoParams, () =>
                {
                    QueryPaymentResp resp = new QueryPaymentResp();
                    resp.code = 1;
                    int.TryParse(product.payId, out var id);
                    resp.data = new PayData()
                    {
                        orderId = outTradeNo,
                        billId = id,
                        payload = payload
                    };

                    var respContent = JsonUtility.ToJson(resp);

                    //支付成功回调，向服务器查询当次订单的付费情况
                    CloudStorage.CallFunctionToSelf("pay_queryOrderDouYin",
                        new Dictionary<string, object>()
                        {
                            ["outTradeNo"] = outTradeNo,
                        },
                        (action, content) => { 
                            PaymentManager.ParsePayResponse(action, respContent);
                            EventTracker.ReportPayment(product.amount, product.currency, outTradeNo, product.name, "DY");
                        },
                        msg => {
                            IvyLog.d($"server check payment failed:{msg}");
                            PaymentManager.failPayCall?.Invoke(msg);
                        });
                }, (int code, string errMsg) =>
                {
                    IvyLog.d($"pay failed:{code};{errMsg}");
                    PaymentManager.failPayCall(errMsg);
                });

            }, (string errMsg) =>
            {
                IvyLog.d($"pay failed:{errMsg}");
                PaymentManager.failPayCall(errMsg);
            });
        }

        public void ReportGameStart()
        {
            
        }

        public void CleanAllFileCache()
        {
            TT.ClearAllSavings();
            TT.RestartMiniProgramSync();
        }

        #region 广告
        public void ShowBanner(string tag, BannerPosition bp, Action<AdEvent, string> rlt = null)
        {
            AdManager.ShowBanner(tag, bp, rlt);
        }

        public  void ShowBanner(string tag, int left, int top, int width, Action<AdEvent, string> rlt = null)
        {
            AdManager.ShowBanner(tag, left, top, width, rlt);
        }

        public  void HideBanner(string tag)
        {
            AdManager.HideBanner(tag);
        }

        public  void ShowRewardedVideo(string tag, Action<AdEvent, string, int> rtl = null, bool multiton = false, string[] multitonRewardMsg = null, int multitonRewardTime = 0)
        {
            AdManager.ShowRewardedVideo(tag, rtl, multiton, multitonRewardMsg, multitonRewardTime);
        }

        public  bool HasInterstitial()
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


        #region 缓存

        public void CacheData(string key, string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return;
            }
            TT.Save<string>(key, data);
        }

        public string LoadCachedData(string key)
        {
            return TT.LoadSaving<string>(key);
        }

        public void DeleteCachedData(string key)
        {
            TT.DeleteSaving<string>(key);
        }

        public void ClearAllCachedData()
        {
            TT.ClearAllSavings();
        }

        #endregion

    }

}
#endif