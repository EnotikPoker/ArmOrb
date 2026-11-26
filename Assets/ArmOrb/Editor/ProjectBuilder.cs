using System;
using System.IO;
using System.Collections.Generic;
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
        static readonly Color Ink = new Color32(17, 17, 17, 255);

        [MenuItem("ArmOrb/Rebuild starter scene and assets")]
        public static void CreateProject()
        {
            Directory.CreateDirectory(Root + "Scenes"); Directory.CreateDirectory(Root + "Prefabs");
            Directory.CreateDirectory(Root + "Settings"); AssetDatabase.Refresh();
            Directory.CreateDirectory(Root + "Resources/WeaponShapes"); AssetDatabase.Refresh();
            ConfigureTextures();
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "Art/Fonts/DejaVuSans.ttf");
            var arena = GetOrCreate<ArenaSettings>(Root + "Settings/Arena.asset");
            arena.smallSize = 6; arena.mediumSize = 8; arena.largeSize = 10; arena.extraLargeSize = 12; arena.hugeSize = 14; arena.moveSpeed = 6.4f;
            arena.gravity = 16.7f; arena.restitution = 1;
            arena.playerColor = new Color32(255,98,106,255); arena.enemyColor = new Color32(0,211,220,255);
            EditorUtility.SetDirty(arena);
            var definitions = new WeaponDefinition[Enum.GetValues(typeof(WeaponKind)).Length];
            string[] names = { "Меч", "Копьё", "Клинок", "Коса", "Щит", "Безоружный", "Лук", "Посох", "Сюрикен", "Арбалет" };
            string[] descriptions = { "+1 к урону за попадание", "+0,5r к длине за попадание", "Ускорение за попадание · без потолка", "+1/с яда за попадание", "Возврат 100% · растёт ширина", "Урон от скорости · разгон за удар", "+1 стрела в очереди за попадание", "3 урона · +0,35 к радиусу", "+1 отскок за попадание", "+1 стрела в залпе за попадание" };
            for (int i = 0; i < definitions.Length; i++)
            {
                string kind = ((WeaponKind)i).ToString();
                var definition = GetOrCreate<WeaponDefinition>(Root + "Settings/" + kind + ".asset");
                definition.kind = (WeaponKind)i; definition.displayName = names[i]; definition.description = descriptions[i];
                definition.damage = i == 4 ? 0 : i == 1 || i == 2 ? 2 : i == 7 ? 3 : 1; definition.reach = i == 1 ? 3.5f : i == 2 ? 1.8f : 2.6f; definition.rotationSpeed = i switch { 0 => 372, 1 => 286, 2 => 573, 7 => 172, _ => 300 };
                definition.growth = i switch { 1 => 0.5f, 2 => 120, 4 => 0.25f, 5 => 0.5f, 7 => 0.35f, _ => 1 };
                definition.sprite = Sprite(kind);
                definition.cooldown = i == 8 ? 0.8f : 1;
                definition.projectileSpeed = i == 7 ? 10 : i == 8 ? 11 : 14;
                definition.blastRadius = 1.5f; definition.waveSpeed = 6; definition.burstInterval = 0.06f; definition.unarmedMaxSpeed = 4; definition.unarmedSpeedUnit = 1.6f;
                definition.projectileSprite = i == 6 || i == 9 ? Sprite("Arrow") : i == 7 ? Sprite("Fireball") : i == 8 ? Sprite("Shuriken") : null;
                var shape = GetOrCreate<WeaponShapeDefinition>(Root + "Resources/WeaponShapes/" + kind + ".asset");
                shape.artwork = ExtractMask(kind);
                if (i == 1) { shape.shaft = ExtractMask("SpearShaft"); shape.head = ExtractMask("SpearHead"); }
                definition.shape = shape; EditorUtility.SetDirty(shape);
                var go = new GameObject(kind); var view = go.AddComponent<WeaponView>();
                view.artwork = SpriteObject("Artwork", definition.sprite, go.transform, 4);
                if (i == 1)
                {
                    view.artwork.sprite = Sprite("SpearShaft"); view.spearShaft = view.artwork;
                    view.spearHead = SpriteObject("Head", Sprite("SpearHead"), go.transform, 4);
                }
                definition.prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "Prefabs/" + kind + ".prefab").GetComponent<WeaponView>();
                UnityEngine.Object.DestroyImmediate(go); EditorUtility.SetDirty(definition); definitions[i] = definition;
            }
            var orbGo = new GameObject("Orb"); var orb = orbGo.AddComponent<OrbView>();
            orb.shadow = SpriteObject("Shadow", Sprite("Disc"), orbGo.transform, 1);
            orb.shadow.color = new Color(0.1f, 0.1f, 0.15f, 0.09f); orb.shadow.transform.localPosition = new Vector3(0.03f, -0.09f, 0);
            orb.shadow.enabled = false;
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
            camera.orthographic = true; camera.orthographicSize = arena.mediumSize / 2 + 0.2f;
            camera.backgroundColor = Color.white; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 50;
            cameraGo.AddComponent<AudioListener>();
            var arenaView = BuildArena(arena.mediumSize, camera);
            var gameGo = new GameObject("ArmOrb Game"); var game = gameGo.AddComponent<ArmOrbGame>();
            game.settings = arena; game.weapons = definitions; game.orbPrefab = orbPrefab;
            game.arenaCamera = camera; game.uiFont = font;
            game.arenaView = arenaView;
            game.styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "UI/ArmOrb.uss");
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
                importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable = true;
                importer.wrapMode = Path.GetFileNameWithoutExtension(path) == "SpearShaft" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                string name = Path.GetFileNameWithoutExtension(path);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spritePivot = name is "Orb" or "Disc" or "AppIcon" or "SetupIllustration" or "Unarmed" or "Shuriken" or "Arrow" or "Fireball" ? new Vector2(0.5f, 0.5f) : new Vector2(0, 0.5f);
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
        }
        static ArenaView BuildArena(float size, Camera camera)
        {
            var root = new GameObject("Arena");
            var view = root.AddComponent<ArenaView>(); view.arenaCamera = camera;
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
            view.boundary = line; view.Configure(size); return view;
        }
        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "EnotikPoker"; PlayerSettings.productName = "ArmOrb";
            PlayerSettings.bundleVersion = "0.10.0"; PlayerSettings.Android.bundleVersionCode = 11;
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
                scenes = new[] { ScenePath }, locationPathName = "Builds/Android/ArmOrb-v" + PlayerSettings.bundleVersion + ".apk",
                target = BuildTarget.Android, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed: " + report.summary.result);
            Debug.Log($"ArmOrb APK built: {report.summary.totalSize} bytes, {report.summary.totalTime}");
        }

        [MenuItem("ArmOrb/Measure reference fight tempo")]
        public static void MeasureTempo()
        {
            var sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Root + "Settings/Sword.asset");
            var spear = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Root + "Settings/Spear.asset");
            for (int seed = 1; seed <= 3; seed++)
            {
                var sim = new ArenaSimulation(sword.CreateState(),spear.CreateState(),100,100,seed,8,0.55f,6.4f,true);
                int hits = 0, clashes = 0; float firstHit = -1;
                sim.Event += e =>
                {
                    if (e.Kind == CombatEventKind.Hit) { hits++; if (firstHit < 0) firstHit = sim.Elapsed; }
                    if (e.Kind == CombatEventKind.Clash) clashes++;
                };
                for (int tick = 0; tick < 120*180 && sim.Result == MatchResult.InProgress; tick++) sim.Step(1f/120);
                Debug.Log($"TEMPO seed={seed} result={sim.Result} seconds={sim.Elapsed:0.00} firstHit={firstHit:0.00} hits={hits} clashes={clashes} HP={sim.Orbs[0].Health.Current:0.#}/{sim.Orbs[1].Health.Current:0.#}");
            }
        }

        static PixelMask ExtractMask(string name)
        {
            var sprite = Sprite(name); var texture = sprite.texture; var pixels = texture.GetPixels32();
            int width = texture.width, height = texture.height;
            var result = new List<Rect>();
            var active = new Dictionary<(int,int),RectInt>();
            for (int y = 0; y <= height; y++)
            {
                var next = new Dictionary<(int,int),RectInt>();
                if (y < height)
                    for (int x = 0; x < width;)
                    {
                        if (pixels[y*width+x].a < 128) { x++; continue; }
                        int start = x; while (x < width && pixels[y*width+x].a >= 128) x++;
                        var key = (start,x-start);
                        next[key] = active.TryGetValue(key,out var previous) ? new RectInt(start,previous.y,x-start,previous.height+1) : new RectInt(start,y,x-start,1);
                    }
                foreach (var pair in active)
                    if (!next.ContainsKey(pair.Key))
                    {
                        var r = pair.Value;
                        result.Add(new Rect((float)r.x/width,(float)r.y/height,(float)r.width/width,(float)r.height/height));
                    }
                active = next;
            }
            return new PixelMask { sprite = sprite, rectangles = result.ToArray() };
        }
    }
}
