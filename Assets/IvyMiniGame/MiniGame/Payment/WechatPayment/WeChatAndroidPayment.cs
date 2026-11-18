using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IvySdk.MiniGame
{
#if IVY_SDK_WX
    public class WeChatAndroidPayment : WeChatPaymentInterface
    {
        void WeChatPaymentInterface.RequestPayment(string payId, string desc, int amount, string payload)
        {
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

            int isSandBox = config.debug ? 1 : 0;
            WeChatWASM.WX.RequestMidasPayment(new WeChatWASM.RequestMidasPaymentOption
            {
                mode = "game",
                env = isSandBox,
                currencyType = "CNY",
                offerId = config.miMasterAppId,
                buyQuantity = amount,
                outTradeNo = outTradeNo,
                success = resp =>
                {
                    Utils.IvyLog.d($"[pay_queryOrderLocal] start -- {outTradeNo}");
                    //支付成功回调，向服务器查询当次订单的付费情况
                    Cloud.CloudStorage.CallFunctionToSelf("pay_queryOrderLocal",
                        new Dictionary<string, object>()
                        {
                            ["outTradeNo"] = outTradeNo,
                            ["is_dev"] = isSandBox, 
                            ["platform"] = "android",
                        },
                        (action, content) =>
                        {
                            Utils.IvyLog.d($" ----[RequestWeChatPayment] {payId}  {action} --- {content}");
                            PaymentManager.ParsePayResponse(action, respContent);
                            Tracker.EventTracker.ReportPayment(amount, currencyType, outTradeNo, payId, "wechat_android");
                        },
                        msg =>
                        {
                            PaymentManager.failPayCall?.Invoke(payId);
                        });
                },
                fail = resp =>
                {
                    Utils.IvyLog.e($"支付失败 -- {resp.errMsg}");
                    PaymentManager.failPayCall?.Invoke(payId);
                },
            });
        }
    }
#endif
}