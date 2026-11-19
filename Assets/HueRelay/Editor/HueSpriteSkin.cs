using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HueRelay.Editor
{
    /// <summary>Authoring tool only: import PNG sprites and bind saved Image components.</summary>
    public static class HueSpriteSkin
    {
        const string Art = "Assets/HueRelay/Art/UI/";

        static void Import(string file, params SpriteMetaData[] slices)
        {
            string path = Art + file + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = file == "Background" ? 32 : 128;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Reuse the generated button's clean alpha as a neutral white UI
            // sprite via Unity's stock import channel settings, without a shader
            // or runtime pixel generation. The compact source is shared in style.
            if (file == "Surface")
            {
                importer.swizzleR = TextureImporterSwizzle.One;
                importer.swizzleG = TextureImporterSwizzle.One;
                importer.swizzleB = TextureImporterSwizzle.One;
            }
            importer.spritesheet = slices;
            importer.SaveAndReimport();
        }

        static SpriteMetaData Slice(string name, Rect rect, float border = 0) => new SpriteMetaData
        {
            name = name, rect = rect, pivot = new Vector2(.5f,.5f), alignment = 9,
            border = Vector4.one * border
        };

        static Sprite Sprite(string file, string name) => AssetDatabase.LoadAllAssetsAtPath(Art + file + ".png")
            .OfType<Sprite>().First(s => s.name == name);

        [MenuItem("Tools/Hue Relay/应用 PNG 界面素材（保留位置）")]
        public static void Apply()
        {
            Import("Surface", Slice("Panel",new Rect(8,8,112,112),26), Slice("Solid",new Rect(63,63,2,2)));
            Import("PrimaryButton", Slice("Button",new Rect(8,8,112,112),26));
            Import("SuccessBadge", Slice("Success",new Rect(9,9,110,110)));
            Import("Background", Slice("Background",new Rect(0,0,4,4)));
            var panel = Sprite("Surface","Panel");
            var solid = Sprite("Surface","Solid");
            var button = Sprite("PrimaryButton","Button");
            var success = Sprite("SuccessBadge","Success");
            var background = Sprite("Background","Background");
            var view = UnityEngine.Object.FindObjectOfType<HueView>(true);
            if (!view) throw new Exception("Open the HueRelay scene before applying its PNG skin.");
            string[] panels = { "HelpButton", "VictoryFlash", "TutorialPanel", "CountBadge", "ResultEmblem",
                "TutorialBackdrop", "ProgressFill", "ResultBackdrop", "ResultRetryButton", "HintPowerUp",
                "TutorialNextButton", "MovesChip", "ProgressTrack", "RetryButton", "ResultPrimaryButton",
                "ScoreChip", "BoardBackground", "ErasePowerUp", "ExtraMovesPowerUp" };
            foreach (var image in view.canvas.GetComponentsInChildren<Image>(true).Where(i => panels.Contains(i.name)))
            {
                Undo.RecordObject(image,"Apply PNG UI sprite");
                image.material = null;
                image.preserveAspect = false;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = .8f;
                image.sprite = panel;
                switch (image.name)
                {
                    case "VictoryFlash": case "ResultBackdrop": case "ProgressTrack":
                        image.sprite = solid; image.type = Image.Type.Simple; break;
                    case "ProgressFill":
                        image.sprite = solid; image.type = Image.Type.Simple;
                        image.color = new Color(.20f, .72f, .55f); break;
                    case "ResultEmblem":
                        image.sprite = success; image.type = Image.Type.Simple; image.preserveAspect = true;
                        image.color = Color.white; break;
                    case "CountBadge":
                        image.sprite = button; image.type = Image.Type.Simple; image.preserveAspect = true;
                        image.color = Color.white; break;
                    case "ResultRetryButton":
                        image.color = new Color(.90f, .91f, .94f); image.enabled = true;
                        view.resultSecondary.targetGraphic = image;
                        view.resultSecondaryText.color = new Color(.38f, .41f, .51f); break;
                    case "ResultPrimaryButton":
                        image.color = new Color(.29f, .40f, .97f); break;
                    case "TutorialNextButton":
                        image.sprite = button; image.color = Color.white; break;
                    default: image.color = Color.white; break;
                }
                EditorUtility.SetDirty(image);
            }
            view.MatchResultButtonStyle();
            var config = UnityEngine.Object.FindObjectOfType<GameBackgroundConfig>(true);
            if (config)
            {
                Undo.RecordObject(config,"Apply background PNG");
                config.BackgroundTexture = null;
                config.GetComponent<Image>().sprite = background;
                config.GetComponent<Image>().color = Color.white;
                EditorUtility.SetDirty(config);
            }
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        public static void BatchApply()
        {
            try
            {
                EditorSceneManager.OpenScene(HueMiniGameTools.ScenePath);
                Apply();
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Directory.CreateDirectory("Logs/SpriteSkin");
                File.WriteAllText("Logs/SpriteSkin/migration.txt","PASS: fixed UI uses PNG-backed standard Images; nine-slice panels and buttons, success badge and background saved.\n");
                EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
