using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IvySdk.Utils;


namespace IvySdk.MiniGame
{
    #region 数据定义

    internal interface PaymentInterface
    {
        internal void RequestPayment(string payId, string desc, int amount, string payload);
    }

    [System.Serializable]
    public class PayConfigData
    {
        public List<Product> data;
    }

    //附加数据
    [Serializable]
    public class ExtraAttach
    {
        [SerializeField] public string uuid;
        [SerializeField] public string pid;
        [SerializeField] public string pkg;
        [SerializeField] public string desc;
        [SerializeField] public string code;
    }

    [Serializable]
    public class PayData
    {
        [SerializeField] public int billId; // 计费点
        [SerializeField] public string orderId; //订单号
        [SerializeField] public string payload; // 订单追加信息
    }

    [Serializable]
    internal class QueryPaymentResp
    {
        [SerializeField] public int code;
        [SerializeField] public string message;
        [SerializeField] public PayData data;
    }

    #endregion

    public static class PaymentManager 
    {
        //private static PaymentManager _instance;

        private static readonly Dictionary<string, Product> _payDict = new Dictionary<string, Product>();

   

        //public static PaymentManager Instance()
        //{
        //    if (_instance == null)
        //    {
        //        _instance = new PaymentManager();
        //    }
        //    return _instance;
        //}

        //private PaymentManager()
        //{
            
        //}

        public static void LoadPayConfig()
        {
            TextAsset asset = Resources.Load<TextAsset>("payConfig");
            if (asset == null)
            {
                Debug.LogWarning("payConfig.json not found in Resources!");
                return;
            }

            // 解析 JSON
            PayConfigData config = JsonConvert.DeserializeObject<PayConfigData>(asset.text);
            List<Product> payIdConfigs = config.data;

            // 打印结果
            foreach (var item in payIdConfigs)
            {
                Debug.Log(JsonConvert.SerializeObject(item, Formatting.Indented));
                _payDict.Add(item.payId, item);
            }
        }

        //通过付费id查询付费配置
        public static Product GetPayConfig(string payId)
        {
            return _payDict.TryGetValue(payId, out var pConfig) ? pConfig : null;
        }

        /// <summary>
        /// 字符串md5计算
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static string MD5(this string s)
        {
            using var provider = System.Security.Cryptography.MD5.Create();
            var builder = new System.Text.StringBuilder();

            foreach (var b in provider.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s)))
                builder.Append(b.ToString("x2").ToLower());

            return builder.ToString();
        }

        private static readonly System.Random random = new System.Random((int)Utils.TimeManager.GetTimeStamp());

        //随机字符串
        private static string RandomString(int length = 8)
        {
            const string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var charsArr = new char[length];
            for (var i = 0; i < charsArr.Length; i++)
            {
                charsArr[i] = characters[random.Next(characters.Length)];
            }

            return new string(charsArr);
        }

        //获取时间戳，单位 毫秒
        private static long GetTimestamp()
        {
            return (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000;
        }

        //随机md5产生订单号
        public static string GenerateOrderNo(string app_id, string channel)
        {
            var ch = "WX";
            switch (channel.ToLower())
            {
                case "douyin":
                    {
                        ch = "DY";
                        break;
                    }
                case "dy":
                    {
                        ch = "DY";
                        break;
                    }
            }

            var length = app_id.Length;
            length += ch.Length;
            var timestamp = GetTimestamp().ToString();
            length += timestamp.Length;
            var randomStr = "";
            if (length < 32)
            {
                randomStr = RandomString(32 - length);
            }
            return $"{app_id}{ch}{randomStr}{GetTimestamp()}";
        }

        internal static Action<string, PayData> successPayCall;
        internal static Action<string> failPayCall;

        public static void ParsePayResponse(string action, string content)
        {
            switch (action)
            {
                //todo 增加其他查询结果
                case "pay_queryOrderDouYin":
                case "pay_queryOrderLocal":
                    {
                        OperatorDispatcher.I.AddActionToQueue(() =>
                        {
                            var data = JsonUtility.FromJson<QueryPaymentResp>(content);
                            //var res = JsonUtility.ToJson(data.data);
                            IvyLog.d($" ----[ParsePayResponse]  {action} --- {data.data}");
                            successPayCall?.Invoke(data.data.billId.ToString(), data.data);
                        });
                        break;
                    }
            }
        }

    }
}