// Run from Assets/Editor in an isolated copy with graphics enabled.
using System;
using System.IO;
using System.Linq;
using HueRelay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class HueResultPresentationChecks
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/HueRelay/Scenes/HueRelay.unity");
            var menu = UnityEngine.Object.FindObjectOfType<HueMainMenu>();
            if (menu) menu.gameObject.SetActive(false);
            var view = UnityEngine.Object.FindObjectOfType<HueView>();
            Require(view.ValidateBindings(out var reason), reason);
            foreach (var file in new[] { "Surface", "PrimaryButton", "SuccessBadge", "Background" })
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HueRelay/Art/UI/" + file + ".png");
                int limit = file == "Background" ? 4 : 128;
                Require(texture && texture.width <= limit && texture.height <= limit,
                    "UI texture exceeds its compact size budget: " + file);
            }
            Require(view.canvas.GetComponentsInChildren<HuePanel>(true).Length == 0,
                "Saved UI still contains a procedural panel component.");
            foreach (var image in new[] { view.scoreChip, view.movesChip, view.boardBackground, view.resultEmblem,
                view.hintBadge, view.resultBackdrop, view.victoryFlash, view.resultPrimary.GetComponent<Image>() })
                Require(image && image.sprite && AssetDatabase.GetAssetPath(image.sprite).StartsWith("Assets/HueRelay/Art/UI/")
                    && image.material.shader.name == "UI/Default", "Fixed UI is not using its PNG sprite/default UI material.");
            Directory.CreateDirectory("Logs/ResultPresentation");
            foreach (var size in new[] { new Vector2Int(390,844), new Vector2Int(390,667), new Vector2Int(844,390) })
            {
                float width = size.x, height = size.y;
                float k = Mathf.Clamp(Mathf.Min(width / 390, height / 800), .75f, 3.5f);
                float appW = Mathf.Min(width, 460 * k);
                float y = (view.topPadding + view.TopBarHeight + view.sectionGap + view.HudHeight + view.sectionGap + view.ProgressLayoutHeight) * k;
                float h = view.PowerUpTop(height,k,0) - view.powerUpBoardGap*k-y;
                var rt = new RenderTexture(size.x*2, size.y*2, 24);
                var camera = new GameObject("PreviewCamera",typeof(Camera)).GetComponent<Camera>();
                camera.targetTexture = rt; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.965f,.973f,1);
                camera.transform.position = new Vector3(0,0,-100);
                camera.orthographic = true; camera.orthographicSize = height/2;
                foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10; }
                view.canvas.GetComponent<CanvasScaler>().scaleFactor = 2;
                Canvas.ForceUpdateCanvases();
                view.ApplyLayout(width,height,k,(width-appW)/2,appW,0,0,y,h,width/2-100,y+h/2-120,200,240,false,2);
                var editable = new[] { view.scoreChip.rectTransform, view.score.rectTransform,
                    view.best.rectTransform, view.levelTitle.rectTransform, view.movesChip.rectTransform, view.moves.rectTransform };
                var positions = editable.Select(rect => rect.anchoredPosition).ToArray();
                var sizes = editable.Select(rect => rect.sizeDelta).ToArray();
                foreach (var rect in editable) rect.anchoredPosition += new Vector2(7,-3);
                view.SetHud("12345",Color.black,1,0,"Best 54321","Level 100","Moves 13/15",Color.black,.5f,false,0,6,12,false);
                view.ApplyLayout(width,height,k,(width-appW)/2,appW,0,0,y,h,width/2-100,y+h/2-120,200,240,false,2);
                for (int i=0;i<editable.Length;i++)
                {
                    Require(Vector2.Distance(editable[i].anchoredPosition,positions[i]+new Vector2(7,-3))<.01f
                        && Vector2.Distance(editable[i].sizeDelta,sizes[i])<.01f,"HUD update overwrote an authored transform.");
                    editable[i].anchoredPosition=positions[i];
                }
                view.boardBackground.gameObject.SetActive(false);
                foreach (bool win in new[] { true, false })
                {
                    view.HideTransient();
                    view.SetHud("420",new Color(.11f,.13f,.25f),1,75,"Best 420","Level 43","Moves 11/13",Color.black,win?1:.6f,false,115,win?12:7,12,win);
                    view.SetResult(win,true,k,1,win?"Level 43 complete!":"Out of moves","420",win?"NEW BEST":"5 arrows left  ·  Give it another try",win?"Next Level":"Retry","Home");
                    Canvas.ForceUpdateCanvases();
                    var progressColor = view.progressFill.GetComponent<Image>().color;
                    Require(progressColor.g > progressColor.r && progressColor.g > progressColor.b,
                        "Progress fill must stay green at partial and full progress.");
                    var retryBackground = view.resultSecondary.GetComponent<Image>();
                    Require(retryBackground && retryBackground.enabled && retryBackground.sprite == view.movesChip.sprite
                        && view.resultSecondary.targetGraphic == retryBackground,
                        "Retry must have an enabled neutral background across its button area.");
                    var primaryBackground = view.resultPrimary.GetComponent<Image>();
                    Require(view.resultRevive && view.resultRevive.gameObject.activeSelf == !win,
                        "Revive must be offered only on failure.");
                    if (!win)
                    {
                        var reviveBackground = view.resultRevive.GetComponent<Image>();
                        Require(primaryBackground.color == retryBackground.color
                            && view.resultPrimaryText.color == view.resultSecondaryText.color,
                            "Failure Retry must use the Home styling.");
                        Require(!view.resultDetail.gameObject.activeSelf, "Failure description must be hidden.");
                        Require(view.resultReviveText.text == "+3 Moves" && view.resultReviveIcon
                            && view.resultReviveIcon.gameObject.activeSelf && !view.resultReviveIcon.raycastTarget,
                            "Revive must use a video-ad icon and the compact +3 Moves label.");
                        Require(reviveBackground.color.b - reviveBackground.color.r > .2f
                            && reviveBackground.rectTransform.rect.height > primaryBackground.rectTransform.rect.height,
                            "Revive must be the prominent blue action.");
                        var card = (RectTransform)view.resultGroup.transform;
                        foreach (var button in new[] { view.resultRevive, view.resultPrimary, view.resultSecondary })
                        {
                            var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                            foreach (var corner in corners)
                                Require(card.rect.Contains((Vector2)card.InverseTransformPoint(corner)), "A result button escaped the card.");
                        }
                    }
                    Require(retryBackground.sprite == primaryBackground.sprite
                        && retryBackground.type == primaryBackground.type
                        && retryBackground.pixelsPerUnitMultiplier == primaryBackground.pixelsPerUnitMultiplier
                        && Vector2.Distance(retryBackground.rectTransform.rect.size,primaryBackground.rectTransform.rect.size)<.01f
                        && view.resultPrimaryText.font == view.resultSecondaryText.font
                        && view.resultPrimaryText.fontSize == view.resultSecondaryText.fontSize,
                        "Result buttons must share their artwork, dimensions and typography.");
                    Require(view.scoreChip && Mathf.Abs(view.scoreChip.rectTransform.rect.width - view.movesChip.rectTransform.rect.width) < .01f,
                        "Score and moves pills must have matching widths.");
                    Require(view.moves.rectTransform.rect.width <= view.movesChip.rectTransform.rect.width,
                        "Authored moves text must fit its pill.");
                    Require(Mathf.Abs(view.HudBounds(view.best.rectTransform).center.x - view.HudBounds(view.levelTitle.rectTransform).center.x) < .01f
                        && view.best.alignment == TextAnchor.MiddleCenter, "Best score must be centered under the level title.");
                    Require(view.resultBackdrop.color.a >= .5f && view.resultBackdrop.color.r < .15f
                        && view.resultBackdrop.color.g < .15f && view.resultBackdrop.color.b < .15f,
                        "Both results must use a translucent dark backdrop.");
                    var backdropBounds = view.HudBounds(view.resultBackdrop.rectTransform);
                    var canvasBounds = ((RectTransform)view.canvas.transform).rect;
                    Require(Mathf.Abs(backdropBounds.width - canvasBounds.width) < .1f
                        && Mathf.Abs(backdropBounds.height - canvasBounds.height) < .1f,
                        "Result backdrop must cover the whole game canvas.");
                    Require(view.resultPrimary.IsInteractable(),"Settled primary button must accept input.");
                    Require(view.resultSecondary.gameObject.activeSelf,"Home must be visible for both results.");
                    var detailRect = view.resultDetail.rectTransform;
                    Require(!win || -detailRect.anchoredPosition.y + detailRect.rect.height <= -view.resultButtons.anchoredPosition.y,"Detail overlaps buttons.");
                    Require(view.progressFill.anchorMax.x>=0 && view.progressFill.anchorMax.x<=1,"Fill escaped track.");
                    camera.Render();
                    var previous = RenderTexture.active; RenderTexture.active = rt;
                    var texture = new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply();
                    File.WriteAllBytes("Logs/ResultPresentation/"+size.x+"x"+size.y+(win?"-win":"-retry")+".png",texture.EncodeToPNG());
                    RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(texture);
                }
                UnityEngine.Object.DestroyImmediate(camera.gameObject); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
            File.WriteAllText("Logs/ResultPresentation/checks.txt","PASS: authored HUD positions/sizes preserved after content and layout updates; bindings, full-canvas dark victory/failure backdrops, button states, non-overlapping details, bounded progress; portrait, short portrait and landscape rendered.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
