using UnityEngine;

namespace HueRelay
{
    public enum HuePowerUpKind { Hint, Eraser, ExtraMoves }

    [CreateAssetMenu(menuName = "Hue Relay/Power Up Config", fileName = "PowerUps")]
    public sealed class HuePowerUpConfig : ScriptableObject
    {
        [Header("First launch gift (once per local save)")]
        [Min(0)] public int initialHints = 3;
        [Min(0)] public int initialErasers = 3;
        [Min(0)] public int initialExtraMoves = 3;
        [Header("Reward for one completed ad")]
        [Min(1)] public int rewardedAmount = 2;
        [Header("Editor preview only; never used in a player build")]
        public bool simulateAdsInEditor = true;
    }
}
