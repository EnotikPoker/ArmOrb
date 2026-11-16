using System;
using System.IO;
using ArmOrb;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ArmOrb.Editor
{
    public static class ProjectBuilder
    {
        const string Root = "Assets/ArmOrb/";
        const string ScenePath = Root + "Scenes/Main.unity";
        static readonly Color Ink = new Color32(48, 49, 56, 255);

        [MenuItem("ArmOrb/Rebuild starter scene and assets")]
        public static void CreateProject()
        {
            Directory.CreateDirectory(Root + "Scenes"); Directory.CreateDirectory(Root + "Prefabs");
            Directory.CreateDirectory(Root + "Settings"); AssetDatabase.Refresh();
            ConfigureTextures();
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "Art/Fonts/DejaVuSans.ttf");
            var arena = GetOrCreate<ArenaSettings>(Root + "Settings/Arena.asset");
            var definitions = new WeaponDefinition[5];
            string[] names = { "Меч", "Копьё", "Клинок", "Коса", "Щит" };
            string[] descriptions = { "Удары увеличивают урон", "Удары увеличивают длину", "Удары ускоряют вращение", "Удары накапливают яд", "Блоки усиливают возврат" };
            for (int i = 0; i < 5; i++)
            {
                string kind = ((WeaponKind)i).ToString();
                var definition = GetOrCreate<WeaponDefinition>(Root + "Settings/" + kind + ".asset");
                definition.kind = (WeaponKind)i; definition.displayName = names[i]; definition.description = descriptions[i];
                definition.damage = 1; definition.reach = i == 1 ? 3 : 2.6f; definition.rotationSpeed = 180;
                definition.growth = i switch { 1 => 0.5f, 2 => 30, 4 => 0.25f, _ => 1 };
                definition.initialReflection = 1; definition.sprite = Sprite(kind);
                var go = new GameObject(kind); var view = go.AddComponent<WeaponView>();
                view.artwork = go.AddComponent<SpriteRenderer>(); view.artwork.sprite = definition.sprite; view.artwork.sortingOrder = 4;
                if (i == 1)
                {
                    view.artwork.enabled = false;
                    view.spearShaft = SpriteObject("Shaft", Sprite("SpearShaft"), go.transform, 4);
                    view.spearHead = SpriteObject("Head", Sprite("SpearHead"), go.transform, 4);
                }
                definition.prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "Prefabs/" + kind + ".prefab").GetComponent<WeaponView>();
                UnityEngine.Object.DestroyImmediate(go); EditorUtility.SetDirty(definition); definitions[i] = definition;
            }
            var orbGo = new GameObject("Orb"); var orb = orbGo.AddComponent<OrbView>();
            orb.shadow = SpriteObject("Shadow", Sprite("Disc"), orbGo.transform, 1);
            orb.shadow.color = new Color(0.1f, 0.1f, 0.15f, 0.09f); orb.shadow.transform.localPosition = new Vector3(0.03f, -0.09f, 0);
            orb.body = SpriteObject("Body", Sprite("Orb"), orbGo.transform, 5);
            var healthGo = new GameObject("Health"); healthGo.transform.SetParent(orbGo.transform);
            orb.healthLabel = healthGo.AddComponent<TextMesh>(); orb.healthLabel.font = font;
            orb.healthLabel.fontSize = 64; orb.healthLabel.characterSize = 0.09f;
            orb.healthLabel.anchor = TextAnchor.MiddleCenter; orb.healthLabel.alignment = TextAlignment.Center;
            orb.healthLabel.color = Ink; orb.healthLabel.text = "100";
            var hr = orb.healthLabel.GetComponent<MeshRenderer>(); hr.sharedMaterial = font.material; hr.sortingOrder = 10;
            var orbPrefab = PrefabUtility.SaveAsPrefabAsset(orbGo, Root + "Prefabs/Orb.prefab").GetComponent<OrbView>();
            UnityEngine.Object.DestroyImmediate(orbGo);

            var panel = GetOrCreate<PanelSettings>(Root + "UI/PanelSettings.asset");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution = new Vector2Int(720, 1280);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; panel.match = 0;
            var textSettings = GetOrCreate<PanelTextSettings>(Root + "UI/TextSettings.asset");
            panel.textSettings = textSettings; EditorUtility.SetDirty(panel);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Arena Camera"); var camera = cameraGo.AddComponent<Camera>();
            cameraGo.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -20);
            camera.orthographic = true; camera.orthographicSize = arena.size / 2 + 0.5f;
            camera.backgroundColor = new Color32(255, 252, 247, 255); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 50;
            cameraGo.AddComponent<AudioListener>();
            BuildArena(arena.size);
            var gameGo = new GameObject("ArmOrb Game"); var game = gameGo.AddComponent<ArmOrbGame>();
            game.settings = arena; game.weapons = definitions; game.orbPrefab = orbPrefab;
            game.arenaCamera = camera; game.uiFont = font;
            game.styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "UI/ArmOrb.uss");
            game.setupIllustration = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/SetupIllustration.png");
            game.document = gameGo.AddComponent<UIDocument>(); game.document.panelSettings = panel;
            game.feedback = gameGo.AddComponent<CombatFeedback>(); game.feedback.disc = Sprite("Disc");
            game.feedback.font = font; game.feedback.audioSource = gameGo.AddComponent<AudioSource>();
            game.feedback.audioSource.playOnAwake = false;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigurePlayer(); AssetDatabase.SaveAssets();
            Debug.Log("ArmOrb starter scene and all prefabs are ready.");
        }

        static void ConfigureTextures()
        {
            foreach (string path in Directory.GetFiles(Root + "Art", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 128; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                string name = Path.GetFileNameWithoutExtension(path);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = name is "Orb" or "Disc" or "AppIcon" or "SetupIllustration" ? new Vector2(0.5f, 0.5f) : new Vector2(0, 0.5f);
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
        }
        static void BuildArena(float size)
        {
            var root = new GameObject("Arena — square, no gravity");
            var material = new Material(Shader.Find("Sprites/Default"));
            const string materialPath = Root + "Art/ArenaLines.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing) { UnityEngine.Object.DestroyImmediate(material); material = existing; }
            else AssetDatabase.CreateAsset(material, materialPath);
            float h = size / 2;
            var borderGo = new GameObject("Boundary"); borderGo.transform.SetParent(root.transform);
            var line = borderGo.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = false; line.positionCount = 4; line.loop = true; line.widthMultiplier = 0.035f;
            line.SetPositions(new[] { new Vector3(-h, -h), new Vector3(-h, h), new Vector3(h, h), new Vector3(h, -h) });
            line.startColor = line.endColor = Ink; line.sortingOrder = 0;
            for (float x = -h + 1; x < h; x += 1)
                for (float y = -h + 1; y < h; y += 1)
                {
                    var dot = SpriteObject("Grid dot", Sprite("Disc"), root.transform, -1);
                    dot.transform.position = new Vector3(x, y); dot.transform.localScale = Vector3.one * 0.028f;
                    dot.color = new Color32(211, 205, 193, 110);
                }
        }
        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "EnotikPoker"; PlayerSettings.productName = "ArmOrb";
            PlayerSettings.bundleVersion = "0.1.0"; PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.enotikpoker.armorb");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false; PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultScreenWidth = 720; PlayerSettings.defaultScreenHeight = 1280;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/AppIcon.png") }, IconKind.Any);
        }
        static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
        static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/" + name + ".png");
        static SpriteRenderer SpriteObject(string name, Sprite sprite, Transform parent, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order; return renderer;
        }
        [MenuItem("ArmOrb/Build Android APK")]
        public static void BuildAndroid()
        {
            ConfigurePlayer();
            Directory.CreateDirectory("Builds/Android"); EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Android/ArmOrb-v0.1.0.apk",
                target = BuildTarget.Android, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed: " + report.summary.result);
            Debug.Log($"ArmOrb APK built: {report.summary.totalSize} bytes, {report.summary.totalTime}");
        }
    }
}
