using System.Collections.Generic;
using UnityEngine;

namespace HueRelay
{
    public sealed partial class HueGame
    {
        private struct UndoStep { public int id, points; }
        private struct UndoLaunch { public int arrow, owner; }
        private struct ReturningArrow { public int arrow; public float start, distance; }
        private const int UndoCapacity = 64;
        private const float ReturnDuration = .5f;
        private readonly List<UndoStep> undoSteps = new List<UndoStep>(UndoCapacity);
        private readonly List<UndoLaunch> undoLaunches = new List<UndoLaunch>(32);
        private readonly List<ReturningArrow> returningArrows = new List<ReturningArrow>(32);
        // Engine masks support at most 30 arrows. Ownership keeps overlapping
        // manual taps and automatic chains separate without changing their rules.
        private readonly int[] undoOwners = new int[32];
        private int undoSequence;
        public bool IsUndoing => returningArrows.Count > 0;
        public bool CanUndo => CanRequestHint && hints > 0 && undoSteps.Count > 0;
        public int UndosRemaining => hints; // Reuse existing inventory/save key.

        private void BeginUndoStep(int arrow)
        {
            if (undoSteps.Count == UndoCapacity)
            {
                int oldest = undoSteps[0].id;
                undoSteps.RemoveAt(0);
                for (int i = undoLaunches.Count - 1; i >= 0; i--)
                    if (undoLaunches[i].owner == oldest) undoLaunches.RemoveAt(i);
            }
            int owner = ++undoSequence;
            undoSteps.Add(new UndoStep { id = owner });
            undoOwners[arrow] = owner;
        }

        private void RecordUndoLaunch(int arrow, int points)
        {
            if (tut >= 0) return;
            int owner = undoOwners[arrow];
            for (int i = undoSteps.Count - 1; i >= 0; i--)
            {
                var step = undoSteps[i];
                if (step.id != owner) continue;
                step.points += points;
                undoSteps[i] = step;
                undoLaunches.Add(new UndoLaunch { arrow = arrow, owner = owner });
                return;
            }
        }

        public void Undo()
        {
            TrackGameEvent("powerup_click", "powerup", "undo");
            ResetIdleMoveGuide();
            if (!CanRequestHint) return;
            if (hints == 0) { OpenPowerUpRefill(HuePowerUpKind.Hint); return; }
            if (!CanUndo || !Inventory.TrySpend(HuePowerUpKind.Hint)) return;
            var step = undoSteps[undoSteps.Count - 1];
            undoSteps.RemoveAt(undoSteps.Count - 1);
            now = GameTime;
            timed.Clear(); // Discard delayed notes/rings belonging to the completed chain.
            ClearEffects();
            hintId = -1;
            chainN = 0;
            finishAt = nextChain = -1;
            comboAt = mileAt = -100;
            Score = Mathf.Max(0, Score - step.points);
            Taps = Mathf.Max(0, Taps - 1);
            // Launch order is also return order, including every automatic arrow
            // owned by this tap. Earlier overlapping chains remain untouched.
            int index = 0;
            for (int i = 0; i < undoLaunches.Count; i++)
            {
                var launch = undoLaunches[i];
                if (launch.owner != step.id) continue;
                returningArrows.Add(new ReturningArrow { arrow = launch.arrow,
                    start = now + index++ * .09f, distance = ReturnDistance(P.arrs[launch.arrow]) });
            }
            for (int i = undoLaunches.Count - 1; i >= 0; i--)
                if (undoLaunches[i].owner == step.id) undoLaunches.RemoveAt(i);
            TrackGameEvent("powerup_use", "powerup", "undo", "arrows_returned", index);
            audioBus.Tone(660, .14, .08);
            UIProgress();
            Render();
        }

        private float ReturnDistance(Arrow arrow)
        {
            Vector2 head = new Vector2(ox, oy) + G(arrow.Head);
            float distance = arrow.d.x > 0 ? viewWidth + cs - head.x
                : arrow.d.x < 0 ? head.x + cs : arrow.d.y > 0 ? viewHeight + cs - head.y : head.y + cs;
            return arrow.cells.Length - 1 + Mathf.Max(1, distance / cs);
        }

        private float ReturningOffset(ReturningArrow arrow)
        {
            float t = Mathf.Clamp01((now - arrow.start) / ReturnDuration);
            // Reverse the same arc-length window used by outgoing flights. Cubic
            // ease-out slows the arrival instead of snapping the arrow into place.
            return arrow.distance * Mathf.Pow(1 - t, 3);
        }

        private void UpdateUndo()
        {
            for (int i = 0; i < returningArrows.Count;)
            {
                var arrow = returningArrows[i];
                if (now < arrow.start + ReturnDuration) { i++; continue; }
                Mask |= 1 << arrow.arrow;
                returningArrows.RemoveAt(i);
                UIProgress();
                Ring(G(P.arrs[arrow.arrow].Head), Col(P.arrs[arrow.arrow].c), cs * .12f, 2, 5, 1.5f);
            }
        }

        private void ResetUndo()
        {
            undoSteps.Clear(); undoLaunches.Clear(); returningArrows.Clear();
            System.Array.Clear(undoOwners, 0, undoOwners.Length);
            undoSequence = 0;
        }
    }
}
