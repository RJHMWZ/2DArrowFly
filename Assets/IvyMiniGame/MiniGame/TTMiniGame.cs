#if IVY_SDK_TT
using IvySdk;
using IvySdk.MiniGame;
using IvySdk.Tracker;
using IvySdk.Utils;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using TTSDK;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class TTMiniGame : IMiniGame
{

    [System.Serializable]
    internal class QueryResp
    {
        public int code;
        public string msg;
        public List<QueryOrderData> data;
        public long timestamp;
    }

    [System.Serializable]
    internal class QueryOrderData
    {
        public string app_id;
        public string uuid;
        public string rp_date;
        public string country;
        public string merchant_transaction_id;
        public string order_id;
        public string environment;
        public int order_status;
        public string product_id;
        public string name;
        public double usd;
        public string payload;
        public int pay_id;
    }

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
        config = ivySdkConfig.tt;
        if (config == null)
        {
            IvyLog.e("unable load tt config");
            action?.Invoke(false);
            return;
        }

        if (string.IsNullOrEmpty(config.appId))
        {
            IvyLog.e($"invalid tt config:{config}");
            action?.Invoke(false);
            return;
        }
        TT.InitSDK((code, env) =>
        {
            if (code == 0)
            {
                IvyLog.d("TT SDK 初始化成功");
                DecodeUserInfo();
                RegisterAppLifecycle();
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
            }
            else
            {
                IvyLog.e($"TT SDk init failed:{code}");
                //错误码说明：
                //Code 说明
                //0 ---- 无错误
                //1 ---- TT Unity SDK 版本不支持
                //2 ---- Unity Engine 版本不被支持
                action?.Invoke(false);
            }
        });
    }

    private void RegisterAppLifecycle()
    {
        TTAppLifeCycle lifeCycle = TT.GetAppLifeCycle();

        // 监听显示事件
        lifeCycle.OnShow += (options) =>
        {
            IvyLog.d("游戏进入前台");
            IvySdk.IvySdk.Instance.onAppShow?.Invoke();
        };

        // 监听隐藏事件
        lifeCycle.OnHide += () =>
        {
            IvyLog.d("游戏进入后台");
            IvySdk.IvySdk.Instance.onAppHide?.Invoke();
        };
    }

    public string GetOS()
    {
        return TT.GetSystemInfo().platform;
    }

    public IvySDKConfig.PlatformConfig GetPlatformConfig()
    {
        return config;
    }

    public Dictionary<string, object> GetSystemInfo()
    {
        TTSystemInfo _systemInfo = TT.GetSystemInfo();
        var systemInfo = new Dictionary<string, object>
        {
            ["model"] = _systemInfo.model,
            ["system"] = _systemInfo.system,
            ["platform"] = _systemInfo.platform,
            ["brand"] = _systemInfo.brand,
            ["language"] = _systemInfo.language,
            ["deviceScore"] = _systemInfo.deviceScore.ToString(),
            ["hostName"] = _systemInfo.hostName,
            ["hostVersion"] = _systemInfo.hostVersion,
            ["pixelRatio"] = _systemInfo.pixelRatio,
            ["safeArea"] = _systemInfo.safeArea,
            ["screenHeight"] = _systemInfo.screenHeight,
            ["screenWidth"] = _systemInfo.screenWidth,
            ["sdkVersion"] = _systemInfo.sdkVersion,
            ["statusBarHeight"] = _systemInfo.statusBarHeight
        };
        return systemInfo;
    }

    public bool IsIOSSystem()
    {
        if ("ios".Equals(TT.GetSystemInfo().platform, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public void IsLogin(Action<bool> preLogin)
    {
        DecodeUserInfo();
        if (userInfo != null)
        {
            if (!string.IsNullOrEmpty(userInfo.access_token) && !string.IsNullOrEmpty(userInfo.open_id) && !string.IsNullOrEmpty(userInfo.refresh_token))
            {
                var data = new Dictionary<string, object>
                {
                    ["app_id"] = config.appId,
                    ["token"] = userInfo.refresh_token,
                };
                var dict = new Dictionary<string, object>
                {
                    ["is_encrypt"] = false,
                    ["data"] = data
                };

                //var content = JsonConvert.SerializeObject(dict);
                HttpUtil.I.HttpPost("https://verifytest.ivymobile.com/api/external/v1/login/token_refresh_tiktok", dict, (net_code, respContent) =>
                {
                    if (net_code == 200)
                    {
                        try
                        {
                            var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(respContent);
                            int resp_code = Convert.ToInt32(resp["code"]);
                            if (resp_code == 0)
                            {
                                var resp_data = JsonConvert.DeserializeObject<Dictionary<string, object>>(resp["data"].ToString());
                                string access_token = resp_data["access_token"].ToString();
                                string open_id = resp_data["open_id"].ToString();
                                string refresh_token = resp_data["refresh_token"].ToString();

                                if (!string.IsNullOrEmpty(access_token) && !string.IsNullOrEmpty(open_id) && !string.IsNullOrEmpty(refresh_token))
                                {
                                    userInfo.open_id = open_id;
                                    userInfo.refresh_token = refresh_token;
                                    userInfo.access_token = access_token;

                                    var str = JsonConvert.SerializeObject(userInfo);

                                    SetLocalString("user_info", str);

                                    preLogin?.Invoke(true);
                                    EventTracker.InitGravityWithOpenId(userInfo.open_id, config);
                                    EventTracker.ReportRegister();
                                }
                                else
                                {
                                    preLogin?.Invoke(false);
                                }
                            }
                            else
                            {
                                preLogin?.Invoke(false);
                            }
                        }
                        catch (Exception)
                        {
                            preLogin?.Invoke(false);
                        }
                    }
                    else
                    {
                        preLogin?.Invoke(false);
                    }
                });
            }
            else
            {
                preLogin?.Invoke(false);
            }
        }
        else
        {
            preLogin?.Invoke(false);
        }
    }

    public void Login(Action<string> loginSuccess, Action<string> loginFail)
    {
        TT.Login((string code) =>
        {
            var data = new Dictionary<string, object>
            {
                ["app_id"] = config.appId,
                ["token"] = code
            };
            var dict = new Dictionary<string, object>
            {
                ["is_encrypt"] = false,
                ["data"] = data
            };
            //var content = JsonConvert.SerializeObject(dict);
            HttpUtil.I.HttpPost("https://verifytest.ivymobile.com/api/external/v1/login/tiktok", dict, (net_code, respContent) =>
            {
                if (net_code == 200)
                {
                    try
                    {
                        var resp = JsonConvert.DeserializeObject<Dictionary<string, object>>(respContent);
                        int resp_code = Convert.ToInt32(resp["code"]);
                        if (resp_code == 0)
                        {
                            var resp_data = JsonConvert.DeserializeObject<Dictionary<string, object>>(resp["data"].ToString());
                            string access_token = resp_data["access_token"].ToString();
                            string open_id = resp_data["open_id"].ToString();
                            string refresh_token = resp_data["refresh_token"].ToString();

                            if (!string.IsNullOrEmpty(access_token) && !string.IsNullOrEmpty(open_id) && !string.IsNullOrEmpty(refresh_token))
                            {
                                userInfo = new UserInfo();
                                userInfo.open_id = open_id;
                                userInfo.refresh_token = refresh_token;
                                userInfo.access_token = access_token;
                                userInfo.login_code = code;

                                var str = JsonConvert.SerializeObject(userInfo);

                                SetLocalString("user_info", str);

                                loginSuccess?.Invoke(str);

                                EventTracker.InitGravityWithOpenId(userInfo.open_id, config);
                                EventTracker.ReportRegister();
                                EventTracker.AddUserProperty("open_id", open_id);
                              
                                EventTracker.TrackEvent("sdk_user_login", new Dictionary<string, object>());
                            }
                            else
                            {
                                IvyLog.e($"login failed: invalid server result");
                                loginFail?.Invoke("invalid server result");
                            }
                        }
                        else
                        {
                            IvyLog.e($"login failed:{resp_code}");
                            loginFail?.Invoke(resp_code.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        IvyLog.e($"login failed:{ex.Message}");
                        loginFail?.Invoke(ex.Message);
                    }
                }
                else
                {
                    IvyLog.e($"login failed:{net_code};{respContent}");
                    loginFail?.Invoke(net_code.ToString());
                }
            });
        }, (string errMsg) =>
        {
            loginFail?.Invoke(errMsg);
        });
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
            userInfo = JsonConvert.DeserializeObject<UserInfo>(content);
        }
        catch (Exception ex) { }

    }
    public UserInfo Me()
    {
        return userInfo;
    }

    public void OpenCustomerServicePage(Action<bool> onComplete)
    {
        onComplete?.Invoke(false);
    }

    public void Pay(IvySdk.MiniGame.Product product, string payload = null)
    {
        if (userInfo == null)
        {
            PaymentManager.failPayCall.Invoke(product.payId);
            return;
        }
        if (string.IsNullOrEmpty(userInfo.access_token) || string.IsNullOrEmpty(userInfo.open_id))
        {
            PaymentManager.failPayCall.Invoke(product.payId);
            return;
        }
        var sku_data = new Dictionary<string, object>();
        sku_data.Add("pay_id", product.payId);
        sku_data.Add("id", product.skuId);
        sku_data.Add("price", product.price);
        sku_data.Add("price_amount", product.amount);
        sku_data.Add("usd", product.amount);
        sku_data.Add("currency", product.currency);
        sku_data.Add("name", product.name);
        sku_data.Add("title", product.name);
        sku_data.Add("desc", product.desc);
        sku_data.Add("payload", payload ?? "");
        sku_data.Add("quantity", 1);
        sku_data.Add("quantity_unit", "个");
        sku_data.Add("image_url", product.imageUrl ?? "");

        var data = new Dictionary<string, object>();
        data.Add("app_id", config.appId);
        data.Add("token", userInfo.access_token);
        data.Add("uuid", userInfo.open_id);
        data.Add("country", TT.GetSystemInfo().language);
        data.Add("is_sandbox", false);
        data.Add("sku_json", sku_data);

        var content = new Dictionary<string, object>();
        content.Add("is_encrypt", false);
        content.Add("data", data);

        //var content_str = JsonConvert.SerializeObject(content);
        HttpUtil.I.HttpPost("https://verify.ivymobile.com/api/external/v1/purchase/pre_tiktok", content, (code, resp) =>
        {
            try
            {
                if (code == 200)
                {
                    try
                    {
                        var resp_dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(resp);
                        int resp_code = Convert.ToInt32(resp_dict["code"]);
                        if (resp_code == 0)
                        {
                            var resp_data = JsonConvert.DeserializeObject<Dictionary<string, object>>(resp_dict["data"].ToString());
                            string merchant_transaction_id = resp_data["merchant_transaction_id"].ToString();
                            string order_id = resp_data["order_id"].ToString();
                            if (!string.IsNullOrEmpty(merchant_transaction_id) && !string.IsNullOrEmpty(order_id))
                            {
                                TT.Pay(new TTPayParam
                                {
                                    trade_order_id = order_id,

                                    success = (data) =>
                                    {
                                        IvyLog.d("付款成功， 准备开始校验");
                                        var su_data = new Dictionary<string, object>();
                                        su_data.Add("app_id", config.appId);
                                        su_data.Add("action", "success");
                                        su_data.Add("merchant_transaction_id", merchant_transaction_id);

                                        var su_body = new Dictionary<string, object>();
                                        su_body.Add("is_encrypt", false);
                                        su_body.Add("data", su_data);

                                        // var su_str = JsonConvert.SerializeObject(su_body);

                                        HttpUtil.I.HttpPost("https://verify.ivymobile.com/api/external/v1/purchase/tiktok", su_body, (su_code, su_resp) =>
                                        {
                                            if (su_code == 200)
                                            {
                                                try
                                                {
                                                    var su_resp_data = JsonConvert.DeserializeObject<Dictionary<string, object>>(su_resp);
                                                    var result = su_resp_data["data"];
                                                    bool value = Convert.ToBoolean(result);
                                                    if (value)
                                                    {
                                                        var _dict = new Dictionary<string, object>();
                                                        _dict.Add("is_encrypt", false);
                                                        var _dict_data = new Dictionary<string, object>();
                                                        _dict_data.Add("app_id", config.appId);
                                                        _dict_data.Add("merchant_transaction_id", merchant_transaction_id);
                                                        _dict_data.Add("token", userInfo.access_token);
                                                        _dict.Add("data", _dict_data);

                                                        //var _dict_str = JsonConvert.SerializeObject(_dict);
                                                        OperatorDispatcher.I.StartCoroutine(ReCheckPaymentResult(product, order_id, product.payId, _dict, merchant_transaction_id, payload, 0));

                                                    }
                                                    else
                                                    {
                                                        PaymentManager.failPayCall.Invoke(product.payId);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    PaymentManager.failPayCall.Invoke(product.payId);
                                                }
                                            }
                                            else
                                            {
                                                PaymentManager.failPayCall.Invoke(product.payId);
                                            }
                                        });
                                    },
                                    fail = (error) =>
                                    {
                                        IvyLog.d($"支付失败: {error}");
                                        PaymentManager.failPayCall.Invoke(product.payId);
                                    }
                                });
                            }
                            else
                            {
                                IvyLog.d($"{product.payId} pre order failed;{code};{resp}");
                                PaymentManager.failPayCall.Invoke(product.payId);
                            }
                        }
                        else
                        {
                            IvyLog.d($"{product.payId} pre order failed;{code};{resp}");
                            PaymentManager.failPayCall.Invoke(product.payId);
                        }
                    }
                    catch
                    {
                        PaymentManager.failPayCall.Invoke(product.payId);
                    }
                }
                else
                {
                    IvyLog.d($"{product.payId} pre order failed;{code};{resp}");
                    PaymentManager.failPayCall.Invoke(product.payId);
                }
            }
            catch (Exception)
            {
                IvyLog.d($"{product.payId} pre order failed;{code};{resp}");
                PaymentManager.failPayCall.Invoke(product.payId);
            }
        });
    }

    private IEnumerator ReCheckPaymentResult(Product product, string order_id, string pay_id, Dictionary<string, object> content, string merchant_transaction_id, string payload, int count)
    {
        if (count > 10)
        {
            PaymentManager.failPayCall?.Invoke(pay_id);
            yield break;
        }
        yield return new WaitForSeconds(3f);
        count++;

        HttpUtil.I.HttpPost("https://verify.ivymobile.com/api/external/v1/purchase/verify_google", content, (code, resp) =>
        {
            if (code == 200)
            {
                try
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(resp);
                    var result = dict["data"];
                    bool value = Convert.ToBoolean(result);
                    if (value)
                    {
                        PayData payData = new PayData();
                        payData.billId = int.Parse(pay_id);
                        payData.orderId = merchant_transaction_id;
                        payData.payload = payload;
                        PaymentManager.successPayCall.Invoke(pay_id, payData);
                        EventTracker.ReportPayment(product.amount, product.currency, order_id, product.name, "TT");
                    }
                    else
                    {
                        OperatorDispatcher.I.StartCoroutine(ReCheckPaymentResult(product, order_id, pay_id, content, merchant_transaction_id, payload, count));
                    }
                } catch {
                    OperatorDispatcher.I.StartCoroutine(ReCheckPaymentResult(product, order_id, pay_id, content, merchant_transaction_id, payload, count));
                }
            } else
            {
                OperatorDispatcher.I.StartCoroutine(ReCheckPaymentResult(product, order_id, pay_id, content, merchant_transaction_id, payload, count));
            }
        });
    }

    public void ConfirmPay(string merchant_transaction_id, Action<bool> callback)
    {
        var _dict = new Dictionary<string, object>();
        _dict.Add("is_encrypt", false);
        var _dict_data = new Dictionary<string, object>();
        _dict_data.Add("app_id", config.appId);
        _dict_data.Add("merchant_transaction_id", merchant_transaction_id);
        _dict.Add("data", _dict_data);
        //var _dict_str = JsonConvert.SerializeObject(_dict);
        HttpUtil.I.HttpPost("https://verify.ivymobile.com/api/external/v1/purchase/consume_tiktok", _dict, (code, resp) =>
        {
            if (code == 200) {
                try
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(resp);
                    var result = dict["data"];
                    bool value = Convert.ToBoolean(result);
                    if (value)
                    {
                        callback?.Invoke(true);
                    }
                    else
                    {
                        callback?.Invoke(false);
                    }
                } catch
                {
                    callback?.Invoke(false);
                }
            } else
            {
                callback?.Invoke(false);
            }
        });

    }

    public void Query()
    {
        if (userInfo == null)
        {
            return;
        }
        if (string.IsNullOrEmpty(userInfo.open_id))
        {
            return;
        }
        var param = new Dictionary<string, object>();
        param.Add("app_id", config.appId);
        param.Add("uuid", userInfo.open_id);
        HttpUtil.I.HttpGet("https://verify.ivymobile.com/api/external/v1/purchase/unconsume_tiktok", param, (code, resp) =>
        {
            try
            {
                if (code == 200)
                {
                    var queryResp = JsonConvert.DeserializeObject<QueryResp>(resp);
                    var list = queryResp.data;

                    foreach (var item in list)
                    {
                        PayData payData = new PayData();
                        payData.billId = item.pay_id;
                        payData.orderId = item.merchant_transaction_id;
                        payData.payload = item.payload;
                        PaymentManager.successPayCall.Invoke(item.pay_id.ToString(), payData);
                    }
                }
            }
            catch (Exception) { }
        });
    }

    public void ReportGameStart()
    {

    }

    public Vector3 GetHorizonSafeAreaOffsetAndMenuOffset()
    {
        Vector3 result = Vector3.zero;
        SafeArea safeArea = TT.GetSystemInfo().safeArea;
        var systemInfo = TT.GetSystemInfo();
        var rate = systemInfo.pixelRatio;
        result.x = (int)(systemInfo.safeArea.left * rate);
        result.y = (int)((systemInfo.screenWidth - systemInfo.safeArea.right) * rate);
        double offset = 0;
        if (IsIOSSystem())
        {
            offset = -25;
        }
        result.z = (float)((systemInfo.screenWidth + offset) * rate);
        return result;
    }

    public void CleanAllFileCache()
    {
        TT.CleanAllFileCache((status) =>
        {

        });
    }

    public void DeleteAll()
    {
        TT.PlayerPrefs.DeleteAll();
    }

    public void DeleteLocalKey(string key)
    {
        TT.PlayerPrefs.DeleteKey(key);
    }

    public bool HasLocalKey(string key)
    {
        return TT.PlayerPrefs.HasKey(key);
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


    public void SetClipboardData(string text, Action<bool> result)
    {
        result?.Invoke(false);
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

    public void ShareMessage(string title, string desc, string imageUrl, Action onSuccessCallback, Action<string> onFailedCallback, Action onCanceledCallback)
    {
        ShareAppMessageParam param = new ShareAppMessageParam
        {
            ImageUrl = imageUrl ?? "",  // 分享图片地址
            Subtitle = desc ?? "",                           // 子标题
            TemplateType = 1,                             // 模板样式：1 或 2
            Title = title ?? "",                             // 主标题

            Success = () =>
            {
                IvyLog.d("分享成功");
                onSuccessCallback?.Invoke();
            },
            Fail = (error) =>
            {
                IvyLog.d($"分享失败: {error.ErrMsg}");
                onFailedCallback?.Invoke(error.ErrMsg);
            },
            Complete = () =>
            {
                IvyLog.d("分享完成（成功或失败都会调用）");
                onCanceledCallback?.Invoke();
            }
        };
        TT.ShareAppMessage(param);
    }


    #region 桌面快捷方式
    public void AddShortcut(Action<bool> callback)
    {
        if (CanIUse.AddShortcut)
        {
            TT.AddShortcut(callback);
        }
        else
        {
            callback?.Invoke(false);
        }
    }

    public void GetShortcutMissionReward(Action<bool> callback)
    {
        if (CanIUse.GetShortcutMissionReward)
        {
            TT.GetShortcutMissionReward(new GetShortcutMissionRewardParam
            {
                Success = (result) =>
                {
                    if (result.CanReceiveReward)
                    {
                        IvyLog.d("用户可以领取快捷方式奖励");
                        callback?.Invoke(true);
                    }
                    else
                    {
                        IvyLog.d("用户暂不可领取奖励");
                        callback?.Invoke(false);
                    }
                },
                Fail = (error) =>
                {
                    IvyLog.d($"获取失败: {error.ErrMsg}");
                    callback?.Invoke(false);
                },
                Complete = () =>
                {
                    IvyLog.d("请求完成");
                    //callback?.Invoke(false);
                }
            });
        }
        else
        {
            callback?.Invoke(false);
        }
    }
    #endregion


    #region 侧边栏
    public void StartEntranceMission(Action<bool> callback)
    {
        if (CanIUse.StartEntranceMission)
        {
            TT.StartEntranceMission(new TTStartEntranceMissionParam
            {
                success = (result) =>
                {
                    if (result.is_sufficient)
                    {
                        callback?.Invoke(true);
                    }
                    else
                    {
                        callback?.Invoke(false);
                    }
                },
                fail = (error) =>
                {
                    callback?.Invoke(false);
                },
                complete = (error) => { }
            });
        }
        else
        {
            callback?.Invoke(false);
        }
    }

    public void GetEntranceMissionReward(Action<bool> callback)
    {
        if (CanIUse.GetEntranceMissionReward)
        {
            TT.GetEntranceMissionReward(new TTGetEntranceMissionRewardParam
            {
                success = (result) =>
                {
                    if (result.canReceiveReward)
                    {
                        callback?.Invoke(true);
                    }
                    else
                    {
                        callback?.Invoke(false);
                    }
                },
                fail = (error) =>
                {
                    callback?.Invoke(false);
                },
                complete = (error) => { }
            });
        }
        else
        {
            callback?.Invoke(false);
        }
    }
    #endregion

    #region 缓存

        public void CacheData(string key, string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return;
            }
            TT.Save<string>(data, key);
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
#endif