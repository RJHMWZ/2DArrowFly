using IvySdk.MiniGame;
using System.Collections.Generic;
using IvySdk.Utils;
using System.Collections;
using UnityEngine;

namespace IvySdk.Tracker
{
    public class IvyTracker : TrackInterface
    {

        private readonly string url = "https://ev.ivymobile.com/track/event";

        private Dictionary<string, object> event_params = new Dictionary<string, object>();

        public void Init()
        {
         
        }

        private void InitEventParams()
        {
            event_params.Clear();
            if (event_params.Count < 0)
            {
                event_params.Add("role_id", MiniGameProxy.GetRoleId());

                var platform_config = MiniGameProxy.GetPlatformConfig();
                event_params.Add("app_version", platform_config.versionName);
                event_params.Add("app_version_code", platform_config.versionCode);
                event_params.Add("package_name", platform_config.packageName);


                var system_info = MiniGameProxy.GetSystemInfo();
                event_params.Add("sdk_model", system_info["model"]);
                event_params.Add("platform", system_info["platform"]);
                event_params.Add("language_code", system_info["language"]);

            }

            var user_info = MiniGameProxy.Me();
            if (user_info != null) { 
                if (!string.IsNullOrEmpty(user_info.open_id))
                {
                    event_params.Add("open_id", user_info.open_id);
                }
                if (!string.IsNullOrEmpty(user_info.union_id))
                {
                    event_params.Add("union_id", user_info.union_id);
                }
            }
           
            event_params.Add("st", Time.time);

        }

        public void report(string eventKey, Dictionary<string, object> paramMap)
        {
            InitEventParams();



        }


    
    }
}