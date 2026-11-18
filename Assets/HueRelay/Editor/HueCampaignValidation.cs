using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HueRelay.Editor
{
    // Editor-only checks: the shipped game only loads authored data and routes.
    internal static class HueCampaignValidation
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        static LevelCollection Load() => JsonUtility.FromJson<LevelCollection>(
            Resources.Load<TextAsset>("HueRelay/Data/Levels").text);

        public static List<string> Validate()
        {
            var errors = new List<string>();
            try
            {
                var collection = Load();
                Require(collection.campaignId == "hue100_v1" && collection.levels.Length == 100, "Expected 100 campaign levels.");
                var signatures = new HashSet<string>();
                LevelData previous = null;
                for (int index = 0; index < collection.levels.Length; index++)
                {
                    var level = collection.levels[index];
                    string prefix = "Level " + (index + 1) + ": ";
                    Require(level.id == index + 1 && level.chapter == index / 10 + 1, prefix + "Incorrect campaign order.");
                    Require(!string.IsNullOrEmpty(level.title) && level.title.All(c => c < 128), prefix + "Title must be English.");
                    Require(level.w > 0 && level.h > 0 && level.a.Length >= 4 && level.a.Length <= 30, prefix + "Invalid board size.");
                    Require(level.par > 0 && level.moves >= level.par && level.solution.Length == level.par, prefix + "Invalid move budget/route.");
                    Require(level.hints >= 0 && level.erasers >= 0 && level.extraMoves >= 0, prefix + "Invalid inventory.");
                    var occupied = new HashSet<Vector2>();
                    foreach (var arrow in level.a)
                    {
                        Require(new[] { "A", "B", "C" }.Contains(arrow.c) && new[] { "u", "d", "l", "r" }.Contains(arrow.d), prefix + "Invalid arrow code.");
                        Require(arrow.cells.Length >= 2, prefix + "Arrow needs a head and body.");
                        for (int j = 0; j < arrow.cells.Length; j++)
                        {
                            var cell = arrow.cells[j];
                            Require(cell.x >= 0 && cell.x < level.w && cell.y >= 0 && cell.y < level.h && occupied.Add(cell.V), prefix + "Cell outside board or overlapping.");
                            if (j > 0) Require((cell.V - arrow.cells[j - 1].V).sqrMagnitude == 1, prefix + "Disconnected path.");
                        }
                        int last = arrow.cells.Length - 1;
                        Require(arrow.cells[last].V - arrow.cells[last - 1].V == HueEngine.Dir(arrow.d), prefix + "Head direction disagrees with body.");
                    }
                    Require(signatures.Add(Signature(level)), prefix + "Duplicate/recolored/reflected layout.");
                    if (index >= 20) Require(level.a.Select(a => a.c).Distinct().Count() == 3, prefix + "Missing third color.");
                    if (previous != null)
                    {
                        Require(level.a.Length >= previous.a.Length && level.difficulty > previous.difficulty, prefix + "Difficulty must increase.");
                        Require(level.hints <= previous.hints && level.erasers <= previous.erasers && level.extraMoves <= previous.extraMoves
                            && level.moves - level.par <= previous.moves - previous.par, prefix + "Assistance increased unexpectedly.");
                    }
                    var puzzle = HueEngine.Prep(level);
                    Require(puzzle.arrs.All(a => !a.self), prefix + "Self-blocking arrow.");
                    int mask = puzzle.Full;
                    foreach (int selected in level.solution)
                    {
                        Require(selected >= 0 && selected < puzzle.n && HueEngine.IsFree(puzzle, mask, selected), prefix + "Invalid standard action.");
                        mask = HueEngine.Chain(puzzle, mask, selected);
                    }
                    Require(mask == 0, prefix + "Standard route did not clear board.");
                    // Independent exhaustive dynamic programming on the smaller boards
                    // also checks the offline A* author's optimal-move claim.
                    if (index < 30) Require(Shortest(puzzle, puzzle.Full, new Dictionary<int, int>()) == level.par, prefix + "Incorrect optimal move count.");
                    previous = level;
                }
                Require(Mathf.Approximately(collection.levels[0].difficulty, 1) && Mathf.Approximately(collection.levels[99].difficulty, 100), "Invalid difficulty scale.");
            }
            catch (Exception e) { errors.Add("Campaign validation: " + e.Message); }
            return errors;
        }

        static int Shortest(Puzzle puzzle, int mask, Dictionary<int, int> memo)
        {
            if (mask == 0) return 0;
            if (memo.TryGetValue(mask, out int value)) return value;
            int best = 1000;
            for (int i = 0; i < puzzle.n; i++)
                if (HueEngine.IsFree(puzzle, mask, i)) best = Math.Min(best, 1 + Shortest(puzzle, HueEngine.Chain(puzzle, mask, i), memo));
            return memo[mask] = best;
        }

        static string Signature(LevelData level)
        {
            var versions = new List<string>();
            foreach (int sx in new[] { 1, -1 })
                foreach (int sy in new[] { 1, -1 })
                    versions.Add(string.Join("|", level.a.Select(a => string.Join(";", a.cells.Select(c =>
                        (sx == 1 ? c.x : level.w - 1 - c.x) + "," + (sy == 1 ? c.y : level.h - 1 - c.y))))
                        .OrderBy(a => a, StringComparer.Ordinal)));
            return level.w + "x" + level.h + ":" + versions.OrderBy(s => s, StringComparer.Ordinal).First();
        }

        public static void CheckRuntime(HueGame game, HueView view)
        {
            var levels = Load().levels;
            Require(game.LevelCount == levels.Length, "Runtime campaign length is incorrect.");
            var hintRoute = typeof(HueGame).GetMethod("CampaignHint", Flags);
            var render = typeof(HueGame).GetMethod("Render", Flags);
            var maskProperty = typeof(HueGame).GetProperty("Mask");
            for (int index = 0; index < levels.Length; index++)
            {
                var level = levels[index];
                game.StartLevel(index);
                Require(game.Limit == level.moves && game.HintsRemaining == level.hints && game.ErasersRemaining == level.erasers
                    && game.ExtraMovesRemaining == level.extraMoves && game.IsLastCampaignLevel == (index == levels.Length - 1), "Runtime level configuration: " + level.id);
                game.Hint();
                Require(game.HintsRemaining == level.hints - 1 && (int)typeof(HueGame).GetField("hintId", Flags).GetValue(game) == level.solution[0], "Hint ignored authored route: " + level.id);
                game.Retry();
                Require(game.Mask == game.P.Full && game.Limit == level.moves && game.HintsRemaining == level.hints
                    && game.ErasersRemaining == level.erasers && game.ExtraMovesRemaining == level.extraMoves, "Retry lost configuration: " + level.id);
                render.Invoke(game, null);
                Require(view.levelTitle.text == "Level " + level.id + "/100", "Incorrect campaign HUD: " + level.id);
                foreach (int selected in level.solution)
                {
                    Require((int)hintRoute.Invoke(game, null) == selected, "Hint route prefix failed: " + level.id);
                    maskProperty.SetValue(game, HueEngine.Chain(game.P, game.Mask, selected));
                }
            }
            foreach (int index in new[] { 4, 9, 19, 49, 89, 98 })
            {
                game.StartLevel(index);
                typeof(HueGame).GetMethod("Finish", Flags).Invoke(game, new object[] { true });
                typeof(HueGame).GetMethod("ShowSheet", Flags).Invoke(game, null);
                game.ContinueResult();
                Require(!game.Endless && !game.Over && game.LevelIndex == index + 1, "Campaign advance failed: " + index);
            }
            game.StartLevel(-10);
            Require(game.LevelIndex == 0, "Lower level clamp failed.");
            game.StartLevel(1000);
            Require(game.LevelIndex == 99, "Upper level clamp failed.");
            game.StartLevel(0);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
