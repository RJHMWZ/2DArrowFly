using System;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>绑定固定场景组件，并按原 HTML 的逻辑像素计算关卡布局。</summary>
    public sealed partial class HueGame
    {
        /// <summary>绑定场景界面和音频；旧场景的装饰节点由 HueView 兼容。</summary>
        private bool BindScene()
        {
            string reason = null;
            if (!sceneView || !audioBus || !sceneView.ValidateBindings(out reason))
            {
                Debug.LogError(reason ?? "HueGame requires scene references to HueView and HueAudio.", this);
                return false;
            }
            root = sceneView.canvas;
            rootScaler = root.GetComponent<CanvasScaler>();
            board = sceneView.boardSurface;
            fx = sceneView.effectsSurface;
            finger = sceneView.finger;
            sceneView.HideTransient();
            return true;
        }

        /// <summary>
        /// 按原 CSS 的尺寸、留白和安全区域计算布局；保留原页面内容高度与外侧内边距的实际关系。
        /// </summary>
        private void Layout()
        {
            LayoutViewport(Screen.width, Screen.height, Screen.safeArea);
        }

        private void LayoutViewport(float width, float height, Rect safeArea)
        {
            pixelScale = width > height ? height / 800f : width / 390f;
            viewWidth = width / pixelScale;
            viewHeight = height / pixelScale;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (deviceDensity <= 0)
            {
                try
                {
                    using (var p = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var a = p.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var r = a.Call<AndroidJavaObject>("getResources"))
                    using (var m = r.Call<AndroidJavaObject>("getDisplayMetrics"))
                    {
                        deviceDensity = m.Get<float>("density");
                    }
                }
                catch (Exception)
                {
                    deviceDensity = pixelScale;
                }
            }

            pixelScale = deviceDensity;
            viewWidth = width / pixelScale;
            viewHeight = height / pixelScale;
#endif
            if (!rootScaler) rootScaler = root.GetComponent<CanvasScaler>();
            rootScaler.scaleFactor = pixelScale;
            safeTop = (height - safeArea.yMax) / pixelScale;
            safeBottom = safeArea.yMin / pixelScale;
            K = Mathf.Clamp(Mathf.Min(viewWidth / 390, viewHeight / 800), .75f, 3.5f);
            appW = Mathf.Min(viewWidth, 460 * K);
            appX = (viewWidth - appW) * .5f;
            // 使用最终 CSS 覆盖后的布局：12px 内边距、44px 工具栏、14px 间距和 6px 进度条。
            float headY = safeTop + (sceneView.topPadding + sceneView.toolbarHeight + sceneView.sectionGap) * K;
            float headHeight = sceneView.HudHeight * K;
            stageY = headY + headHeight + sceneView.sectionGap * K + sceneView.ProgressLayoutHeight * K;
            // Keep the entire board above the item bar and the device's bottom safe area.
            stageH = Mathf.Max(1, sceneView.PowerUpTop(viewHeight, K, safeBottom) - sceneView.powerUpBoardGap * K - stageY);
            cs = Mathf.Max(1, Mathf.Min(64 * K, Mathf.Floor((appW - sceneView.sidePadding * 2 * K - 32 * K) / P.L.w), Mathf.Floor((stageH - 48 * K) / P.L.h)));
            ox = appX + appW * .5f - P.L.w * cs * .5f;
            oy = stageY + (stageH - P.L.h * cs) * .5f;
            sceneView.ApplyLayout(viewWidth, viewHeight, K, appX, appW, safeTop, safeBottom, stageY, stageH,
                ox, oy, P.L.w * cs, P.L.h * cs, tut >= 0, pixelScale);
            scoreRect = sceneView.HudBounds(sceneView.scoreChip ? sceneView.scoreChip.rectTransform : sceneView.score.rectTransform);
            chipRect = sceneView.HudBounds(sceneView.movesChip.rectTransform);
        }
    }
}
