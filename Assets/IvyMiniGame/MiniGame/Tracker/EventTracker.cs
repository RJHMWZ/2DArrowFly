using System;
using System.Collections.Generic;
using UnityEngine;


namespace IvySdk.Tracker
{
    internal interface TrackInterface
    {
        void Init();
        void report(string eventKey, Dictionary<string, object> paramMap);
    }

    public static class EventTracker
    {

        private static Action<string, Dictionary<string, object>> tracker;
        private static Action trackerInit;

        private static Dictionary<string, object> user_property = new Dictionary<string, object>();

#if IVY_SDK_WX || IVY_SDK_DY || IVY_SDK_TT
        private static Attribute.GravityTracker gravity_tracker;
#endif

        [RuntimeInitializeOnLoadMethod]
        static void OnRuntimeInit()
        {
#if IVY_SDK_DY
            var byteInstance = new ByteTracker();
            tracker += byteInstance.report;
            trackerInit += byteInstance.Init;
#elif IVY_SDK_WX
            var wechatInstance = new WeChatTrack();
            tracker += wechatInstance.report;
            trackerInit += wechatInstance.Init;

#elif  IVY_SDK_TT
            var tiktokInstance = new TiktokTracker();
            tracker += tiktokInstance.report;
            trackerInit += tiktokInstance.Init;
#endif

            var ivy_tracker = new IvyTracker();
            tracker += ivy_tracker.report;
            trackerInit += ivy_tracker.Init;

        }

        internal static void InitTrack()
        {
            trackerInit?.Invoke();
        }

        public static void AddUserProperty(string key, object value)
        {
            try
            {
                user_property.TryAdd(key, value);
            }catch{ }
        }

        public static void RemoveUserProperty(string key)
        {
            try
            {
                user_property.Remove(key);
            }
            catch { }
        }

        public static void TrackEvent(string eventKey, Dictionary<string, object> paramMap)
        {
            if (paramMap == null)
            {
                paramMap = new Dictionary<string, object>();
            }
            var merged = new Dictionary<string, object>(paramMap);

            foreach (var kv in user_property)
            {
                merged[kv.Key] = kv.Value;
            }
            tracker?.Invoke(eventKey, merged);
#if IVY_SDK_WX || IVY_SDK_DY || IVY_SDK_TT
            gravity_tracker?.Report(eventKey, merged);
#endif
        }

        public static void TrackEvent(string eventKey, string paramContent)
        {
            var ss = paramContent.Split(",");
            var dict = new Dictionary<string, object>();
            if (ss != null && ss.Length > 0)
            {
                for (int i = 0; i < ss.Length - 1; i += 2)
                {
                    var key = ss[i];
                    var value = ss[i + 1];
                    if (!string.IsNullOrEmpty(key))
                    {
                        dict[key] = value;
                    }
                }
            }
            TrackEvent(eventKey, dict);
        }

        public static void InitGravityWithOpenId(string openId, IvySDKConfig.PlatformConfig platformConfig)
        {
#if IVY_SDK_WX || IVY_SDK_DY || IVY_SDK_TT
            if (gravity_tracker == null)
            {
                gravity_tracker = new Attribute.GravityTracker();
            }
            gravity_tracker.InitWithOpenId(openId, platformConfig);
#endif
        }

        public static void ReportRegister()
        {
#if IVY_SDK_WX || IVY_SDK_DY || IVY_SDK_TT
            gravity_tracker?.ReportRegister();
#endif
        }

        public static void ReportPayment(int payAmount, string payType, string orderId, string payReason, string payMethod)
        {
            var obj = new Dictionary<string, object>();
            obj.TryAdd("amount", payAmount);
            obj.TryAdd("payType", payType);
            obj.TryAdd("orderId", orderId);
            obj.TryAdd("sku", payReason);
            obj.TryAdd("payMethod", payMethod);

            TrackEvent("sdk_payment", obj);

#if IVY_SDK_WX || IVY_SDK_DY || IVY_SDK_TT
            gravity_tracker?.ReportPayment(payAmount, payType, orderId, payReason, payMethod);
#endif
#if IVY_SDK_WX
            Event.ADQDataTrack.Instance.TrackPurchase(payAmount);
            Event.OceanDataTrack.Instance.Event(Event.OceanEventType.ActivePay, payAmount);
#endif
        }



    }
}
