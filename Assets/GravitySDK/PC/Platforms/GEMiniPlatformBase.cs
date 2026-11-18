using UnityEngine;
using System.Collections.Generic;
using System;

namespace GravityEngine.Platform
{
    public class GEMiniPlatformBase
    {
        #region 通用
        public virtual void SetDeviceInfo()
        {
        }

        public virtual void OnGravityStartEnd()
        {
        }

        public virtual string platformStr()
        {
            return null;
        }

        public virtual void GetLaunchOptionsSync(Action<Dictionary<string, string>, string> callBack)
        {

        }
        public virtual void getDeviceInfo(Action<Dictionary<string,string>> callBack)
        {

        }

        public virtual void addShowAutoTrack(Func<string,Dictionary<string, object>> propertiesCallback)
        {

        }

        public virtual void addHideAutoTrack(Func<string, Dictionary<string, object>> propertiesCallback)
        {

        }

        public virtual void addFavoriteAutoTrack(Func<string, Dictionary<string, object>> propertiesCallback)
        {

        }

        public virtual void addShareAutoTrack(Func<string, Dictionary<string, object>> propertiesCallback)
        {

        }
        #endregion
        #region 数据管理
        public virtual void SaveData(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            if (value == null)
            {
                return;
            }
            if (value.GetType() == typeof(int))
            {
                UnityEngine.PlayerPrefs.SetInt(key, (int)value);
            }
            else if (value.GetType() == typeof(float))
            {
                UnityEngine.PlayerPrefs.SetFloat(key, (float)value);
            }
            else if (value.GetType() == typeof(string))
            {
                UnityEngine.PlayerPrefs.SetString(key, (string)value);
            }
            UnityEngine.PlayerPrefs.Save();
        }

        public virtual object GetData(string key, Type type)
        {
            if (!string.IsNullOrEmpty(key) && UnityEngine.PlayerPrefs.HasKey(key))
            {
                if (type == typeof(int))
                {
                    return UnityEngine.PlayerPrefs.GetInt(key);
                }
                else if (type == typeof(float))
                {
                    return UnityEngine.PlayerPrefs.GetFloat(key);
                }
                else if (type == typeof(string))
                {
                    return UnityEngine.PlayerPrefs.GetString(key);
                }
                UnityEngine.PlayerPrefs.Save();
            }
            return null;
        }
        public virtual void DeleteData(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            if (UnityEngine.PlayerPrefs.HasKey(key))
            {
                UnityEngine.PlayerPrefs.DeleteKey(key);
            }
        }
        public virtual bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            return UnityEngine.PlayerPrefs.HasKey(key);
        }
        #endregion

        #region 腾讯SDK专用

        public virtual void SetOpenId(string openId)
        {
        }

        public virtual void TrackMPShare(string target, Dictionary<string, object> properties = null)
        {
        }

        public virtual object TrackMPAddFavorites(string wishType = "default")
        {
            return null;
        }

        public virtual object TrackMPCreateRole(string roleName)
        {
            return null;
        }

        public virtual object TrackMPTutorialFinish()
        {
            return null;
        }

        public virtual void TrackMPUpdateLevel(int level, int power)
        {
        }

        public virtual void TrackMPViewMallContent()
        {

        }

        public virtual void TrackMPViewActivityContent()
        {

        }


        public virtual object onPurchase(int amount)
        {
            return null;
        }

        public virtual object onRegister()
        {
            return null;
        }

        public virtual void onReActive(int backFlowDay)
        {

        }
        #endregion
    }
}
