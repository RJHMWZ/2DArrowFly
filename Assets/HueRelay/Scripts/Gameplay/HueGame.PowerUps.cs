using UnityEngine;

namespace HueRelay
{
    public sealed partial class HueGame
    {
        private int eraseUses = 3;
        private int extraMoveUses = 1;
        private int baseMoveLimit;
        private int erasingArrow = -1;
        private float eraseStarted;
        private float eraseDuration;
        public bool IsChoosingErase { get; private set; }
        public bool IsErasing => erasingArrow >= 0;
        private float EraseProgress => IsErasing ? Mathf.Clamp01((now - eraseStarted) / eraseDuration) : 0;

        // Clearing the final arrow already commits the board to a win, including
        // the settling interval before the victory flags are updated.
        private bool PowerUpsReady => Mask != 0 && !generating && tut < 0 && !UsesChainGuide && (!Over || !Won) && Idle;
        public bool CanHint => PowerUpsReady && !IsChoosingErase && !Over && hints > 0 && Taps < Limit;
        public bool CanErase => PowerUpsReady && eraseUses > 0;
        public bool CanAddMoves => PowerUpsReady && !IsChoosingErase && extraMoveUses > 0;
        public int HintsRemaining => hints;
        public int ErasersRemaining => eraseUses;
        public int ExtraMovesRemaining => extraMoveUses;

        public void ToggleErase()
        {
            if (IsChoosingErase)
            {
                IsChoosingErase = false;
                Render();
                return;
            }
            if (!CanErase) return;
            if (Over)
            {
                ClearPendingMove();
                ResumeBoard();
            }
            IsChoosingErase = true;
            hintId = -1;
            finishAt = -1;
            Render();
        }

        private void SelectEraseArrow(int id)
        {
            if (!IsChoosingErase || !CanErase || id < 0 || id >= P.n || (Mask & (1 << id)) == 0) return;
            IsChoosingErase = false;
            eraseUses--;
            erasingArrow = id;
            now = eraseStarted = Time.unscaledTime;
            eraseDuration = Mathf.Clamp((P.arrs[id].cells.Length - 1) * .08f, .65f, 1.25f);
            finishAt = -1;
            audioBus.Tone(520, .12, .045);
            Render();
        }

        private void UpdateErase()
        {
            if (!IsErasing || EraseProgress < 1) return;
            // Remove exactly the selected arrow, including a blocked arrow.
            // Erasing spends neither a move nor launch points and starts no chain.
            Mask &= ~(1 << erasingArrow);
            erasingArrow = -1;
            finishAt = -1;
            UIProgress();
            audioBus.Tone(660, .1, .04);
        }

        private void CancelErase()
        {
            IsChoosingErase = false;
            erasingArrow = -1;
            sceneView.HideErasePresentation();
        }

        public void AddMoves()
        {
            if (!CanAddMoves) return;
            extraMoveUses--;
            Limit += 3;
            if (Over)
            {
                ClearPendingMove();
                ResumeBoard();
            }
            finishAt = -1;
            Pop(1.12f);
            audioBus.Tone(784, .14, .07);
            Render();
        }

        private void ClearPendingMove()
        {
            timed.Clear();
            q.Clear();
            auto.Clear();
            charged.Clear();
            chargeAt.Clear();
            ClearEffects();
            CancelErase();
            nextChain = -1;
            chainN = 0;
        }

        private void ResumeBoard()
        {
            now = Time.unscaledTime;
            busy = Over = Won = sheetVisible = false;
            finishAt = -1;
            remaining = bonusLaunched = bonusLanded = 0;
            chipOverride = null;
            hintId = -1;
            punchAt = endAt = modalAt = comboAt = mileAt = scorePop = -100;
            sceneView.HideTransient();
        }
    }
}
