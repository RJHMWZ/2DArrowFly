using UnityEngine;
using static HueRelay.HueEngine;

namespace HueRelay
{
    /// <summary>
    /// 游戏控制器的教学部分：按指定箭头推进六步教学，并保存、恢复教学前的棋盘状态。
    /// </summary>
    public sealed partial class HueGame
    {
        private int tut = -1;
        private bool tutWait;
        private float tutIdle = -1;
        private float tutStepAt;
        private int tutorialAdvancedFrame = -1;
        private Snapshot snapshot;
        private Puzzle guidePuzzle;
        private int guideMask = -1;
        private int guideArrow = -1;
        private bool UsesChainGuide => tut < 0 && !Endless && P != null && P.L.guidedChains;

        private bool CanStartGuidedChain(int id)
        {
            return id >= 0 && id < P.n && IsFree(P, Mask, id)
                && PC(Mask) - PC(Chain(P, Mask, id)) >= 2;
        }

        private void UpdateCampaignGuide()
        {
            if (!UsesChainGuide) return;
            if (Over || !Idle || Mask == 0)
            {
                hintId = -1;
                return;
            }
            if (guidePuzzle != P || guideMask != Mask)
            {
                guidePuzzle = P;
                guideMask = Mask;
                guideArrow = CampaignHint();
                if (!CanStartGuidedChain(guideArrow))
                {
                    guideArrow = -1;
                    for (int i = 0; i < P.n; i++)
                        if (CanStartGuidedChain(i)) { guideArrow = i; break; }
                }
                hintAt = Mathf.Max(now, built + P.n * .026f + .45f);
            }
            hintId = guideArrow;
        }

        /// <summary>
        /// 进入教学前保存的游戏快照；教学不应消耗原棋局的分数、步数或提示次数。
        /// </summary>
        sealed class Snapshot
        {
            public Puzzle p;
            public int mask;
            public int taps;
            public int score;
            public int limit;
            public int par;
        }

        private static readonly string[] TutorialText =
        {
            "Tap an arrow to send it flying.",
            "Blocked arrows cannot move.",
            "Clear the orange arrow first.",
            "Matching colors chain for free.\nTap anywhere to continue.",
            "Longer chains earn more points.\nTap anywhere to continue.",
            "Clear the board before moves run out.\nTap anywhere to play."
        };
        private static readonly int[] TutorialIds =
        {
            0,
            1,
            2,
            -1,
            -1,
            -1
        };

        /// <summary>
        /// 是否正在展示教学。
        /// </summary>
        public bool IsTutorial
        {
            get
            {
                return tut >= 0;
            }
        }

        /// <summary>
        /// 当前教学步骤编号；未教学时为 -1。
        /// </summary>
        public int TutorialStepIndex
        {
            get
            {
                return tut;
            }
        }

        /// <summary>
        /// 保存当前棋局并换入教学棋盘；有飞行或碰撞未完成时不切换。
        /// </summary>
        public void StartTutorial()
        {
            if (!enabled || !root || P == null || LevelCount == 0 || !Idle || refillOpen)
                return;
            TrackGameEvent("tutorial_start");
            ResetIdleMoveGuide();
            CancelErase();
            var sn = Over ? null : new Snapshot
            {
                p = P,
                mask = Mask,
                taps = Taps,
                score = Score,
                limit = Limit,
                par = par
            };
            timed.Clear();
            q.Clear();
            nextChain = -1;
            ClearEffects();
            P = Prep(new LevelData(9, 12, new[] { new ArrowData("B", "r", new[] { new Cell(1, 2), new Cell(2, 2), new Cell(3, 2) }), new ArrowData("C", "r", new[] { new Cell(1, 5), new Cell(2, 5), new Cell(3, 5) }), new ArrowData("C", "u", new[] { new Cell(5, 6), new Cell(5, 5), new Cell(5, 4) }), new ArrowData("C", "u", new[] { new Cell(3, 8), new Cell(3, 7), new Cell(3, 6) }) }));
            Mask = 15;
            Taps = Score = 0;
            ds = 0;
            Limit = par = 9;
            Over = busy = sheetVisible = false;
            built = now;
            charged.Clear();
            chargeAt.Clear();
            snapshot = sn;
            Layout();
            UIProgress();
            TutorialStep(0);
        }

        /// <summary>
        /// 显示指定教学卡片，并在需要时定位提示圆环和点击手势。
        /// </summary>
        /// <param name="index">预设关卡或教学步骤的零基编号。</param>
        private void TutorialStep(int index)
        {
            tut = Mathf.Clamp(index, 0, 5);
            tutWait = false;
            tutIdle = -1;
            tutStepAt = now;
            if (TutorialIds[tut] >= 0)
            {
                hintId = TutorialIds[tut];
                hintAt = Mathf.Max(now, built + P.n * .026f + .45f);
            }
            else
                hintId = -1;
        }

        /// <summary>
        /// 保存已看教学标记，清理教学特效，并恢复原棋局或重新进入当前关卡。
        /// </summary>
        public void EndTutorial()
        {
            if (tut < 0) return;
            TrackGameEvent("tutorial_complete");
            ResetIdleMoveGuide();
            bool enterEndless = resumeEndlessAfterTutorial;
            resumeEndlessAfterTutorial = false;
            PlayerPrefs.SetInt(tutorialSeenKey, 1);
            PlayerPrefs.Save();
            var sn = snapshot;
            guidePuzzle = null;
            timed.Clear();
            ClearEffects();
            q.Clear();
            charged.Clear();
            chargeAt.Clear();
            busy = false;
            nextChain = -1;
            tut = -1;
            hintId = -1;
            finishAt = -1;
            if (sn != null)
            {
                P = sn.p;
                Mask = sn.mask;
                Taps = sn.taps;
                Score = sn.score;
                ds = Score;
                Limit = sn.limit;
                par = sn.par;
                built = now;
                Layout();
                progress = progressTarget = (P.n - PC(Mask)) / (float)P.n;
                snapshot = null;
            }
            else
                StartLevel(LevelIndex);
            if (enterEndless) StartEndless();
        }
    }
}
