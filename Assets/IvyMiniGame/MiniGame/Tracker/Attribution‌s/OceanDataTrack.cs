#if IVY_SDK_WX
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using IvySdk.MiniGame;
using IvySdk.Utils;
using Newtonsoft.Json;
using WeChatWASM;
using Random = System.Random;


// 巨量
namespace IvySdk.Event
{

    public enum OceanEventType
    {
        Active = 0,
        ActiveRegister = 1,
        ActivePay = 2
    }

    public class OceanDataTrack
    {
        private static object _syncRoot = new object();
        private static OceanDataTrack _instance;
        public static OceanDataTrack Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new OceanDataTrack();
                        }
                    }
                }
                return _instance;
            }
        }

        public string clueToken;
        public Dictionary<string, string> query = new();
        public Dictionary<string, object> AdData = new();

        public string oceanUrl = "https://clue.oceanengine.com/outer/wechat/applet/token/1769935031452675";
        public string oceanToken = "08C0BD1FA42EE7091A7DE70FD19277D2";


        public void OnShow(LaunchOptionsGame launchOptionsSync)
        {
            try
            {
                clueToken = query.GetValueOrDefault("clue_token", "");
                if (launchOptionsSync.query == null || launchOptionsSync.query.Count < 1)
                {
                    // 本地缓存进行初始化
                    var localStringSync = MiniGameProxy.GetLocalStringSync("wx_query", "");
                    if (!string.IsNullOrEmpty(localStringSync))
                    {
                        query = JsonConvert.DeserializeObject<Dictionary<string, string>>(localStringSync);
                    }
                }
                else
                {
                    query = launchOptionsSync.query;
                    MiniGameProxy.SetLocalString("wx_query", JsonConvert.SerializeObject(query));
                }

                if (string.IsNullOrEmpty(clueToken))
                {
                    // 尝试从本地缓存获取
                    clueToken = MiniGameProxy.GetLocalStringSync("clue_token", "");
                }
                else
                {
                    // 本地缓存
                    MiniGameProxy.SetLocalString("clue_token", clueToken);
                }
                IvyLog.d($"微信冷启动query:{JsonConvert.SerializeObject(query)}");
                string req_id = query.GetValueOrDefault("req_id", "");
                if (string.IsNullOrEmpty(req_id))
                {
                    req_id = query.GetValueOrDefault("request_id", "");
                }
                if (!string.IsNullOrEmpty(req_id))
                {
                    GetDataFromServer(req_id);
                }
                // 激活事件回传
                if (!MiniGameProxy.HasLocalKey("IsRangerActived"))
                {
                    OceanDataTrack.Instance.Event(OceanEventType.Active);
                    MiniGameProxy.SetLocalString("IsRangerActived", "1");
                }
            }
            catch (Exception e)
            {
                IvyLog.d($"{e.Message}");
            }
        }

        private void GetDataFromServer(string reqid)
        {
            IvySDKConfig.PlatformConfig platformConfig = MiniGameProxy.GetPlatformConfig();
            Dictionary<string, object> param = new Dictionary<string, object>
            {
                { "channel", "juliang" },
                { "appid", platformConfig.appId },
                { "requestId", reqid },
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
                            AdData = (Dictionary<string, object>)dataObj;
                        }
                    }
                    IvyLog.d($"Ocean get source success:{result}");
                }
            }, (err) =>
            {
                IvyLog.d($"Ocean get source fail:{err}");
            });
        }

        public void Event(OceanEventType oceanEventType, int payNum = 0)
        {

            if (!string.IsNullOrEmpty(clueToken) && query != null && query.Count > 0)
            {
                var paramMap = new Dictionary<string, object>
                {
                    ["event_type"] = oceanEventType.GetHashCode().ToString()
                };
                if (oceanEventType == OceanEventType.ActivePay)
                {
                    var props = new Dictionary<string, object>
                    {
                        ["pay_amount"] = payNum
                    };
                    paramMap["props"] = props;
                }

                TransferToHttpPost(paramMap);
            }

        }

        private void TransferToHttpPost(Dictionary<string, object> paramMap)
        {
            if (string.IsNullOrEmpty(clueToken))
            {
                IvyLog.d("MiniGameConfig.clueToken is null.");
                return;
                // "clue_token": "xxx", // 用户跳转小程序时携带的clue_token参数
                // "union_id": "xxx",   // 微信union_id
                // "open_id": "xxx",    // 微信open_id，必须回传
                // "event_type": "0",  // 事件类型，string格式
                // "props": {
                //     "pay_amount": 123 // 当event_type为2，使用付费类出价产品时需上报，单位为分，格式int
                // } 
            }
            MiniGame.UserInfo userInfo = MiniGameProxy.Me();
            paramMap["clue_token"] = clueToken;
            paramMap["union_id"] = userInfo?.union_id ?? "";
            paramMap["open_id"] = userInfo?.open_id ?? "";

            Utils.HttpUtil.I.HttpEventPost(GetEventUrl, null, Newtonsoft.Json.JsonConvert.SerializeObject(paramMap));
        }

        private string GetEventUrl(string eventKey)
        {
            // 假设 token, nonce 和 dto 已经在作用域内定义
            var token = oceanToken;
            // 使用 Random 类生成一个随机整数
            var random = new Random();
            // nonce现在包含一个随机整数，可以转换为字符串用于之前的数组
            var nonce = random.Next().ToString();
            var dto = DateTimeOffset.Now; // or any other way you obtain the dto
            var timestamp = dto.ToUnixTimeSeconds().ToString();
            // 创建一个字符串数组并进行排序
            string[] arr = { token, nonce, timestamp };
            Array.Sort(arr);

            // 使用 StringBuilder 组合字符串
            StringBuilder buffer = new StringBuilder();
            foreach (var t in arr)
            {
                buffer.Append(t);
            }

            // 进行sha1加密生成签名
            string signature;
            using (var sha1 = SHA1.Create())
            {
                byte[] bufferBytes = Encoding.UTF8.GetBytes(buffer.ToString());
                byte[] hashBytes = sha1.ComputeHash(bufferBytes);
                signature = BitConverter.ToString(hashBytes);
            }


            var url = $"{oceanUrl}?timestamp={timestamp}&nonce={nonce}&signature={signature}";
            IvyLog.d($"数据回传URL:{url}");
            return url;
        }
    }

}
#endif