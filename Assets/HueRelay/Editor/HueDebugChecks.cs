using System;
using UnityEditor;
using UnityEngine;

namespace HueRelay.Editor
{
    public static class HueDebugChecks
    {
        private static void Require(bool value, string message)
        {
            if (!value) throw new Exception(message);
        }

        [MenuItem("Tools/Hue Relay/Debug/Check storage and simulation commands")]
        public static void Run()
        {
            string inventoryKey = "cc_debug_inventory_" + Guid.NewGuid().ToString("N");
            string progressKey = "cc_debug_progress_" + Guid.NewGuid().ToString("N");
            var config = ScriptableObject.CreateInstance<HuePowerUpConfig>();
            try
            {
                config.initialHints = config.initialErasers = config.initialExtraMoves = 1;
                var inventory = new HuePowerUpInventory(config, inventoryKey);
                inventory.TryClaimRetentionReward(HueRetentionKind.Profile);
                inventory.DebugSetCounts(1, 1, 1);
                var reloaded = new HuePowerUpInventory(config, inventoryKey);
                Require(reloaded.Count(HuePowerUpKind.Hint) == 1 && reloaded.Count(HuePowerUpKind.Eraser) == 1
                    && reloaded.Count(HuePowerUpKind.ExtraMoves) == 1, "Debug balances did not persist.");
                Require(reloaded.HasClaimedRetentionReward(HueRetentionKind.Profile), "Editing balances reset reward receipts.");
                reloaded.DebugSetCounts(-1, 0, int.MaxValue);
                inventory = new HuePowerUpInventory(config, inventoryKey);
                Require(inventory.Count(HuePowerUpKind.Hint) == 0 && inventory.Count(HuePowerUpKind.Eraser) == 0
                    && inventory.Count(HuePowerUpKind.ExtraMoves) == int.MaxValue, "Invalid debug balance clamp.");
                Require(!inventory.TryClaimRetentionReward(HueRetentionKind.Profile), "Debug edit repeated retention reward.");
                var progress = new HueCampaignProgress(progressKey, 100, 0);
                progress.DebugSetProgress(100);
                Require(new HueCampaignProgress(progressKey, 100, 0).Completed, "Debug unlock did not persist.");
                progress.DebugSetProgress(0);
                Require(!progress.Completed && new HueCampaignProgress(progressKey, 100, 0).NextLevelIndex == 0,
                    "Debug progress reset did not persist.");
                for (int mode = 0; mode < 5; mode++)
                {
                    using (var backend = new HueDebugWindow.DebugBackend(mode))
                    {
                        foreach (HueAdFormat format in Enum.GetValues(typeof(HueAdFormat)))
                        {
                            Require(!backend.IsReady(format), "Simulation started ready without loading.");
                            backend.Preload(format);
                            Require(backend.IsReady(format) == (mode != 3), "No-fill simulation readiness is incorrect.");
                            int callbacks = 0;
                            HueAdOutcome? result = null;
                            backend.Show(format, value => { callbacks++; result = value; });
                            Require(callbacks == (mode == 4 ? 0 : 1), "Incorrect simulated callback count.");
                            HueAdOutcome? expected = mode == 0 ? HueAdOutcome.Completed : mode == 1 ? HueAdOutcome.Cancelled
                                : mode == 2 ? HueAdOutcome.Failed : mode == 3 ? HueAdOutcome.Unavailable : (HueAdOutcome?)null;
                            Require(result == expected && !backend.IsReady(format), "Incorrect simulated result or instance reuse.");
                        }
                    }
                }
                Debug.Log("PASS: debug balances persist, receipt retained, negative clamp, progress reset/unlock, five simulation modes for both ad formats. Player saves unchanged.");
            }
            finally
            {
                PlayerPrefs.DeleteKey(inventoryKey); PlayerPrefs.DeleteKey(progressKey); PlayerPrefs.Save();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
