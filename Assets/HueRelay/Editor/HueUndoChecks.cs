using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    public static class HueUndoChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int checks;
        private static FieldInfo Field(string name) => typeof(HueGame).GetField(name, Private);
        private static void Call(HueGame game, string name) => typeof(HueGame).GetMethod(name, Private).Invoke(game, null);
        private static void Require(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        private static void Fixture(HueGame game, HuePowerUpInventory inventory)
        {
            game.StartLevel(3);
            typeof(HueGame).GetProperty("P").SetValue(game, HueEngine.Prep(new LevelData(6, 7, new[] {
                new ArrowData("B", "r", new[] { new Cell(3, 3), new Cell(3, 2), new Cell(4, 2) }),
                new ArrowData("B", "r", new[] { new Cell(0, 2), new Cell(1, 2) }),
                new ArrowData("C", "u", new[] { new Cell(2, 6), new Cell(2, 5) }),
                new ArrowData("A", "u", new[] { new Cell(5, 6), new Cell(5, 5) })
            })));
            Field("baseMoveLimit").SetValue(game, 20);
            game.ResetGame(); Field("built").SetValue(game, -100f);
            inventory.DebugSetCounts(10, 4, 4);
        }

        // Drive the real chain, flight, erase and return implementations on one
        // deterministic clock; do not run result/ads while setting up undo cases.
        private static void Advance(HueGame game, float seconds)
        {
            int frames = Mathf.CeilToInt(seconds / .01f);
            for (int frame = 0; frame < frames; frame++)
            {
                float now = (float)Field("now").GetValue(game) + .01f;
                Field("now").SetValue(game, now); Field("dt").SetValue(game, .01f);
                float next = (float)Field("nextChain").GetValue(game);
                if (next >= 0 && now >= next)
                {
                    Field("nextChain").SetValue(game, -1f);
                    var queue = (Queue<int>)Field("q").GetValue(game);
                    if (queue.Count > 0) typeof(HueGame).GetMethod("Step", Private).Invoke(game, new object[] { queue.Dequeue() });
                }
                Call(game, "UpdateErase"); Call(game, "UpdateUndo"); Call(game, "UpdateEffects");
            }
            Call(game, "Render");
        }

        private static void Settle(HueGame game)
        {
            for (int i = 0; i < 1200 && !game.Idle; i++) Advance(game, .01f);
            Require(game.Idle, "Undo fixture did not reach a stable board.");
        }

        public static void Run()
        {
            checks = 0;
            var game = UnityEngine.Object.FindObjectOfType<HueGame>();
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            object original = Field("inventory").GetValue(game);
            const string key = "cc_undo_checks";
            bool hadSave = PlayerPrefs.HasKey(key); string saved = PlayerPrefs.GetString(key, "");
            var inventory = new HuePowerUpInventory(game.PowerUpSettings, key);
            Field("inventory").SetValue(game, inventory);
            try
            {
                Fixture(game, inventory);
                game.Hint();
                Require(!game.CanUndo && game.UndosRemaining == 10 && game.Taps == 0, "Fresh board spent an undo without history.");
                var undoIcon = view.hintButton.transform.Find("Icon").GetComponent<Image>();
                Require(view.hintButton.transform.Find("Label").GetComponent<Text>().text == "Undo"
                    && undoIcon.enabled && undoIcon.sprite && undoIcon.transform.childCount == 0,
                    "Undo must use the authored PNG Image without runtime geometry.");
                Require(UnityEditor.AssetDatabase.GetAssetPath(undoIcon.sprite) == "Assets/HueRelay/Art/PowerUps/Undo.png"
                    && undoIcon.color == Color.white && undoIcon.sprite.texture.width == 128,
                    "Undo sprite must use the compact colored PNG without recoloring.");
                var cells = game.P.arrs[0].cells;
                game.TapArrow(0); Settle(game);
                Require(game.Mask == 12 && game.Score == 30 && game.Taps == 1 && game.CanUndo,
                    "Launch/automatic chain did not create an undoable action.");
                game.Hint();
                Require(game.IsUndoing && game.Mask == 12 && game.Score == 0 && game.Taps == 0 && game.UndosRemaining == 9,
                    "Undo must spend once, refund one tap, remove chain points and defer arrow restoration.");
                int limit = game.Limit;
                game.TapArrow(2); game.Undo(); game.AddMoves(); game.ToggleErase();
                Require(game.Taps == 0 && game.UndosRemaining == 9 && game.Limit == limit && !game.IsChoosingErase,
                    "Return animation accepted another action or spent duplicate inventory.");
                Advance(game, .51f);
                Require((game.Mask & 1) != 0 && (game.Mask & 2) == 0 && game.IsUndoing,
                    "Arrows did not land in launch order.");
                Advance(game, .1f);
                Require(game.Mask == game.P.Full && game.Idle && !game.CanUndo && ReferenceEquals(cells, game.P.arrs[0].cells),
                    "Returning arrows failed to restore their exact original paths.");
                game.TapArrow(0); Settle(game); game.Undo(); Settle(game);
                Require(game.Mask == game.P.Full && game.Score == 0 && game.Taps == 0 && game.UndosRemaining == 8,
                    "A returned arrow could not be replayed and undone independently.");

                Fixture(game, inventory);
                game.TapArrow(0); Advance(game, .05f); game.TapArrow(2); Settle(game);
                Require(game.Mask == 8 && game.Taps == 2 && game.Score == 60, "Overlapping-tap fixture changed gameplay behavior.");
                game.Undo(); Settle(game);
                Require(game.Mask == 12 && game.Taps == 1 && game.Score == 40,
                    "Undo reverted an earlier overlapping chain or retained the last tap's points.");
                game.Undo(); Settle(game);
                Require(game.Mask == game.P.Full && game.Taps == 0 && game.Score == 0,
                    "Second undo did not restore the earlier chain.");

                Fixture(game, inventory);
                game.TapArrow(1); Settle(game);
                Require(game.Taps == 1 && game.Mask == game.P.Full, "Blocked-tap fixture did not consume exactly one move.");
                game.Undo();
                Require(game.Taps == 0 && game.Mask == game.P.Full && !game.IsUndoing && game.UndosRemaining == 9,
                    "Undo of a blocked tap must refund the step without inventing a flight.");

                Fixture(game, inventory);
                game.TapArrow(0); Settle(game); game.AddMoves();
                game.ToggleErase(); game.TapArrow(3); Settle(game);
                game.Undo(); Settle(game);
                Require(game.Mask == 7 && game.Limit == 23 && game.Taps == 0 && game.Score == 0 && game.ErasersRemaining == 3,
                    "Undo discarded an independent eraser use or added moves.");

                Fixture(game, inventory);
                game.TapArrow(0); Settle(game); game.Undo(); game.Retry(); Advance(game, 2);
                Require(game.Mask == game.P.Full && game.Taps == 0 && !game.IsUndoing && !game.CanUndo,
                    "Retry retained a returning arrow, old history or stale restore.");
                game.StartLevel(4); Advance(game, 1);
                Require(game.Mask == game.P.Full && !game.IsUndoing && !game.CanUndo, "Next level retained old undo state.");

                Fixture(game, inventory); inventory.DebugSetCounts(0, 4, 4); game.Hint();
                var panel = view.canvas.GetComponentInChildren<HuePowerUpRefillPanel>(true);
                Require(game.IsPowerUpRefillOpen && panel.title.text == "Out of Undos" && panel.amountLabel.text == "+1"
                    && panel.message.text == "Watch a video to get 1 undo." && panel.itemIcon.enabled
                    && panel.itemIcon.sprite == undoIcon.sprite && panel.itemIcon.transform.childCount == 0,
                    "Undo refill must share the authored PNG and show the correct reward.");
                game.ClosePowerUpRefill();
                Directory.CreateDirectory("Logs/Undo");
                File.WriteAllText("Logs/Undo/checks.txt", "PASS: " + checks + " checks: exact path return in launch order; whole-chain undo; one-step/score/inventory accounting; overlapping chains; repeated undo/replay; blocked taps; independent erase/move tools preserved; input locking; retry/level cleanup; existing save inventory and rewarded refill presentation.\n");
            }
            finally
            {
                game.ClosePowerUpRefill(); game.StartLevel(3); Field("inventory").SetValue(game, original);
                if (hadSave) PlayerPrefs.SetString(key, saved); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }
    }
}
