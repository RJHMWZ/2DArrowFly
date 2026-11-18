using IvySdk.MiniGame;
using IvySdk.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace IvySdk.Remote
{
    [Serializable]
    public class Response
    {
        public int code { get; set; }
        public string msg { get; set; }

        // ∂ØÃ¨ ˝æ›
        public JObject data { get; set; }

        public long timestamp { get; set; }
    }

    public class IvyRemoteData
    {
        private static IvyRemoteData _instance = null;

        private readonly string url = "https://gameconfig.ivymobile.com/api/external/v1/distribution/remote_config/edata";
        private readonly string local_cache_key = "remote_config";

        private static JObject remote_config = null;

        private static readonly object _lock = new object();

        public static IvyRemoteData Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IvyRemoteData();
                }
                return _instance;
            }
        }

        private IvyRemoteData() { }

        public void InitRemoteDataWithCache()
        {
            try
            {
                string cached = null;
#if IVY_SDK_DY || IVY_SDK_TT
                cached = MiniGameProxy.LoadCachedData(local_cache_key);
#else
                cached = MiniGameProxy.GetLocalStringSync(local_cache_key, null);
#endif

                if (!string.IsNullOrEmpty(cached))
                {
                    remote_config = JObject.Parse(cached);
                    IvyLog.d("‘∂≥Ã≈‰÷√£∫ π”√±æµÿª∫¥Ê");
                }
            }
            catch (Exception e)
            {
                IvyLog.d($"ª∫¥ÊΩ‚Œˆ ß∞‹: {e.Message}");
            }

            LoadRemoteData();
        }

        private void LoadRemoteData()
        {
            Dictionary<string, object> pa = new Dictionary<string, object>();

            IvySDKConfig.PlatformConfig config = MiniGameProxy.GetPlatformConfig();

            pa["app_id"] = config.appId;
            pa["user_id"] = MiniGameProxy.GetRoleId();
            pa["app_version"] = int.Parse(config.versionCode);
            pa["af_id"] = MiniGameProxy.GetRoleId();
            pa["bundleId"] = config.packageName;
            pa["debug"] = 1;

            HttpUtil.I.HttpPost(url, pa, (code, resp) =>
            {
                if (code == 200 && !string.IsNullOrEmpty(resp))
                {
                    try
                    {
                        Response result = JsonConvert.DeserializeObject<Response>(resp);
                        var data = result.data;

                        if (data != null)
                        {
                            lock (_lock)
                            {
                                remote_config = data;
                            }

                            // –¥ª∫¥Ê
                            JObject jsonObject = JObject.Parse(remote_config.ToString());
                            string cleanJson = JsonConvert.SerializeObject(jsonObject, Formatting.None);
#if IVY_SDK_DY || IVY_SDK_TT
                            MiniGameProxy.CacheData(local_cache_key, cleanJson);
#else
                            MiniGameProxy.SetLocalString(local_cache_key, cleanJson);
#endif

                        }
                    }
                    catch (Exception ex)
                    {
                        IvyLog.d($"‘∂≥Ã≈‰÷√Ω‚Œˆ ß∞‹: {ex.Message}");
                    }
                }
                else
                {
                    IvyLog.d($"‘∂≥Ã≈‰÷√«Î«Û ß∞‹: {code}");
                }
            });
        }

        
        public T Get<T>(string path, T defaultValue = default)
        {
            if (remote_config == null)
                return defaultValue;

            try
            {
                var token = remote_config.SelectToken(path);

                if (token == null)
                    return defaultValue;

                if (token is T value)
                    return value;

                // ª˘¥°÷µ(string/int/bool/long...)
                if (token is JValue)
                    return token.Value<T>();

                if (typeof(T) == typeof(string))
                {
                    // JValue: ∑µªÿ÷µ
                    if (token is JValue)
                        return (T)(object)token.Value<string>();

                    // JObject/JArray: ∑µªÿjson◊÷∑˚¥Æ
                    return (T)(object)token.ToString(Formatting.None);
                }

                return token.ToObject<T>();
            }
            catch (Exception e)
            {
                IvyLog.d($"Get<{typeof(T).Name}>({path}) ß∞‹: {e}");
                return defaultValue;
            }
        }


        public string GetString(string path, string defaultValue = "")
        {
            return Get<string>(path, defaultValue);
        }

        public int GetInt(string path, int defaultValue = 0)
        {
            return Get<int>(path, defaultValue);
        }

        public long GetLong(string path, long defaultValue = 0)
        {
            return Get<long>(path, defaultValue);
        }

        public double GetDouble(string path, double defaultValue = 0)
        {
            return Get<double>(path, defaultValue);
        }

        public bool GetBool(string path, bool defaultValue = false)
        {
            return Get<bool>(path, defaultValue);
        }

        public bool HasKey(string path)
        {
            if (remote_config == null)
                return false;

            return remote_config.SelectToken(path) != null;
        }
    }
}