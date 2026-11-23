using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HueRelay.Editor
{
    public static class HueAdTools
    {
        public const string PolicyPath = "Assets/HueRelay/Resources/HueRelay/Data/AdPolicy.asset";
        [MenuItem("Tools/Hue Relay/Ads/Set up ad strategy")]
        public static void Setup()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before authoring the ad strategy.");
            var game = UnityEngine.Object.FindObjectOfType<HueGame>();
            if (!game) throw new InvalidOperationException("Open the HueRelay scene first.");
            var config = AssetDatabase.LoadAssetAtPath<HueAdPolicy>(PolicyPath);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<HueAdPolicy>();
                AssetDatabase.CreateAsset(config, PolicyPath);
            }
            var ads = game.GetComponent<HueAds>();
            if (!ads) ads = game.gameObject.AddComponent<HueAds>();
            ads.game = game; ads.policy = config;
            EditorUtility.SetDirty(ads);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log("Ad strategy configured. Placements start empty and ads remain disabled until explicitly enabled.");
        }
    }
}
