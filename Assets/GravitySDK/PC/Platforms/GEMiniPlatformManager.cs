using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GravityEngine.Platform
{
    public class GEMiniPlatformManager : MonoBehaviour
    {
        public static bool CanTrackMiniGameAdShowEvent()
        {
            return !GEMiniPlatformByteDance.activity() && !GEMiniPlatformKuaiShou.activity() && !GEMiniPlatformWechat.IsBilibili();
        }
        public static string GetTrackMiniGameAdShowType()
        {
            if (GEMiniPlatformWechat.IsWechat())
            {
                return "wechat";
            }
            return "media";
        }
        public static bool CanFilterAndReportPayEventToTencent()
        {
            return GEMiniPlatformWechat.canTrack();
        }
        public static bool IsAliPayMiniGame()
        {
            return GEMiniPlatformAlipay.activity();
        }
        public static bool IsWechatMiniGame()
        {
            return GEMiniPlatformWechat.activity();
        }

        public static bool NeedGetDeviceInfoBeforeInit()
        {
            return GEMiniPlatformOppo.activity() || GEMiniPlatformHuawei.activity() || GEMiniPlatformVivo.activity();
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        public static bool isMiniGame()
        {
            if (mGEMiniPlatform != null)
            {
                return true;
            }
            return false;
        }


        public static void SetDeviceInfo()
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.SetDeviceInfo();
            }
        }

        public static void OnGravityStartEnd()
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.OnGravityStartEnd();
            }
        }

        public static void GetLaunchOptionsSync(Action<Dictionary<string,string>,string> callBack)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.GetLaunchOptionsSync(callBack);
            }
        }


        public static void addShowAutoTrack(Func<string, Dictionary<string, object>> propertiesCallback)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.addShowAutoTrack(propertiesCallback);
            }
        }

        public static void addHideAutoTrack(Func<string, Dictionary<string, object>> propertiesCallback)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.addHideAutoTrack(propertiesCallback);
            }
        }


        public static void addFavoriteAutoTrack(Func<string, Dictionary<string, object>> propertiesCallback)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.addFavoriteAutoTrack(propertiesCallback);
            }
        }

        public static void addShareAutoTrack(Func<string, Dictionary<string, object>> propertiesCallback)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.addShareAutoTrack(propertiesCallback);
            }
        }

        public static string platformStr()
        {
            if (mGEMiniPlatform != null)
            {
                return mGEMiniPlatform.platformStr();
            }
            return null;
        }


        public static void SetOpenId(string openId)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.SetOpenId(openId);
            }
        }
        public static void TrackMPShare(string target, Dictionary<string, object> properties = null)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.TrackMPShare(target, properties);
            }
        }
        public static object TrackMPAddFavorites(string wishType = "default")
        {
            if (mGEMiniPlatform != null)
            {
                return mGEMiniPlatform.TrackMPAddFavorites(wishType);
            }
            return null;
        }
        public static object TrackMPCreateRole(string roleName)
        {
            if (mGEMiniPlatform != null)
            {
                return mGEMiniPlatform.TrackMPCreateRole(roleName);
            }
            return null;
        }
        public static object TrackMPTutorialFinish()
        {
            if (mGEMiniPlatform != null)
            {
               return mGEMiniPlatform.TrackMPTutorialFinish();
            }
            return null;
        }
        public static void TrackMPUpdateLevel(int level, int power)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.TrackMPUpdateLevel(level, power);
            }
        }
        public static void TrackMPViewMallContent()
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.TrackMPViewMallContent();
            }
        }
        public static void TrackMPViewActivityContent()
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.TrackMPViewActivityContent();
            }
        }

        public static object onRegister()
        {
            if (mGEMiniPlatform != null)
            {
                return mGEMiniPlatform.onRegister();
            }
            return null;
        }
        public static void onReActive(int backFlowDay)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.onReActive(backFlowDay);
            }
        }
        public static object onPurchase(int amount)
        {
            if (mGEMiniPlatform != null)
            {
                return mGEMiniPlatform.onPurchase(amount);
            }
            return null;
        }

        public static void getDeviceInfo(Action<Dictionary<string, string>> callBack)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.getDeviceInfo(callBack);
            }
        }


        public static void SaveData(string key, object value)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.SaveData(key, value);
            }
        }

        public static object GetData(string key, Type type)
        {
            if (mGEMiniPlatform != null)
            {
                return mGEMiniPlatform.GetData(key, type);
            }
            return null;
        }
        public static void DeleteData(string key)
        {
            if (mGEMiniPlatform != null)
            {
                mGEMiniPlatform.DeleteData(key);
            }
        }
        public static bool HasKey(string key)
        {
            if (mGEMiniPlatform != null)
            {
                return mGEMiniPlatform.HasKey(key);
            }
            return false;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //返回有定义宏定义接入的SDK数量
        private static int hasDefineSdkNumber()
        {
            int hasSdk = 0;
            if (GEMiniPlatformAlipay.activity())
            {
                Debug.Log("[GravityEngineSDK] 有接入 Alipay 小游戏");
                hasSdk++;
            }
            if (GEMiniPlatformByteDance.activity())
            {
                Debug.Log("[GravityEngineSDK] 有接入 ByteDance 小游戏");
                hasSdk++;
            }
            if (GEMiniPlatformByteDanceTT.activity())
            {
                Debug.Log("[GravityEngineSDK] 有接入 ByteDanceTT 小游戏");
                hasSdk++;
            }
            if (GEMiniPlatformHuawei.activity())
            {
                Debug.Log("[GravityEngineSDK] 有接入 Huawei 小游戏");
                hasSdk++;
            }
            if (GEMiniPlatformKuaiShou.activity())
            {
                Debug.Log("[GravityEngineSDK] 有接入 KuaiShou 小游戏");
                hasSdk++;
            }
            if (GEMiniPlatformKuaiShouWebGl.activity())
            {
                Debug.Log("[GravityEngineSDK] 有接入 KuaiShouWebGl 小游戏");
                hasSdk++;
            }
            if (GEMiniPlatformOppo.activity())
            {
                Debug.Log("[GravityEngineSDK] 有接入 Oppo 小游戏");
                hasSdk++;
            }
            if (GEMiniPlatformVivo.activity())
            {
                Debug.Log("[GravityEngineSDK] 有接入 Vivo 小游戏");
                hasSdk++;
            }
            if (GEMiniPlatformWechat.activity())
            {
                Debug.Log("[GravityEngineSDK] 有接入 Wechat 小游戏");
                hasSdk++;
            }
            if (GEMiniPlatformXiaomi.activity())
            {
                Debug.Log("[GravityEngineSDK] 有接入 Xiaomi 小游戏");
                hasSdk++;
            }
            if (GEMiniPlatformTapTap.activity())
            {
                Debug.Log("[GravityEngineSDK] 有接入 TapTap 小游戏");
                hasSdk++;
            }
            
            return hasSdk;
        }

        private static GEMiniPlatformBase mGEMiniPlatform = getGEMiniPlatForm();

        private static GEMiniPlatformBase getGEMiniPlatForm()
        {
            int sdkNumber = hasDefineSdkNumber();
            if (sdkNumber > 1)
            {
                throw new ApplicationException("[GravityEngineSDK] 检测到" + sdkNumber + "个小游戏平台接入（应最多为1个），请检查宏设置是否正确！！！！！！！");
            }
            if (GEMiniPlatformAlipay.activity())
            {
                return new GEMiniPlatformAlipay();
            }
            else if (GEMiniPlatformByteDance.activity())
            {
                return new GEMiniPlatformByteDance();
            }
            else if (GEMiniPlatformByteDanceTT.activity())
            {
                return new GEMiniPlatformByteDanceTT();
            }
            else if (GEMiniPlatformHuawei.activity())
            {
                return new GEMiniPlatformHuawei();
            }
            else if (GEMiniPlatformKuaiShou.activity())
            {
                return new GEMiniPlatformKuaiShou();
            }
            else if (GEMiniPlatformKuaiShouWebGl.activity())
            {
                return new GEMiniPlatformKuaiShouWebGl();
            }
            else if (GEMiniPlatformOppo.activity())
            {
                return new GEMiniPlatformOppo();
            }
            else if (GEMiniPlatformVivo.activity())
            {
                return new GEMiniPlatformVivo();
            }
            else if (GEMiniPlatformWechat.activity())
            {
                return new GEMiniPlatformWechat();
            }
            else if (GEMiniPlatformXiaomi.activity())
            {
                return new GEMiniPlatformXiaomi();
            }
            else if (GEMiniPlatformTapTap.activity())
            {
                return new GEMiniPlatformTapTap();
            }
            return null;
        }
    }
}