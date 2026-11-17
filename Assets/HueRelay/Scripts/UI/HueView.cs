using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>
    /// 保存在场景中的固定界面引用与自适应布局。此组件不创建对象、不生成关卡，
    /// 编辑模式也可预览固定界面；业务状态和动画时间仍由 HueGame 驱动。
    /// </summary>
    [ExecuteAlways]
    public sealed class HueView : MonoBehaviour
    {
        [Header("场景和资源绑定")]
        public Canvas canvas;
        public TextAsset levelData;
        public Material canvasMaterial;
        public Material backdropMaterial;
        public HueSurface boardSurface;
        public HueSurface effectsSurface;

        [Header("顶部固定结构")]
        public RectTransform header;
        public RectTransform head;
        public Button retryButton;
        public Button helpButton;
        public Button hintButton;
        public HuePanel hintBadge;
        public Text hintCount;
        public Text score;
        public Text best;
        public Text levelTitle;
        public HuePanel movesChip;
        public Text moves;
        public RectTransform progressTrack;
        public RectTransform progressFill;

        [Header("棋盘容器")]
        public RectTransform stage;
        public RectTransform boardFrame;
        public HuePanel boardBackground;

        [Header("连锁和里程碑")]
        public CanvasGroup comboGroup;
        public Text combo;
        public CanvasGroup milestoneGroup;
        public Text milestoneNumber;
        public Text milestoneCaption;
        public RawImage finger;

        [Header("教学卡片")]
        public CanvasGroup tutorialGroup;
        public Text tutorialText;
        public Button tutorialNext;
        public Text tutorialNextText;

        [Header("结算卡片")]
        public HuePanel resultBackdrop;
        public CanvasGroup resultGroup;
        public Text resultTitle;
        public Text resultScore;
        public Text resultDetail;
        public RectTransform resultButtons;
        public Button resultPrimary;
        public Text resultPrimaryText;
        public Button resultSecondary;
        public Text resultSecondaryText;
        public HuePanel victoryFlash;

        [Header("固定布局参数（CSS 逻辑像素）")]
        [Min(0)] public float sidePadding = 14;
        [Min(0)] public float topPadding = 12;
        [Min(0)] public float toolbarHeight = 44;
        [Min(0)] public float sectionGap = 14;
        [Min(0)] public float scoreLineHeight = 41.8f;
        [Min(0)] public float bestLineHeight = 17;
        [Min(0)] public float bestTopGap = 4;
        [Min(0)] public float progressHeight = 6;
        [Min(0)] public float backdropBlur = 4;

        private float layoutWidth = -1;
        private float layoutHeight = -1;
        private float layoutScale = -1;
        private float layoutStageY = -1;
        private float layoutStageHeight = -1;
        private float layoutBoardWidth = -1;
        private float layoutBoardHeight = -1;
        private float layoutPhysicalScale = -1;
        private bool layoutTutorial;
        private Vector2 previewSize;
        private float lastResultDetail = -1;
        private bool lastResultWin;

        /// <summary>启动前验证场景绑定；缺失时由控制器报错，不回退为运行时重新搭建整套 UI。</summary>
        public bool ValidateBindings(out string reason)
        {
            bool valid = canvas && levelData && canvasMaterial && backdropMaterial && boardSurface && effectsSurface
                && header && head && retryButton && helpButton && hintButton && hintBadge && hintCount
                && score && best && levelTitle && movesChip && moves && progressTrack && progressFill
                && stage && boardFrame && boardBackground && comboGroup && combo && milestoneGroup
                && milestoneNumber && milestoneCaption && finger && tutorialGroup && tutorialText
                && tutorialNext && tutorialNextText && resultBackdrop && resultGroup && resultTitle
                && resultScore && resultDetail && resultButtons && resultPrimary && resultPrimaryText
                && resultSecondary && resultSecondaryText && victoryFlash && effectsSurface.TextCapacity >= 40;
            reason = valid ? null : "HueView 的固定界面、资源或预置特效文字池存在未绑定项。";
            return valid;
        }

        /// <summary>仅在分辨率、教学头部或关卡尺寸变化时重排容器，保留子节点在场景中的布局。</summary>
        public void ApplyLayout(float width, float height, float scale, float appX, float appWidth,
            float top, float stageY, float stageHeight, float boardX, float boardY, float boardWidth,
            float boardHeight, bool teaching, float physicalScale)
        {
            if (layoutWidth == width && layoutHeight == height && layoutScale == scale && layoutStageY == stageY
                && layoutStageHeight == stageHeight && layoutBoardWidth == boardWidth && layoutBoardHeight == boardHeight
                && layoutTutorial == teaching && layoutPhysicalScale == physicalScale)
                return;

            layoutWidth = width;
            layoutHeight = height;
            layoutScale = scale;
            layoutStageY = stageY;
            layoutStageHeight = stageHeight;
            layoutBoardWidth = boardWidth;
            layoutBoardHeight = boardHeight;
            layoutTutorial = teaching;
            layoutPhysicalScale = physicalScale;

            float headerWidth = (appWidth - sidePadding * 2 * scale) / scale;
            float headHeight = scoreLineHeight + bestTopGap + (teaching ? 0 : bestLineHeight);
            SetBox(header, new Rect(appX + sidePadding * scale, top + topPadding * scale, headerWidth, 180));
            header.localScale = Vector3.one * scale;
            head.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, headHeight);
            head.anchoredPosition = new Vector2(head.anchoredPosition.x, -(toolbarHeight + sectionGap));
            SetBox(progressTrack, new Rect(0, toolbarHeight + sectionGap + headHeight + sectionGap, headerWidth, progressHeight));
            SetBox(stage, new Rect(appX + sidePadding * scale, stageY, appWidth - sidePadding * 2 * scale, stageHeight));
            PlaceCenter(boardFrame, new Vector2(boardX + boardWidth * .5f, boardY + boardHeight * .5f),
                new Vector2(boardWidth + 16 * scale, boardHeight + 16 * scale));
            boardBackground.SetMetrics(28 * scale, 30 * scale, 10 * scale, -4 * scale);
            resultBackdrop.radius = 16 * scale;
            resultBackdrop.SetVerticesDirty();
            boardSurface.rectTransform.sizeDelta = effectsSurface.rectTransform.sizeDelta = new Vector2(width, height);
            best.gameObject.SetActive(!teaching);
            resultGroup.transform.localScale = tutorialGroup.transform.localScale = Vector3.one * scale;
            Shader.SetGlobalFloat("_HueBlurPixels", backdropBlur * scale * physicalScale);
        }

        /// <summary>更新既有 HUD 节点；不再每帧生成固定背景、按钮和标签网格。</summary>
        public void SetHud(string scoreValue, Color scoreColor, float scoreScale, float scoreWidth,
            string bestValue, string titleValue, string movesValue, Color movesColor, string hintsValue,
            bool badgeVisible, float fill, bool teaching, float movesWidth)
        {
            score.text = scoreValue;
            score.color = scoreColor;
            score.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, scoreWidth);
            score.rectTransform.anchoredPosition = new Vector2(scoreWidth * .5f, -scoreLineHeight * .47f);
            score.rectTransform.localScale = Vector3.one * scoreScale;
            best.text = bestValue;
            levelTitle.text = titleValue;
            moves.text = movesValue;
            moves.color = movesColor;
            movesChip.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, movesWidth);
            hintCount.text = hintsValue;
            SetActive(hintBadge.gameObject, badgeVisible);
            Vector2 maximum = progressFill.anchorMax;
            maximum.x = Mathf.Clamp01(fill);
            progressFill.anchorMax = maximum;
        }

        /// <summary>把已存在的浮层放到原 HTML 的绝对位置，并同步透明度和缩放。</summary>
        public static void SetFloating(CanvasGroup group, Vector2 center, float scale, float alpha)
        {
            bool visible = alpha > 0;
            SetActive(group.gameObject, visible);
            if (!visible)
                return;
            group.alpha = Mathf.Clamp01(alpha);
            RectTransform rect = (RectTransform)group.transform;
            rect.anchoredPosition = new Vector2(center.x, -center.y);
            rect.localScale = Vector3.one * scale;
        }

        /// <summary>更新教学内容与卡片尺寸；按钮、背景和文字组件始终来自场景。</summary>
        public void SetTutorial(string text, int step, int lines, float scale, float easing)
        {
            SetActive(tutorialGroup.gameObject, true);
            tutorialGroup.alpha = Mathf.Clamp01(easing);
            bool hasButton = step >= 3;
            float buttonWidth = step == 5 ? 66 : 81;
            float width = stage.rect.width / scale - 20;
            float height = 32 + Mathf.Max(lines * 24.65f, hasButton ? 38 : 0);
            RectTransform card = (RectTransform)tutorialGroup.transform;
            card.sizeDelta = new Vector2(width, height);
            card.anchoredPosition = new Vector2(0, 10 * scale - (1 - easing) * 40 * scale);
            card.localScale = Vector3.one * scale;
            tutorialText.text = text;
            tutorialText.rectTransform.sizeDelta = new Vector2(width - 36 - (hasButton ? 12 + buttonWidth : 0), lines * 24.65f);
            tutorialText.rectTransform.anchoredPosition = new Vector2(tutorialText.rectTransform.anchoredPosition.x,
                -16 - (Mathf.Max(lines * 24.65f, hasButton ? 38 : 0) - lines * 24.65f) * .5f);
            SetActive(tutorialNext.gameObject, hasButton);
            tutorialNext.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, buttonWidth);
            tutorialNextText.text = step == 5 ? "开始" : "下一步";
        }

        /// <summary>更新结算内容和原版底部滑入曲线，不创建或销毁任何固定控件。</summary>
        public void SetResult(bool win, bool newRecord, float scale, float easing, string title, string number,
            string detail, string primary, string secondary)
        {
            SetActive(resultBackdrop.gameObject, true);
            SetActive(resultGroup.gameObject, true);
            resultGroup.alpha = Mathf.Clamp01(easing);
            float detailHeight = win && newRecord ? 47.6f : 23.8f;
            RectTransform card = (RectTransform)resultGroup.transform;
            card.sizeDelta = new Vector2(stage.rect.width / scale,
                5 + 4 + 29 + 68 + detailHeight + 50 + (win ? 36 : 0) + (win ? 50 : 40) + 28);
            card.anchoredPosition = new Vector2(0, 8 * scale - (1 - easing) * 40 * scale);
            card.localScale = Vector3.one * scale;
            resultTitle.text = title;
            resultScore.text = number;
            resultDetail.text = detail;
            resultPrimaryText.text = primary;
            resultSecondaryText.text = secondary;
            if (lastResultDetail != detailHeight || lastResultWin != win)
            {
                lastResultDetail = detailHeight;
                lastResultWin = win;
                resultDetail.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, detailHeight);
                resultButtons.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, win ? 96 : 50);
                SetActive(resultSecondary.gameObject, win);
            }
        }

        /// <summary>重置时立即隐藏旧面板，避免等下一帧才清除上一局的可见状态。</summary>
        public void HideTransient()
        {
            SetActive(resultBackdrop.gameObject, false);
            SetActive(resultGroup.gameObject, false);
            SetActive(tutorialGroup.gameObject, false);
            SetActive(comboGroup.gameObject, false);
            SetActive(milestoneGroup.gameObject, false);
            SetActive(finger.gameObject, false);
            victoryFlash.color = new Color(1, 1, 1, 0);
        }

        /// <summary>固定 UI 的矩形占用区域用于隔离棋盘输入，不依赖上一帧 EventSystem 的指针状态。</summary>
        public bool BlocksBoardPointer(Vector2 logicalPosition, float physicalScale)
        {
            Vector2 screen = new Vector2(logicalPosition.x * physicalScale, Screen.height - logicalPosition.y * physicalScale);
            if (UIManager.Instance != null && UIManager.Instance.BlocksPointer(screen))
                return true;
            if (resultGroup.gameObject.activeSelf)
                return true;
            if (tutorialGroup.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)tutorialGroup.transform, screen))
                return true;
            return RectTransformUtility.RectangleContainsScreenPoint(retryButton.transform as RectTransform, screen)
                || RectTransformUtility.RectangleContainsScreenPoint(helpButton.transform as RectTransform, screen)
                || RectTransformUtility.RectangleContainsScreenPoint(hintButton.transform as RectTransform, screen);
        }

        private void OnEnable()
        {
            Preview();
        }
        private void Update()
        {
            if (!Application.isPlaying && canvas && previewSize != ((RectTransform)canvas.transform).rect.size)
                Preview();
        }

        private void Preview()
        {
            if (Application.isPlaying || !canvas || !ValidateBindings(out _))
                return;
            previewSize = ((RectTransform)canvas.transform).rect.size;
            float width = Mathf.Max(1, previewSize.x);
            float height = Mathf.Max(1, previewSize.y);
            float scale = Mathf.Clamp(Mathf.Min(width / 390, height / 800), .75f, 3.5f);
            float appWidth = Mathf.Min(width, 460 * scale);
            float stageY = (topPadding + toolbarHeight + sectionGap + scoreLineHeight + bestTopGap + bestLineHeight + sectionGap + progressHeight) * scale;
            var manager = FindObjectOfType<UIManager>();
            float footerHeight = manager != null ? manager.FooterHeightInPixels() / canvas.scaleFactor : 0;
            float stageHeight = height + 12 * scale - stageY - footerHeight;
            float cell = Mathf.Max(16, Mathf.Min(64 * scale, Mathf.Floor((appWidth - 60 * scale) / 5), Mathf.Floor((stageHeight - 32 * scale) / 6)));
            ApplyLayout(width, height, scale, (width - appWidth) * .5f, appWidth, 0, stageY, stageHeight,
                width * .5f - cell * 2.5f, stageY + (stageHeight - cell * 6) * .5f, cell * 5, cell * 6, false, canvas.scaleFactor);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf != active)
                target.SetActive(active);
        }

        private static void SetBox(RectTransform target, Rect box)
        {
            target.anchorMin = target.anchorMax = target.pivot = new Vector2(0, 1);
            target.anchoredPosition = new Vector2(box.x, -box.y);
            target.sizeDelta = box.size;
        }

        private static void PlaceCenter(RectTransform target, Vector2 center, Vector2 size)
        {
            target.anchorMin = target.anchorMax = new Vector2(0, 1);
            target.pivot = new Vector2(.5f, .5f);
            target.anchoredPosition = new Vector2(center.x, -center.y);
            target.sizeDelta = size;
        }
    }
}
