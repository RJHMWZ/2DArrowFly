#if IVY_SDK_WX
using System;
using System.Collections.Generic;
using System.Net;
using IvySdk.MiniGame;
using IvySdk.Utils;
using Newtonsoft.Json;
using UnityEngine;
using WeChatWASM;

//广点通
namespace IvySdk.Event
{

    public enum ADQEventType
    {
        REGISTER,
        RE_ACTIVE,
        PURCHASE,
    }

    public class ADQDataTrack
    {

        private static object _sync = new object();

        private static ADQDataTrack _instance;

        public static ADQDataTrack Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_sync)
                    {
                        if (_instance == null)
                        {
                            _instance = new ADQDataTrack();
                        }
                    }
                }
                return _instance;
            }
        }

        private string base_url = "http://tracking.e.qq.com/conv";

        private string gdt_vid;
        private string weixinadinfo;
        private string cb;
        public Dictionary<string, string> query = new();
        public Dictionary<string, object> ADQData = new();

        public void OnShow(LaunchOptionsGame launchOptions)
        {

            if (launchOptions == null)
            {
                IvyLog.d("ADQ WX OnShow launchOptions is null");
                return;
            }

            query = launchOptions.query;
            IvyLog.d($"ADQ WX OnShow query::{query}");
            if (query == null || query.Count <= 0)
            {
                string localQuery = MiniGameProxy.GetLocalStringSync("adq_query", "");
                if (string.IsNullOrEmpty(localQuery))
                {
                    IvyLog.d("ADQ local cached query is null");
                    return;
                }
                query = JsonConvert.DeserializeObject<Dictionary<string, string>>(localQuery);
            }
            MiniGameProxy.SetLocalString("adq_query", JsonConvert.SerializeObject(query));
            query.TryGetValue("gdt_vid", out gdt_vid);
            query.TryGetValue("weixinadinfo", out weixinadinfo);
            if (!string.IsNullOrEmpty(gdt_vid))
            {
                GetDataFromServer(gdt_vid);
            }
            #region 广点通归因
            if (MiniGameProxy.GetLocalIntSync("IsADQActived", 1) == 0)
            {
                ADQDataTrack.Instance.TrackEvent(ADQEventType.REGISTER);
                MiniGameProxy.SetLocalInt("IsADQActived", 1);
            }
            #endregion

        }

        private void GetDataFromServer(string click_id)
        {
            IvySDKConfig.PlatformConfig platformConfig = MiniGameProxy.GetPlatformConfig();
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "channel", "gdt" },
                { "appid", platformConfig.appId },
                { "requestId", click_id },
            };
            Cloud.CloudStorage.CallFunctionToSelf("api_get_click", param, (string api, string result) =>
            {
                if (api.Equals("api_get_click") && !string.IsNullOrEmpty(result))
                {
                    Dictionary<string, object> data = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
                    if (data.TryGetValue("code", out object code) && (int)code == 1)
                    {
                        if (data.TryGetValue("data", out object dataObj) && dataObj != null)
                        {
                            ADQData = (Dictionary<string, object>)dataObj;
                            ADQData.TryGetValue("callbackurl", out var tmp);
                            cb = tmp?.ToString();
                        }
                    }
                    IvyLog.d($"ADQ get source success:{result}");
                }
            }, (err) =>
            {
                IvyLog.d($"ADQ get source fail:{err}");
            });
        }

        /// <summary>
        /// 单位： 分
        /// </summary>
        /// <param name="price"></param>
        public void TrackPurchase(int price)
        {

            Dictionary<string, object> p = new Dictionary<string, object>();
            Dictionary<string, int> action_param = new Dictionary<string, int>();
            action_param.Add("value", price);
            p.Add("action_param", action_param);
            TrackEvent(ADQEventType.PURCHASE, p);
        }

        public void TrackEvent(ADQEventType eventType, Dictionary<string, object> body = null)
        {
            if (!string.IsNullOrEmpty(gdt_vid) && query != null && query.Count > 0)
            {
                if (body == null)
                {
                    body = new Dictionary<string, object>();
                }
                body.Add("action_time", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Dictionary<string, string> userId = new Dictionary<string, string>();

                MiniGame.UserInfo userInfo = MiniGameProxy.Me();
                IvySDKConfig.PlatformConfig platformConfig = MiniGameProxy.GetPlatformConfig();

                if (!string.IsNullOrEmpty(platformConfig.miMasterAppId))
                {
                    userId.Add("wechat_app_id", platformConfig.miMasterAppId);
                }
                if (userInfo != null) {
                    if (!string.IsNullOrEmpty(userInfo.open_id))
                    {
                        userId.Add("wechat_openid", userInfo.open_id);
                    }
                    else
                    if (!string.IsNullOrEmpty(userInfo.union_id))
                    {
                        userId.Add("wechat_unionid", userInfo.union_id);
                    }
                }
                body.Add("user_id", userId);
                Dictionary<string, string> trace = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(gdt_vid))
                {
                    trace.Add("click_id", gdt_vid);
                }
                body.Add("trace", trace);

                switch (eventType)
                {
                    case ADQEventType.REGISTER:
                        body.Add("action_type", "REGISTER");
                        break;
                    case ADQEventType.PURCHASE:
                        body.Add("action_type", "PURCHASE");
                        break;
                    case ADQEventType.RE_ACTIVE:
                        body.Add("action_type", "RE_ACTIVE");
                        break;
                }
                HttpUtil.I.HttpEventPost(GetUrl, "", JsonConvert.SerializeObject(body));
            }

        }

        private string GetUrl(string uselessKey)
        {
            if (!string.IsNullOrEmpty(cb))
            {
                return base_url + "?cb=" + cb;
            }
            return base_url;
        }

    }

}
#endif