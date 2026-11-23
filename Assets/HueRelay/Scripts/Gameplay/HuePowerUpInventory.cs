using System;
using UnityEngine;

namespace HueRelay
{
    /// <summary>One persistent inventory, independent of level and campaign versions.</summary>
    public sealed class HuePowerUpInventory
    {
        public const string SaveKey = "cc_powerups_v1";
        [Serializable] private sealed class SaveData
        {
            public int version = 1;
            public int hints, erasers, extraMoves;
            public bool profileRewardClaimed, shortcutRewardClaimed;
        }
        private readonly string key;
        private SaveData data;

        public HuePowerUpInventory(HuePowerUpConfig config, string storageKey = SaveKey)
        {
            key = storageKey;
            if (!PlayerPrefs.HasKey(key))
            {
                data = new SaveData { hints = Mathf.Max(0, config.initialHints),
                    erasers = Mathf.Max(0, config.initialErasers), extraMoves = Mathf.Max(0, config.initialExtraMoves) };
                Save();
                return;
            }
            try { data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(key)); }
            catch (Exception) { data = null; }
            if (data == null || data.version != 1)
            {
                Debug.LogWarning("Power-up save is invalid. Restoring an empty inventory without repeating the first-launch gift.");
                data = new SaveData(); Save();
            }
            data.hints = Mathf.Max(0, data.hints);
            data.erasers = Mathf.Max(0, data.erasers);
            data.extraMoves = Mathf.Max(0, data.extraMoves);
        }

        public int Count(HuePowerUpKind kind)
        {
            switch (kind) { case HuePowerUpKind.Hint: return data.hints;
                case HuePowerUpKind.Eraser: return data.erasers;
                case HuePowerUpKind.ExtraMoves: return data.extraMoves;
                default: throw new ArgumentOutOfRangeException(nameof(kind)); }
        }
        public bool TrySpend(HuePowerUpKind kind)
        {
            int count = Count(kind);
            if (count <= 0) return false;
            Set(kind, count - 1); Save(); return true;
        }
        public void Add(HuePowerUpKind kind, int amount)
        {
            if (amount <= 0) return;
            int count = Count(kind);
            Set(kind, amount > int.MaxValue - count ? int.MaxValue : count + amount);
            Save();
        }
        public bool HasClaimedRetentionReward(HueRetentionKind kind) => kind == HueRetentionKind.Profile
            ? data.profileRewardClaimed : data.shortcutRewardClaimed;

        public bool TryClaimRetentionReward(HueRetentionKind kind)
        {
            if (HasClaimedRetentionReward(kind)) return false;
            int amount = HueRetention.RewardAmount(kind);
            data.hints = data.hints > int.MaxValue - amount ? int.MaxValue : data.hints + amount;
            if (kind == HueRetentionKind.Profile) data.profileRewardClaimed = true;
            else data.shortcutRewardClaimed = true;
            // Receipt and balance are persisted in the same JSON snapshot. A duplicate
            // callback, reopening the panel or restarting cannot repeat the grant.
            Save();
            return true;
        }
        private void Set(HuePowerUpKind kind, int count)
        {
            switch (kind) { case HuePowerUpKind.Hint: data.hints = count; break;
                case HuePowerUpKind.Eraser: data.erasers = count; break;
                case HuePowerUpKind.ExtraMoves: data.extraMoves = count; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind)); }
        }
        private void Save() { PlayerPrefs.SetString(key, JsonUtility.ToJson(data)); PlayerPrefs.Save(); }
    }
}
