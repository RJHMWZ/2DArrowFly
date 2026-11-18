using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class IvySDKConfigWindow : EditorWindow
{
    private int tabIndex = 0;
    private string[] tabs = { "微信", "抖音", "Tiktok" };

    private IvySDKConfig config;

    [MenuItem("IvySDK/Config")]
    public static void ShowWindow()
    {
        GetWindow<IvySDKConfigWindow>("Ivy SDK Config");
    }

    private void OnEnable()
    {
        LoadOrCreateConfig();
    }

    void LoadOrCreateConfig()
    {
        string path = "Assets/IvyMiniGame/IvySDK/Resources/IvySDKConfig.asset";

        config = AssetDatabase.LoadAssetAtPath<IvySDKConfig>(path);

        if (config == null)
        {
            config = CreateInstance<IvySDKConfig>();

            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
        }
    }

    private void OnGUI()
    {
        if (config == null) return;

        tabIndex = GUILayout.Toolbar(tabIndex, tabs);
        GUILayout.Space(10);

        EditorGUI.BeginChangeCheck();

        if (tabIndex == 0)
        {
            DrawPlatform(config.wx, new List<IvySDKConfig.PlatformConfig> { config.dy, config.tt}, "IVY_SDK_WX", new List<string> { "IVY_SDK_DY", "IVY_SDK_TT" }, "微信");
        }
        else if (tabIndex == 1)
        {
            DrawPlatform(config.dy, new List<IvySDKConfig.PlatformConfig> { config.wx, config.tt }, "IVY_SDK_DY", new List<string> { "IVY_SDK_WX", "IVY_SDK_TT" }, "抖音");
        } else 
        {
            DrawPlatform(config.tt, new List<IvySDKConfig.PlatformConfig> { config.wx, config.dy }, "IVY_SDK_TT", new List<string> { "IVY_SDK_WX", "IVY_SDK_DY" }, "Toktok");
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }
    }

    void DrawPlatform(IvySDKConfig.PlatformConfig p,
                      List<IvySDKConfig.PlatformConfig> other,
                      string define,
                      List<string> otherDefine,
                      string label)
    {
        EditorGUI.BeginChangeCheck();
        bool newEnable = EditorGUILayout.Toggle($"激活（{label}）", p.enable);
        if (EditorGUI.EndChangeCheck())
        {
            p.enable = newEnable;

            if (p.enable)
            {
                //  互斥逻辑
                foreach (var item in other)
                {
                    item.enable = false;
                }

                ToggleDefine(define, true);
                foreach (string item in otherDefine)
                {
                    ToggleDefine(item, false);
                }
            }
            else
            {
                ToggleDefine(define, false);
            }

            EditorUtility.SetDirty(config);
        }

        GUI.enabled = p.enable;

        p.debug = EditorGUILayout.Toggle("Debug", p.debug);


        p.appName = EditorGUILayout.TextField("App Name", p.appName);
        p.appId = EditorGUILayout.TextField("App ID", p.appId);
        p.channel = EditorGUILayout.TextField("Channel", p.channel);
        p.versionCode = EditorGUILayout.TextField("Version Code", p.versionCode);
        p.versionName = EditorGUILayout.TextField("Version Name", p.versionName);
        p.packageName = EditorGUILayout.TextField("Package Name", p.packageName);

        if (define.Equals("IVY_SDK_WX"))
        {
            p.miMasterAppId = EditorGUILayout.TextField("米大师app key", p.miMasterAppId);
        }

        p.gravityAccessToken = EditorGUILayout.TextField("引力Access Token", p.gravityAccessToken);

        DrawBannerAdsList(p);
        DrawInterstitialAdsList(p);
        DrawRewardedAdsList(p);

        GUI.enabled = true;
    }

    void DrawBannerAdsList(IvySDKConfig.PlatformConfig p)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Banner广告配置", EditorStyles.boldLabel);

        if (p.bannerAds == null)
            p.bannerAds = new List<AdConfig>();

        for (int i = 0; i < p.bannerAds.Count; i++)
        {
            var ad = p.bannerAds[i];

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Ad {i}", EditorStyles.boldLabel);

            if (GUILayout.Button("删除", GUILayout.Width(60)))
            {
                p.bannerAds.RemoveAt(i);
            }
            EditorGUILayout.EndHorizontal();

            ad.tag = EditorGUILayout.TextField("Tag", ad.tag);
            ad.adUnitId = EditorGUILayout.TextField("Ad Unit Id", ad.adUnitId);
            ad.adIntervals = EditorGUILayout.IntField("Interval", ad.adIntervals);

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("➕ 添加Banner广告"))
        {
            p.bannerAds.Add(new AdConfig()
            {
                tag = "main",
                adUnitId = "",
                adIntervals = 0
            });
        }
    }

    void DrawInterstitialAdsList(IvySDKConfig.PlatformConfig p)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("插屏广告配置", EditorStyles.boldLabel);

        if (p.interstitialAds == null)
            p.interstitialAds = new List<AdConfig>();

        for (int i = 0; i < p.interstitialAds.Count; i++)
        {
            var ad = p.interstitialAds[i];

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Ad {i}", EditorStyles.boldLabel);

            if (GUILayout.Button("删除", GUILayout.Width(60)))
            {
                p.interstitialAds.RemoveAt(i);
            }
            EditorGUILayout.EndHorizontal();

            ad.tag = EditorGUILayout.TextField("Tag", ad.tag);
            ad.adUnitId = EditorGUILayout.TextField("Ad Unit Id", ad.adUnitId);
            ad.adIntervals = EditorGUILayout.IntField("Interval", ad.adIntervals);

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("➕ 添加插屏广告"))
        {
            p.interstitialAds.Add(new AdConfig()
            {
                tag = "main",
                adUnitId = "",
                adIntervals = 0
            });
        }
    }

    void DrawRewardedAdsList(IvySDKConfig.PlatformConfig p)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("激励视频广告配置", EditorStyles.boldLabel);

        if (p.rewardedAds == null)
            p.rewardedAds = new List<AdConfig>();

        for (int i = 0; i < p.rewardedAds.Count; i++)
        {
            var ad = p.rewardedAds[i];

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Ad {i}", EditorStyles.boldLabel);

            if (GUILayout.Button("删除", GUILayout.Width(60)))
            {
                p.rewardedAds.RemoveAt(i);
            }
            EditorGUILayout.EndHorizontal();

            ad.tag = EditorGUILayout.TextField("Tag", ad.tag);
            ad.adUnitId = EditorGUILayout.TextField("Ad Unit Id", ad.adUnitId);
            ad.adIntervals = EditorGUILayout.IntField("Interval", ad.adIntervals);

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("➕ 添加激励视频广告"))
        {
            p.rewardedAds.Add(new AdConfig()
            {
                tag = "main",
                adUnitId = "",
                adIntervals = 0
            });
        }
    }


    void ToggleDefine(string define, bool enable)
    {
        var target = EditorUserBuildSettings.selectedBuildTargetGroup;
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);

        if (enable)
        {
            if (!defines.Contains(define))
                defines += ";" + define;
        }
        else
        {
            defines = defines.Replace(define, "");
        }

        PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
    }
}