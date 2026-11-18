using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IvySdk.MiniGame
{
#if IVY_SDK_WX
    internal interface WeChatPaymentInterface
    {
        internal void RequestPayment(string payId, string desc, int amount, string payload);
    }

    public class WeChatPayment
    {
        private readonly WeChatPaymentInterface _interface;
        public WeChatPayment()
        {
            bool is_ios = MiniGameProxy.IsIOSSystem();
            if (is_ios)
            {
                _interface = new WeChatiOSPayment();
            }
            else
            {
                _interface = new WeChatAndroidPayment();
            }
        }

        public void RequestPayment(string payId, string desc, int amount, string payload)
        {
            _interface?.RequestPayment(payId, desc, amount, payload);
        }
    }
#endif
}
