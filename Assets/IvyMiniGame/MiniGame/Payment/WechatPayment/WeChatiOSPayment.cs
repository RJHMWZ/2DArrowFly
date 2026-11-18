using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IvySdk.MiniGame
{
    [System.Serializable]
    internal struct ServiceMessage
    {
        public string client_ip;
        public string platform;
        public string tradeNo;
        public string payId;
    }

#if IVY_SDK_WX
    public class WeChatiOSPayment : WeChatPaymentInterface
    {
        void WeChatPaymentInterface.RequestPayment(string payId, string desc, int amount, string payload)
        {
            var pConfig = PaymentManager.GetPayConfig(payId);
            if (pConfig == null)
            {
                Utils.IvyLog.d($"pConfig is null");
                return;
            }

            //不同的小游戏要查询对应服务器的路由，来分散访问压力
            Utils.HttpUtil.I.HttpPostWithoutTimeout("https://wxapi.winwingplane.cn:8081/queryip", "", (ret, ip) =>
            {
                Utils.IvyLog.d($"resp code::{ret}; ip:{ip}");
                if (ret != Utils.SdkErrorCode.Ok) return;

                IvySDKConfig.PlatformConfig config = MiniGameProxy.GetPlatformConfig();

                var outTradeNo = PaymentManager.GenerateOrderNo(config.appId, config.channel);


                //这部分是需要返回给客户端的参数
                QueryPaymentResp resp = new QueryPaymentResp();
                resp.code = 1;
                int.TryParse(payId, out var id);
                resp.data = new PayData()
                {
                    orderId = outTradeNo,
                    billId = id,
                    payload = payload
                };
                var respContent = JsonUtility.ToJson(resp);

                WeChatWASM.WX.OpenCustomerServiceConversation(new WeChatWASM.OpenCustomerServiceConversationOption
                {
                    showMessageCard = true,
                    sendMessageTitle = config.appName,
                    sendMessageImg = pConfig.imageUrl,
                    sessionFrom = JsonUtility.ToJson(new ServiceMessage
                    {
                        client_ip = ip,
                        platform = "ios",
                        tradeNo = outTradeNo,
                        payId = payId
                    }),
                    complete = ret =>
                    {
                        Utils.IvyLog.d($"complete:{ret.errMsg}");
                        Cloud.CloudStorage.CallFunctionToSelf("pay_queryOrderLocal",
                            new Dictionary<string, object>
                            {
                                ["outTradeNo"] = outTradeNo,
                                ["platform"] = "ios",
                            },
                            (action, content) =>
                            {
                                PaymentManager.ParsePayResponse(action, respContent);
                                Tracker.EventTracker.ReportPayment(amount, "CNY", outTradeNo, payId, "wechat_ios");
                            },
                            msg =>
                            {
                                Utils.OperatorDispatcher.I.StartCoroutine(QueryWechatPayment(payId, outTradeNo, 1, respContent));
                            });
                    }
                });
            });

        }

        private static IEnumerator QueryWechatPayment(string payId, string tradeNo, int count, string respContent)
        {
            if (count > 6)
            {
                PaymentManager.failPayCall?.Invoke(payId);
                yield break;
            }
            yield return new WaitForSeconds(4f); ;
            count += 1;
            Cloud.CloudStorage.CallFunctionToSelf("pay_queryOrderLocal",
                new Dictionary<string, object>
                {
                    ["outTradeNo"] = tradeNo,
                    ["platform"] = "ios",
                },
                (action, content) =>
                {
                    PaymentManager.ParsePayResponse(action, respContent);
                },
                msg =>
                {
                    Utils.OperatorDispatcher.I.StartCoroutine(QueryWechatPayment(payId, tradeNo, count, respContent));
                });
        }
    }
#endif
}
