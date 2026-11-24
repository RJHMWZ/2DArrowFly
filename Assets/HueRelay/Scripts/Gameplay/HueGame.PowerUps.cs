using UnityEngine;

namespace HueRelay
{
    public sealed partial class HueGame
    {
        private int eraseUses => Inventory.Count(HuePowerUpKind.Eraser);
        private int extraMoveUses => Inventory.Count(HuePowerUpKind.ExtraMoves);
        private int baseMoveLimit;
        private int erasingArrow = -1;
        private float eraseStarted;
        private float eraseDuration;
        public bool IsChoosingErase { get; private set; }
        public bool IsErasing => erasingArrow >= 0;
        private float EraseProgress => IsErasing ? Mathf.Clamp01((now - eraseStarted) / eraseDuration) : 0;

        // Lock tools as soon as moves run out or the last arrow clears, including
        // settling before Finish and the entire result-panel lifetime.
        private bool PowerUpsReady => !IsPaused && !refillOpen && !Over && !sheetVisible && Taps < Limit
            && Mask != 0 && !generating && tut < 0 && !UsesChainGuide && Idle;
        public bool CanHint => PowerUpsReady && !IsChoosingErase && hints > 0;
        public bool CanErase => PowerUpsReady && eraseUses > 0;
        public bool CanAddMoves => PowerUpsReady && !IsChoosingErase && extraMoveUses > 0;
        private bool CanRequestHint => PowerUpsReady && !IsChoosingErase;
        private bool CanRequestErase => PowerUpsReady;
        private bool CanRequestExtraMoves => PowerUpsReady && !IsChoosingErase;
        public int HintsRemaining => hints;
        public int ErasersRemaining => eraseUses;
        public int ExtraMovesRemaining => extraMoveUses;

        public void ToggleErase()
        {
            TrackGameEvent("powerup_click", "powerup", "eraser");
            ResetIdleMoveGuide();
            if (IsChoosingErase)
            {
                IsChoosingErase = false;
                Render();
                return;
            }
            if (!CanRequestErase) return;
            if (eraseUses == 0) { OpenPowerUpRefill(HuePowerUpKind.Eraser); return; }
            IsChoosingErase = true;
            hintId = -1;
            finishAt = -1;
            Render();
        }

        private void SelectEraseArrow(int id)
        {
            if (!IsChoosingErase || !CanErase || id < 0 || id >= P.n || (Mask & (1 << id)) == 0) return;
            IsChoosingErase = false;
            if (!Inventory.TrySpend(HuePowerUpKind.Eraser)) return;
            TrackGameEvent("powerup_use", "powerup", "eraser");
            erasingArrow = id;
            now = eraseStarted = GameTime;
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
            TrackGameEvent("powerup_click", "powerup", "extra_moves", "from_idle_guide",
                idleMoveGuide || moveGuideInputFrame == Time.frameCount ? 1 : 0);
            ResetIdleMoveGuide();
            if (!CanRequestExtraMoves) return;
            if (extraMoveUses == 0) { OpenPowerUpRefill(HuePowerUpKind.ExtraMoves); return; }
            if (!Inventory.TrySpend(HuePowerUpKind.ExtraMoves)) return;
            TrackGameEvent("powerup_use", "powerup", "extra_moves");
            Limit += 3;
            finishAt = -1;
            Pop(1.12f);
            audioBus.Tone(784, .14, .07);
            Render();
        }
    }
}
