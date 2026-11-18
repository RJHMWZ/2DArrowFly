#if IVY_SDK_TT
using System.Collections.Generic;
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;

namespace IvySdk.Tracker
{

    public class TiktokTracker : TrackInterface
    {
        public void Init()
        {
        }

        public void report(string eventKey, Dictionary<string, object> paramMap)
        {
            var param = new ReportEventParam();
            param.eventName = eventKey;
            if (paramMap != null)
            {
                paramMap.Add("role_id", MiniGame.MiniGameProxy.GetRoleId());
                string jsonStr = JsonMapper.ToJson(paramMap);
                JsonData jsonData = JsonMapper.ToObject(jsonStr);
                param.@params = jsonData;
            }

            TT.ReportEvent(param);

        }
    }

}
#endif