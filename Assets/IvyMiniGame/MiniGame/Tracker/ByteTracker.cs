#if IVY_SDK_DY
using IvySdk.MiniGame;
using System.Collections.Generic;
using System.Linq;
using TTSDK;

namespace IvySdk.Tracker
{

    public class ByteTracker : TrackInterface
    {
        public void Init()
        {
        }

        public void report(string eventKey, Dictionary<string, object> paramMap)
        {
            paramMap.Add("role_id", MiniGameProxy.GetRoleId());
            //todo 添加默认信息
            var tMap = paramMap.ToDictionary(
                    x => x.Key,
                    x => x.Value.ToString());

            TT.ReportAnalytics(eventKey, tMap);
        }
    }

}
#endif