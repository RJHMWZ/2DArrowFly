using IvySdk.MiniGame;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IvySdk.Tracker
{
#if IVY_SDK_WX
    public class WeChatTrack : TrackInterface
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
            WeChatWASM.WX.ReportEvent(eventKey, tMap);
        }
    }
#endif
}


