using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace HueRelay
{
    /// <summary>
    /// 保存在场景中的固定界面引用与自适应布局；仅兼容旧场景时补充分数背景，
    /// 编辑模式也可预览固定界面；业务状态和动画时间仍由 HueGame 驱动。
    /// </summary>
    [ExecuteAlways]
    public sealed class HueView : MonoBehaviour
    {
        [Header("Scene and Assets")]
        public Canvas canvas;
        public TextAsset levelData;
        public Material canvasMaterial;
        public Material backdropMaterial;
        public HueSurface boardSurface;
        public HueSurface effectsSurface;

        [Header("Header")]
        public RectTransform header;
        public RectTransform head;
        public Button retryButton;
        public Button helpButton;
        public Text score;
        public Text best;
        public Text levelTitle;
        public Image scoreChip;
        public Image movesChip;
        public Text moves;
        public RectTransform progressTrack;
        public RectTransform progressFill;
        public Text progressCaption;

        [Header("Power Ups")]
        public RectTransform powerUpBar;
        public Button hintButton;
        [FormerlySerializedAs("undoButton")] public Button eraseButton;
        public Button extraMovesButton;
        public Image hintBadge;
        public Text hintCount;
        [FormerlySerializedAs("undoCount")] public Text eraseCount;
        public Text extraMovesCount;

        [Header("Erase Presentation")]
        public Text eraseLabel;
        public Text erasePrompt;
        public Image eraseCursor;
        public RawImage eraseDust;
        [Min(16)] public float eraserSize = 42;
        [Min(32)] public float eraseDustSize = 96;
        [Range(0, 1)] public float eraseDustOpacity = 1;
        public Color eraseDustTint = new Color(.82f, .72f, 1, 1);

        [Header("Board")]
        public RectTransform stage;
        public RectTransform boardFrame;
        public Image boardBackground;

        [Header("Chains and Milestones")]
        public CanvasGroup comboGroup;
        public Text combo;
        public CanvasGroup milestoneGroup;
        public Text milestoneNumber;
        public Text milestoneCaption;
        public RawImage finger;

        [Header("Tutorial Card")]
        public CanvasGroup tutorialGroup;
        public Text tutorialText;
        public Button tutorialNext;
        public Text tutorialNextText;

        [Header("Result Card")]
        public Image resultBackdrop;
        public CanvasGroup resultGroup;
        public Text resultTitle;
        public Text resultScore;
        public Text resultDetail;
        public RectTransform resultButtons;
        public Button resultPrimary;
        public Text resultPrimaryText;
        public Button resultSecondary;
        public Text resultSecondaryText;
        public Image resultEmblem;
        public Image victoryFlash;

        [Header("Layout (Logical Pixels)")]
        [Min(0)] public float sidePadding = 14;
        [Min(0)] public float topPadding = 12;
        [Min(0)] public float toolbarHeight = 44;
        [Min(0)] public float sectionGap = 14;
        [Min(0)] public float scoreLineHeight = 41.8f;
        [Min(0)] public float bestLineHeight = 17;
        [Min(0)] public float bestTopGap = 4;
        [Min(0)] public float progressHeight = 16;
        [Min(0)] public float backdropBlur = 4;
        [Min(44)] public float powerUpHeight = 76;
        [Min(0)] public float powerUpBottomPadding = 12;
        [Min(0)] public float powerUpBoardGap = 16;

        public float ProgressLayoutHeight => Mathf.Clamp(progressHeight, 5, 16);
        public float HudHeight => Mathf.Max(44, scoreLineHeight);
        public float HudChipWidth => Mathf.Min(104, head.rect.width * .28f);

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
        private bool? shownComplete;
        private string measuredTutorial;
        private float measuredTutorialWidth = -1;
        private float tutorialTextHeight;
        private string measuredDetail;
        private float measuredDetailWidth = -1;
        private float resultDetailHeight;
        private int shownHintCount = -1, shownEraseCount = -1, shownExtraMovesCount = -1;
        private sealed class TextFit
        {
            public string text;
            public float width;
            public int maximum;
            public Font font;
        }
        private readonly Dictionary<Text, TextFit> textFits = new Dictionary<Text, TextFit>();
        private readonly Dictionary<Text, int> hudFontSizes = new Dictionary<Text, int>();
        private readonly Vector3[] hudCorners = new Vector3[4];
        private Vector3 scoreRestScale = Vector3.one;

        /// <summary>启动前验证场景绑定；缺失时由控制器报错，不回退为运行时重新搭建整套 UI。</summary>
        public bool ValidateBindings(out string reason)
        {
            ResolveImageBindings();
            ResolvePowerUpBindings();
            ResolvePresentationBindings();
            bool valid = canvas && levelData && canvasMaterial && backdropMaterial && boardSurface && effectsSurface
                && header && head && retryButton && helpButton && hintButton && hintBadge && hintCount
                && powerUpBar && eraseButton && extraMovesButton && eraseCount && extraMovesCount
                && score && best && levelTitle && movesChip && moves && progressTrack && progressFill
                && stage && boardFrame && boardBackground && comboGroup && combo && milestoneGroup
                && milestoneNumber && milestoneCaption && finger && tutorialGroup && tutorialText
                && tutorialNext && tutorialNextText && resultBackdrop && resultGroup && resultTitle
                && resultScore && resultDetail && resultButtons && resultPrimary && resultPrimaryText
                && resultSecondary && resultSecondaryText && victoryFlash && effectsSurface.TextCapacity >= 40;
            reason = null;
            if (!valid)
            {
                var missing = new List<string>();
                // Only inspect fields on failure; successful startup and editor previews
                // do not allocate a reflection-based list of every UI reference.
                foreach (var field in typeof(HueView).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    if (typeof(Object).IsAssignableFrom(field.FieldType) && !(field.GetValue(this) as Object))
                        missing.Add(field.Name);
                if (effectsSurface && effectsSurface.TextCapacity < 40)
                    missing.Add("effectsSurface text pool (" + effectsSurface.TextCapacity + "/40)");
                reason = "HueView is missing required bindings: " + string.Join(", ", missing.ToArray()) + ".";
            }
            return valid;
        }

        // Unity can drop serialized component references in an already-open
        // scene when a field changes from HuePanel to Image. The authored Images
        // still exist: recover those exact nodes before deciding startup failed.
        private void ResolveImageBindings()
        {
            if (!movesChip)
            {
                if (moves && moves.transform.parent)
                    movesChip = moves.transform.parent.GetComponent<Image>();
                if (!movesChip) movesChip = FindImage(head, "MovesChip");
            }
            if (!boardBackground) boardBackground = FindImage(boardFrame, "BoardBackground");
            if (!resultBackdrop) resultBackdrop = FindImage(stage, "ResultBackdrop");
            if (!victoryFlash) victoryFlash = FindImage(transform, "VictoryFlash");
        }

        private static Image FindImage(Transform scope, string objectName)
        {
            if (!scope) return null;
            foreach (var image in scope.GetComponentsInChildren<Image>(true))
                if (image.name == objectName) return image;
            return null;
        }

        // Cosmetic additions must not prevent an already open older scene from
        // starting. Recover authored controls when present; retain the old HUD
        // when its caption has not yet been imported into the open scene.
        private void ResolvePresentationBindings()
        {
            if (!scoreChip && head)
                scoreChip = head.Find("ScoreChip")?.GetComponent<Image>();
            // Older open scenes do not yet contain the newly authored pill.
            // Add only this decorative background, once, without a required binding.
            if (!scoreChip && head && movesChip && Application.isPlaying)
            {
                var chip = new GameObject("ScoreChip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                chip.transform.SetParent(head, false);
                chip.transform.SetAsFirstSibling();
                scoreChip = chip.GetComponent<Image>();
                scoreChip.material = movesChip.material;
                scoreChip.color = movesChip.color;
                scoreChip.sprite = movesChip.sprite;
                scoreChip.type = movesChip.type;
                scoreChip.pixelsPerUnitMultiplier = movesChip.pixelsPerUnitMultiplier;
                scoreChip.raycastTarget = false;
                var rect = scoreChip.rectTransform;
                rect.anchorMin = score.rectTransform.anchorMin;
                rect.anchorMax = score.rectTransform.anchorMax;
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = score.rectTransform.anchoredPosition;
                rect.sizeDelta = new Vector2(HudChipWidth, 38);
            }
            if (!progressCaption && header)
                progressCaption = header.Find("ProgressCaption")?.GetComponent<Text>();
            if (progressCaption) SetActive(progressCaption.gameObject, false);
            if (!resultEmblem && resultGroup)
            {
                var emblem = resultGroup.transform.Find("ResultEmblem");
                if (!emblem) emblem = resultGroup.transform.Find("ToolbarDivider");
                if (emblem) resultEmblem = emblem.GetComponent<Image>();
                if (resultEmblem)
                {
                    resultEmblem.color = Color.white;
                    resultEmblem.SetVerticesDirty();
                }
            }
            // Recover the disabled retry background in already-open older scenes.
            if (resultSecondary && resultSecondary.TryGetComponent<Image>(out var retryBackground) && !retryBackground.enabled && movesChip)
            {
                retryBackground.sprite = movesChip.sprite;
                retryBackground.material = null;
                retryBackground.type = Image.Type.Sliced;
                retryBackground.pixelsPerUnitMultiplier = movesChip.pixelsPerUnitMultiplier;
                retryBackground.color = new Color(.90f, .91f, .94f);
                retryBackground.enabled = true;
                resultSecondary.targetGraphic = retryBackground;
                if (resultSecondaryText) resultSecondaryText.color = new Color(.38f, .41f, .51f);
            }
            MatchResultButtonStyle();
        }

        public void MatchResultButtonStyle()
        {
            if (!resultPrimary || !resultSecondary) return;
            var primary = resultPrimary.GetComponent<Image>();
            var secondary = resultSecondary.GetComponent<Image>();
            if (!primary || !secondary) return;
            // Migrate the old baked-blue art to the shared tintable panel.
            if (primary.sprite && primary.sprite.name == "Button" && movesChip)
            {
                primary.sprite = movesChip.sprite;
                primary.color = new Color(.29f, .40f, .97f);
            }
            secondary.sprite = primary.sprite;
            secondary.material = primary.material;
            secondary.type = primary.type;
            secondary.pixelsPerUnitMultiplier = primary.pixelsPerUnitMultiplier;
            secondary.preserveAspect = primary.preserveAspect;
            secondary.enabled = true;
            resultSecondary.targetGraphic = secondary;
            var mainRect = primary.rectTransform;
            var retryRect = secondary.rectTransform;
            retryRect.anchorMin = mainRect.anchorMin;
            retryRect.anchorMax = mainRect.anchorMax;
            retryRect.pivot = mainRect.pivot;
            retryRect.sizeDelta = mainRect.sizeDelta;
            retryRect.localScale = mainRect.localScale;
            if (resultPrimaryText && resultSecondaryText)
            {
                resultSecondaryText.font = resultPrimaryText.font;
                resultSecondaryText.fontStyle = resultPrimaryText.fontStyle;
                resultSecondaryText.alignment = resultPrimaryText.alignment;
            }
        }

        /// <summary>
        /// Recover references to authored item controls after an editor script reload.
        /// Unity can retain an open scene instance that predates newly serialized fields,
        /// even though the corresponding GameObjects have already been imported.
        /// </summary>
        public void ResolvePowerUpBindings()
        {
            if (powerUpBar && hintButton && eraseButton && extraMovesButton && hintBadge && hintCount && eraseCount && extraMovesCount && eraseCursor && eraseDust && erasePrompt && eraseLabel)
                return;
            if (!powerUpBar) powerUpBar = transform.Find("PowerUpBar") as RectTransform;
            if (!powerUpBar) return;
            var hint = powerUpBar.Find("HintPowerUp");
            var eraser = powerUpBar.Find("ErasePowerUp");
            if (!eraser) eraser = powerUpBar.Find("UndoPowerUp");
            var extra = powerUpBar.Find("ExtraMovesPowerUp");
            if (hint)
            {
                if (!hintButton) hintButton = hint.GetComponent<Button>();
                var badge = FindItemBadge(hint);
                if (badge)
                {
                    if (!hintBadge) hintBadge = badge.GetComponent<Image>();
                    if (!hintCount) hintCount = badge.GetComponentInChildren<Text>(true);
                }
            }
            if (eraser)
            {
                if (!eraseButton) eraseButton = eraser.GetComponent<Button>();
                if (!eraseCount) eraseCount = FindItemBadge(eraser)?.GetComponentInChildren<Text>(true);
            }
            if (!eraseLabel && eraser) eraseLabel = eraser.Find("Label")?.GetComponent<Text>();
            if (!erasePrompt) erasePrompt = transform.Find("EraseSelectionPrompt")?.GetComponent<Text>();
            if (!eraseCursor) eraseCursor = transform.Find("EraseCursor")?.GetComponent<Image>();
            if (!eraseDust && eraseCursor) eraseDust = eraseCursor.transform.Find("Dust")?.GetComponent<RawImage>();
            if (extra)
            {
                if (!extraMovesButton) extraMovesButton = extra.GetComponent<Button>();
                if (!extraMovesCount) extraMovesCount = FindItemBadge(extra)?.GetComponentInChildren<Text>(true);
            }
        }

        private static Transform FindItemBadge(Transform item)
        {
            // Labels are Text children; the inventory badge is the child Image
            // containing a Text, independent of the scene object's display language.
            foreach (Transform child in item)
                if (child.GetComponent<Image>() && child.GetComponentInChildren<Text>(true))
                    return child;
            return null;
        }

        /// <summary>仅在分辨率、教学头部或关卡尺寸变化时重排容器，保留子节点在场景中的布局。</summary>
        public void ApplyLayout(float width, float height, float scale, float appX, float appWidth,
            float top, float bottom, float stageY, float stageHeight, float boardX, float boardY, float boardWidth,
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
            float headHeight = HudHeight;
            SetBox(header, new Rect(appX + sidePadding * scale, top + topPadding * scale, headerWidth, 180));
            header.localScale = Vector3.one * scale;
            head.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, headHeight);
            head.anchoredPosition = new Vector2(head.anchoredPosition.x, -(toolbarHeight + sectionGap));
            float progressY = toolbarHeight + sectionGap + headHeight + sectionGap;
            SetBox(progressTrack, new Rect(4, progressY, headerWidth - 8, 5));
            SetBox(stage, new Rect(appX + sidePadding * scale, stageY, appWidth - sidePadding * 2 * scale, stageHeight));
            float barWidth = appWidth / scale - sidePadding * 2;
            SetBox(powerUpBar, new Rect(appX + sidePadding * scale, PowerUpTop(height, scale, bottom), barWidth, powerUpHeight));
            powerUpBar.localScale = Vector3.one * scale;
            PlacePowerUp(hintButton, 0, barWidth);
            PlacePowerUp(eraseButton, 1, barWidth);
            PlacePowerUp(extraMovesButton, 2, barWidth);
            if (erasePrompt)
            {
                SetBox(erasePrompt.rectTransform, new Rect(appX + sidePadding * scale,
                    PowerUpTop(height, scale, bottom) - 24 * scale, barWidth, 22));
                erasePrompt.transform.localScale = Vector3.one * scale;
            }
            PlaceCenter(boardFrame, new Vector2(boardX + boardWidth * .5f, boardY + boardHeight * .5f),
                new Vector2(boardWidth + 16 * scale, boardHeight + 16 * scale));
            boardSurface.rectTransform.sizeDelta = effectsSurface.rectTransform.sizeDelta = new Vector2(width, height);
            best.gameObject.SetActive(!teaching);
            resultGroup.transform.localScale = tutorialGroup.transform.localScale = Vector3.one * scale;
            float overlayWidth = (appWidth / scale - 32) / 1.4f;
            ((RectTransform)comboGroup.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, overlayWidth);
            ((RectTransform)milestoneGroup.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, overlayWidth);
            CenterOverlay(combo, overlayWidth);
            CenterOverlay(milestoneNumber, overlayWidth);
            CenterOverlay(milestoneCaption, overlayWidth);
            Shader.SetGlobalFloat("_HueBlurPixels", backdropBlur * scale * physicalScale);
        }

        /// <summary>更新既有 HUD 节点；不再每帧生成固定背景、按钮和标签网格。</summary>
        public void SetHud(string scoreValue, Color scoreColor, float scoreScale, float scoreWidth,
            string bestValue, string titleValue, string movesValue, Color movesColor,
            float fill, bool teaching, float movesWidth, int cleared, int total, bool complete)
        {
            score.text = scoreValue;
            score.color = scoreColor;
            float pop = Mathf.Min(scoreScale, 1.12f);
            FitHudLabel(score);
            score.rectTransform.localScale = scoreRestScale * pop;
            best.text = bestValue;
            FitHudLabel(best);
            levelTitle.text = titleValue;
            FitHudLabel(levelTitle);
            moves.text = movesValue;
            moves.color = movesColor;
            FitHudLabel(moves);
            Vector2 maximum = progressFill.anchorMax;
            maximum.x = Mathf.Clamp01(fill);
            progressFill.anchorMax = maximum;
            if (progressCaption) SetActive(progressCaption.gameObject, false);
            if (shownComplete != complete)
            {
                shownComplete = complete;
                progressFill.GetComponent<Image>().color = new Color(.20f, .72f, .55f);
            }
        }

        // HUD geometry and alignment are authored in the scene. Only shrink text
        // when a changing score/move count exceeds its existing text rectangle.
        private void FitHudLabel(Text label)
        {
            if (!hudFontSizes.TryGetValue(label, out var maximum))
            {
                maximum = label.fontSize;
                hudFontSizes.Add(label, maximum);
            }
            FitLabel(label, label.rectTransform.rect.width, maximum);
        }

        public Rect HudBounds(RectTransform target)
        {
            target.GetWorldCorners(hudCorners);
            var canvasRect = (RectTransform)canvas.transform;
            Vector3 bottomLeft = canvasRect.InverseTransformPoint(hudCorners[0]);
            Vector3 topRight = canvasRect.InverseTransformPoint(hudCorners[2]);
            return new Rect(bottomLeft.x - canvasRect.rect.xMin, canvasRect.rect.yMax - topRight.y,
                topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
        }

        public float PowerUpTop(float height, float scale, float bottom)
        {
            return height - bottom - (powerUpHeight + powerUpBottomPadding) * scale;
        }

        private static void PlacePowerUp(Button button, int index, float width)
        {
            SetBox((RectTransform)button.transform, new Rect((index + .5f) * width / 3 - 42, 2, 84, 72));
        }

        public void HideErasePresentation()
        {
            if (eraseCursor) SetActive(eraseCursor.gameObject, false);
            if (erasePrompt) SetActive(erasePrompt.gameObject, false);
        }

        public void SetErasePresentation(bool selecting, bool erasing, Vector2 position, float progress, float scale)
        {
            if ((selecting || erasing) && (!eraseCursor || !erasePrompt || !eraseDust || !eraseLabel))
                ResolvePowerUpBindings();
            if (erasePrompt) SetActive(erasePrompt.gameObject, selecting);
            if (!eraseCursor) return;
            SetActive(eraseCursor.gameObject, erasing);
            if (!erasing) return;
            var rect = eraseCursor.rectTransform;
            Vector2 size = Vector2.one * eraserSize;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
            float tipOffset = eraserSize * .25f;
            rect.anchoredPosition = new Vector2(position.x + tipOffset * scale, -position.y + tipOffset * scale);
            rect.localScale = Vector3.one * scale;
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(progress * 36) * 9);
            float alpha = Mathf.Clamp01(progress * 12) * Mathf.Clamp01((1 - progress) * 10);
            eraseCursor.color = new Color(1, 1, 1, alpha);
            if (eraseDust)
            {
                var dustRect = eraseDust.rectTransform;
                Vector2 dustSize = Vector2.one * eraseDustSize;
                if (dustRect.sizeDelta != dustSize) dustRect.sizeDelta = dustSize;
                dustRect.anchoredPosition = new Vector2(-tipOffset, -tipOffset);
                int frame = Mathf.Min(7, Mathf.FloorToInt(progress * 8));
                eraseDust.uvRect = new Rect((frame % 4) * .25f, 1 - (frame / 4 + 1) * .5f, .25f, .5f);
                Color tint = eraseDustTint;
                tint.a *= alpha * eraseDustOpacity;
                eraseDust.color = tint;
            }
        }

        public void SetPowerUps(int hints, int erasers, int extraMoves, bool canHint, bool canErase, bool canAddMoves, bool choosingErase)
        {
            if (shownHintCount != hints) { shownHintCount = hints; hintCount.text = hints.ToString(); }
            if (shownEraseCount != erasers) { shownEraseCount = erasers; eraseCount.text = erasers.ToString(); }
            if (shownExtraMovesCount != extraMoves) { shownExtraMovesCount = extraMoves; extraMovesCount.text = extraMoves.ToString(); }
            hintButton.interactable = canHint;
            eraseButton.interactable = canErase;
            extraMovesButton.interactable = canAddMoves;
            if (eraseLabel) eraseLabel.text = choosingErase ? "Cancel" : "Erase";
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
        public void SetTutorial(string text, int step, float scale, float easing)
        {
            SetActive(tutorialGroup.gameObject, true);
            tutorialGroup.alpha = Mathf.Clamp01(easing);
            bool hasButton = step >= 3;
            float buttonWidth = step == 5 ? 76 : 72;
            float width = stage.rect.width / scale - 20;
            float textWidth = width - 36 - (hasButton ? 12 + buttonWidth : 0);
            if (measuredTutorial != text || measuredTutorialWidth != textWidth)
            {
                measuredTutorial = text;
                measuredTutorialWidth = textWidth;
                tutorialText.horizontalOverflow = HorizontalWrapMode.Wrap;
                tutorialText.verticalOverflow = VerticalWrapMode.Overflow;
                tutorialText.rectTransform.sizeDelta = new Vector2(textWidth, 200);
                // Exported Text can retain its original unwrapped mesh width after
                // a resize. Explicit word breaks keep both renderers in agreement.
                tutorialText.text = WrapWords(tutorialText, text, textWidth - 2);
                tutorialTextHeight = CardTextHeight(tutorialText);
            }
            float height = 32 + Mathf.Max(tutorialTextHeight, hasButton ? 38 : 0);
            RectTransform card = (RectTransform)tutorialGroup.transform;
            card.sizeDelta = new Vector2(width, height);
            card.anchoredPosition = new Vector2(0, 10 * scale - (1 - easing) * 40 * scale);
            card.localScale = Vector3.one * scale * Mathf.Min(1, Mathf.Max(.1f, stage.rect.height / scale - 20) / height);
            tutorialText.rectTransform.sizeDelta = new Vector2(textWidth, tutorialTextHeight);
            tutorialText.rectTransform.anchoredPosition = new Vector2(tutorialText.rectTransform.anchoredPosition.x,
                -16 - (Mathf.Max(tutorialTextHeight, hasButton ? 38 : 0) - tutorialTextHeight) * .5f);
            SetActive(tutorialNext.gameObject, hasButton);
            tutorialNext.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, buttonWidth);
            tutorialNextText.text = step == 5 ? "Play" : "Next";
            FitLabel(tutorialNextText, buttonWidth - 16, 15);
        }

        /// <summary>居中呈现结算卡片，轻微上浮淡入；胜利保留明亮棋盘背景。</summary>
        public void SetResult(bool win, bool newRecord, float scale, float easing, string title, string number,
            string detail, string primary, string secondary)
        {
            SetActive(resultBackdrop.gameObject, true);
            SetActive(resultGroup.gameObject, true);
            resultGroup.alpha = Mathf.Clamp01(easing);
            resultGroup.interactable = easing > .9f;
            // Keep the input barrier, without a visible rectangle below the HUD.
            resultBackdrop.material = null;
            resultBackdrop.color = new Color(.94f, .96f, 1, win ? 0 : .65f * easing);
            float width = Mathf.Min(340, stage.rect.width / scale - 16);
            float detailWidth = width - 48;
            resultDetail.fontSize = 13;
            if (measuredDetail != detail || measuredDetailWidth != detailWidth)
            {
                measuredDetail = detail;
                measuredDetailWidth = detailWidth;
                resultDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
                resultDetail.verticalOverflow = VerticalWrapMode.Overflow;
                resultDetail.text = detail;
                resultDetail.rectTransform.sizeDelta = new Vector2(detailWidth, 200);
                resultDetailHeight = CardTextHeight(resultDetail);
            }
            float detailHeight = resultDetailHeight;
            RectTransform card = (RectTransform)resultGroup.transform;
            float height = (win ? 370 : 250) + detailHeight;
            card.sizeDelta = new Vector2(width, height);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.anchoredPosition = new Vector2(0, -(1 - easing) * 18 * scale);
            card.localScale = Vector3.one * scale * Mathf.Min(1, Mathf.Max(.1f, stage.rect.height / scale - 24) / height)
                * Mathf.Lerp(.97f, 1, easing);
            resultTitle.text = title;
            resultScore.text = number;
            resultDetail.text = detail;
            resultPrimaryText.text = primary;
            resultSecondaryText.text = secondary;
            FitLabel(resultTitle, detailWidth, 22);
            FitLabel(resultScore, detailWidth, 68);
            FitLabel(resultPrimaryText, detailWidth - 32, 17);
            FitLabel(resultSecondaryText, detailWidth - 32, 17);
            if (resultEmblem)
            {
                SetActive(resultEmblem.gameObject, win);
                SetResultBox(resultEmblem.rectTransform, width * .5f - 26, 24, 52, 52);
            }
            SetResultBox(resultTitle.rectTransform, 24, win ? 90 : 28, detailWidth, 32);
            SetResultBox(resultScore.rectTransform, 24, win ? 128 : 70, detailWidth, 76);
            SetResultBox(resultDetail.rectTransform, 24, win ? 210 : 150, detailWidth, detailHeight);
            resultDetail.color = newRecord && win ? new Color(.12f, .58f, .43f) : new Color(.46f, .49f, .61f);
            SetResultBox(resultButtons, 24, height - (win ? 132 : 74), detailWidth, win ? 110 : 50);
            SetActive(resultSecondary.gameObject, win);
        }

        private static void SetResultBox(RectTransform target, float x, float y, float width, float height)
        {
            SetBox(target, new Rect(x, y, width, height));
        }

        // Measure only when content, font or available width changes. Animation
        // frames reuse the fitted size instead of regenerating text for measurement.
        private void FitLabel(Text label, float width, int maximum)
        {
            width = Mathf.Max(1, width);
            TextFit fit;
            if (textFits.TryGetValue(label, out fit) && fit.text == label.text && fit.width == width
                && fit.maximum == maximum && fit.font == label.font)
                return;
            if (fit == null) { fit = new TextFit(); textFits.Add(label, fit); }
            fit.text = label.text; fit.width = width; fit.maximum = maximum; fit.font = label.font;
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.fontSize = maximum;
            float preferred = label.preferredWidth;
            if (preferred > width - 2)
                label.fontSize = Mathf.Max(1, Mathf.FloorToInt(maximum * Mathf.Max(1, width - 2) / preferred));
        }

        private static void CenterOverlay(Text label, float width)
        {
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, rect.anchoredPosition.y);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private static string WrapWords(Text label, string text, float width)
        {
            var wrapped = new System.Text.StringBuilder();
            string line = "";
            foreach (string word in text.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                label.text = candidate;
                if (line.Length > 0 && label.preferredWidth > width)
                {
                    wrapped.Append(line).Append('\n');
                    line = word;
                }
                else line = candidate;
            }
            return wrapped.Append(line).ToString();
        }

        private static float CardTextHeight(Text label)
        {
            int lines = 1;
            foreach (char c in label.text)
                if (c == '\n') lines++;
            // Playworks preferredHeight omits the authored 1.35 line spacing.
            // Reserve a full line box per explicit line, including font metrics
            // and padding, so vertical overflow never escapes the growing card.
            float measured = label.preferredHeight * 1.35f;
            float minimum = lines * label.fontSize * 1.7f;
            return Mathf.Ceil(Mathf.Max(measured, minimum)) + 6;
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
            HideErasePresentation();
            victoryFlash.color = new Color(1, 1, 1, 0);
        }

        /// <summary>固定 UI 的矩形占用区域用于隔离棋盘输入，不依赖上一帧 EventSystem 的指针状态。</summary>
        public bool BlocksBoardPointer(Vector2 logicalPosition, float physicalScale)
        {
            Vector2 screen = new Vector2(logicalPosition.x * physicalScale, Screen.height - logicalPosition.y * physicalScale);
            if (RectTransformUtility.RectangleContainsScreenPoint(powerUpBar, screen))
                return true;
            if (resultGroup.gameObject.activeSelf)
                return true;
            if (tutorialGroup.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)tutorialGroup.transform, screen))
                return true;
            return RectTransformUtility.RectangleContainsScreenPoint(retryButton.transform as RectTransform, screen)
                || RectTransformUtility.RectangleContainsScreenPoint(helpButton.transform as RectTransform, screen);
        }

        private void OnEnable()
        {
            if (score) scoreRestScale = score.rectTransform.localScale;
            hudFontSizes.Clear();
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
            float stageY = (topPadding + toolbarHeight + sectionGap + HudHeight + sectionGap + ProgressLayoutHeight) * scale;
            float stageHeight = Mathf.Max(1, PowerUpTop(height, scale, 0) - powerUpBoardGap * scale - stageY);
            float cell = Mathf.Max(1, Mathf.Min(64 * scale, Mathf.Floor((appWidth - 60 * scale) / 5), Mathf.Floor((stageHeight - 48 * scale) / 6)));
            ApplyLayout(width, height, scale, (width - appWidth) * .5f, appWidth, 0, 0, stageY, stageHeight,
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
