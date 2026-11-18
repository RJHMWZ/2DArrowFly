using UnityEngine;

namespace IvySdk.Utils
{
    public class IvyLog
    {
        public static void d(string msg)
        {
            
            Debug.Log(msg);
        }

        public static void w(string msg)
        {
            Debug.LogWarning(msg);
        }

        public static void e(string msg) { Debug.LogError(msg); }


    }
}
