using System;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay
{
    /// <summary>绑定固定场景组件，并按原 HTML 的逻辑像素计算关卡布局。</summary>
    public sealed partial class HueGame
    {
        /// <summary>启动时只读取场景引用；不创建 Canvas、按钮、文字、材质或音频组件。</summary>
        private bool BindScene()
        {
            string reason = null;
            if (!sceneView || !audioBus || !sceneView.ValidateBindings(out reason))
            {
                Debug.LogError(reason ?? "HueGame 尚未绑定场景中的 HueView 或 HueAudio。", this);
                return false;
            }
            root = sceneView.canvas;
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
            pixelScale = Screen.width / 390f;
            viewWidth = 390;
            viewHeight = Screen.height / pixelScale;
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
            viewWidth = Screen.width / pixelScale;
            viewHeight = Screen.height / pixelScale;
#endif
            root.GetComponent<CanvasScaler>().scaleFactor = pixelScale;
            safeTop = (Screen.height - Screen.safeArea.yMax) / pixelScale;
            safeBottom = Screen.safeArea.yMin / pixelScale;
            K = Mathf.Clamp(Mathf.Min(viewWidth / 390, viewHeight / 800), .75f, 3.5f);
            appW = Mathf.Min(viewWidth, 460 * K);
            appX = (viewWidth - appW) * .5f;
            // 使用最终 CSS 覆盖后的布局：12px 内边距、44px 工具栏、14px 间距和 6px 进度条。
            float headY = safeTop + (sceneView.topPadding + sceneView.toolbarHeight + sceneView.sectionGap) * K;
            float headHeight = (sceneView.scoreLineHeight + sceneView.bestTopGap + (tut >= 0 ? 0 : sceneView.bestLineHeight)) * K;
            stageY = headY + headHeight + sceneView.sectionGap * K + sceneView.progressHeight * K;
            // 原页面的 800px 内容高度之外还包含上下各 12px 内边距，此处保留该实际布局。
            stageH = viewHeight + 12 * K - safeBottom - stageY;
            cs = Mathf.Max(16, Mathf.Min(64 * K, Mathf.Floor((appW - sceneView.sidePadding * 2 * K - 32 * K) / P.L.w), Mathf.Floor((stageH - 32 * K) / P.L.h)));
            ox = appX + appW * .5f - P.L.w * cs * .5f;
            oy = stageY + (stageH - P.L.h * cs) * .5f;
            scoreRect = new Rect(appX + 18 * K, headY, (scoreText == null ? 1 : scoreText.Length) * 24.376f * K, 41.8f * K);
            float chipW = (97.90625f + (Taps >= 10 ? 9.25f : 0) + (Limit >= 10 ? 9.25f : 0)) * K;
            chipRect = new Rect(appX + appW - 18 * K - chipW, headY + headHeight - 42 * K, chipW, 38 * K);
            sceneView.ApplyLayout(viewWidth, viewHeight, K, appX, appW, safeTop, stageY, stageH,
                ox, oy, P.L.w * cs, P.L.h * cs, tut >= 0, pixelScale);
        }
    }
}
