using System;
using System.Collections.Generic;
using System.IO;
using Ouroboros.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ouroboros.Editor
{
    public static class OSProjectSetup
    {
        private const string Root = "Assets/Ouroboros";
        private const string InputActionsPath = Root + "/Input/OSInputActions.inputactions";
        private const string BootScenePath = Root + "/Scenes/00_Boot.unity";
        private const string MainMenuScenePath = Root + "/Scenes/10_MainMenu.unity";
        private const string GameScenePath = Root + "/Scenes/20_Game.unity";
        private const int IconSize = 32;

        private static readonly string[] RequiredFolders =
        {
            Root + "/Art",
            Root + "/Art/Placeholders",
            Root + "/Audio",
            Root + "/BuildProfiles",
            Root + "/Data",
            Root + "/Data/Balance",
            Root + "/Data/Enemies",
            Root + "/Data/Waves",
            Root + "/Data/Upgrades",
            Root + "/Input",
            Root + "/Prefabs",
            Root + "/Prefabs/Player",
            Root + "/Prefabs/Enemies",
            Root + "/Prefabs/Projectiles",
            Root + "/Prefabs/Pickups",
            Root + "/Prefabs/VFX",
            Root + "/Prefabs/UI",
            Root + "/Scenes",
            Root + "/Scripts/Body",
            Root + "/Scripts/Combat",
            Root + "/Scripts/CustomCharacter",
            Root + "/Scripts/Enemies",
            Root + "/Tests",
            Root + "/Tests/EditMode",
            Root + "/Tests/PlayMode"
        };

        private static readonly EditorBuildSettingsScene[] BuildScenes =
        {
            new EditorBuildSettingsScene(BootScenePath, true),
            new EditorBuildSettingsScene(MainMenuScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };

        [MenuItem("Ouroboros/Setup/Apply Step 01 Foundation")]
        public static void ApplyStep01Foundation()
        {
            ConfigureProjectSettings();
            EnsureFolders();
            CreatePlaceholderSprites();
            CreateBootSceneIfMissing();
            CreateMainMenuSceneIfMissing();
            CreateGameSceneIfMissing();
            ConfigureBuildScenes();
            ConfigureBuildProfiles();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 01 foundation applied.");
        }

        private static void ConfigureProjectSettings()
        {
            PlayerSettings.productName = "OUROBOROS SWARM";
            if (PlayerSettings.companyName == "DefaultCompany")
            {
                PlayerSettings.companyName = "Ouroboros";
            }

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 540;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.usePlayerLog = true;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSettings.projectGenerationRootNamespace = "Ouroboros";
        }

        private static void EnsureFolders()
        {
            for (var i = 0; i < RequiredFolders.Length; i++)
            {
                EnsureFolder(RequiredFolders[i]);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException($"Invalid Unity folder path: {path}");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void CreatePlaceholderSprites()
        {
            CreatePlaceholder("Head", new Color32(54, 231, 255, 255), IconKind.Head);
            CreatePlaceholder("Body_Shield", new Color32(61, 139, 255, 255), IconKind.Shield);
            CreatePlaceholder("Body_Attack", new Color32(255, 77, 92, 255), IconKind.Attack);
            CreatePlaceholder("Body_Laser", new Color32(222, 74, 255, 255), IconKind.Laser);
            CreatePlaceholder("Body_Control", new Color32(65, 230, 145, 255), IconKind.Control);
            CreatePlaceholder("Enemy_Chaser", new Color32(255, 144, 54, 255), IconKind.Enemy);
            CreatePlaceholder("Projectile", new Color32(255, 235, 84, 255), IconKind.Projectile);
            CreatePlaceholder("Pickup", new Color32(109, 255, 211, 255), IconKind.Pickup);
        }

        private static void CreatePlaceholder(string fileName, Color32 color, IconKind kind)
        {
            var assetPath = $"{Root}/Art/Placeholders/{fileName}.png";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) != null)
            {
                return;
            }

            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[IconSize * IconSize];
            for (var y = 0; y < IconSize; y++)
            {
                for (var x = 0; x < IconSize; x++)
                {
                    pixels[(y * IconSize) + x] = EvaluatePixel(x, y, color, kind);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unable to resolve project root.");
            var absolutePath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = IconSize;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static Color32 EvaluatePixel(int x, int y, Color32 color, IconKind kind)
        {
            var dx = x - 15.5f;
            var dy = y - 15.5f;
            var radiusSquared = (dx * dx) + (dy * dy);
            var white = new Color32(245, 250, 255, 255);
            var dark = new Color32(22, 29, 44, 255);
            var clear = new Color32(0, 0, 0, 0);

            switch (kind)
            {
                case IconKind.Head:
                    if (radiusSquared > 196f) return clear;
                    if ((x >= 18 && x <= 21 && y >= 18 && y <= 21) ||
                        (x >= 10 && x <= 13 && y >= 18 && y <= 21)) return dark;
                    return color;
                case IconKind.Shield:
                    if (radiusSquared > 196f || radiusSquared < 72f) return clear;
                    if (Mathf.Abs(dx) < 1.5f || Mathf.Abs(dy) < 1.5f) return white;
                    return color;
                case IconKind.Attack:
                    if (y < 5 || y > 27 || Mathf.Abs(dx) > (27 - y) * 0.55f) return clear;
                    if (Mathf.Abs(dx + dy * 0.2f) < 1.3f) return white;
                    return color;
                case IconKind.Laser:
                    if (Mathf.Abs(dx) > 7f || Mathf.Abs(dy) > 14f) return clear;
                    if (Mathf.Abs(dx) < 1.5f) return white;
                    return color;
                case IconKind.Control:
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > 15f) return clear;
                    if (Mathf.Abs(dx) < 1.5f || Mathf.Abs(dy) < 1.5f) return white;
                    return color;
                case IconKind.Enemy:
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > 15f) return clear;
                    if (Mathf.Abs(dx - dy) < 1.5f || Mathf.Abs(dx + dy) < 1.5f) return dark;
                    return color;
                case IconKind.Projectile:
                    if (radiusSquared > 64f) return clear;
                    return radiusSquared < 16f ? white : color;
                case IconKind.Pickup:
                    if (Mathf.Abs(dx) > 12f || Mathf.Abs(dy) > 12f) return clear;
                    if (Mathf.Abs(dx) < 2f || Mathf.Abs(dy) < 2f) return white;
                    return color;
                default:
                    return clear;
            }
        }

        private static void CreateBootSceneIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) != null)
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootRoot = new GameObject("BootRoot");
            var bootstrap = bootRoot.AddComponent<OSBootstrap>();
            var serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            CreateCamera(null);
            CreateGlobalLight(null);

            var canvas = CreateCanvas("BootCanvas");
            CreateText(canvas.transform, "Status", "OUROBOROS: SWARM\nValidating project foundation...", 38,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(900f, 180f), Vector2.zero);
            CreateBuildInfoText(canvas.transform);

            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        private static void CreateMainMenuSceneIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath) != null)
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("MainMenuRoot");
            var controller = root.AddComponent<OSMainMenuController>();

            CreateCamera(null);
            CreateGlobalLight(null);
            CreateEventSystem();

            var canvas = CreateCanvas("MainMenuCanvas");
            CreatePanel(canvas.transform, "Background", new Color32(12, 18, 32, 255));
            CreateText(canvas.transform, "Title", "OUROBOROS: SWARM", 74, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.72f), new Vector2(1200f, 120f), Vector2.zero);
            CreateText(canvas.transform, "Subtitle", "ACCUMULATE  •  CUT  •  DETONATE  •  REGROW", 24,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.62f), new Vector2(1000f, 60f), Vector2.zero);
            var button = CreateButton(canvas.transform, "StartButton", "START", new Vector2(0.5f, 0.43f),
                new Vector2(360f, 88f));
            UnityEventTools.AddPersistentListener(button.onClick, controller.StartGame);
            CreateText(canvas.transform, "Hint", "PRESS ENTER OR SELECT START", 22, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.33f), new Vector2(700f, 50f), Vector2.zero);
            CreateBuildInfoText(canvas.transform);

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void CreateGameSceneIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) != null)
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameRoot = new GameObject("GameRoot");
            var systems = CreateChild(gameRoot.transform, "Systems");
            CreateChild(systems, "OSGameSessionController");
            CreateChild(systems, "OSInputRouter");
            CreateChild(systems, "OSPoolRegistry");
            CreateChild(systems, "OSEnemyRegistry");
            CreateChild(systems, "OSWaveDirector");
            CreateChild(systems, "OSSelectionQueueHost");
            CreateChild(systems, "OSAudioVfxRouter");

            var world = CreateChild(gameRoot.transform, "World");
            CreateChild(world, "Arena");
            CreateChild(world, "Obstacles");
            CreateChild(world, "RuntimePools");
            CreateGlobalLight(world);

            var playerRoot = CreateChild(gameRoot.transform, "PlayerRoot");
            var head = CreateChild(playerRoot, "Head");
            AddSprite(head, "Head", Vector3.zero, 1.2f);
            var bodyChain = CreateChild(playerRoot, "BodyChain");
            AddSprite(bodyChain, "Body_Shield", new Vector3(-0.8f, 0f, 0f), 0.8f);
            AddSprite(bodyChain, "Body_Attack", new Vector3(-1.55f, 0f, 0f), 0.8f);
            AddSprite(bodyChain, "Body_Laser", new Vector3(-2.3f, 0f, 0f), 0.8f);
            AddSprite(bodyChain, "Body_Control", new Vector3(-3.05f, 0f, 0f), 0.8f);
            CreateChild(playerRoot, "PickupCollector");

            AddSprite(world, "Enemy_Chaser", new Vector3(3f, 1.25f, 0f), 1f);
            AddSprite(world, "Projectile", new Vector3(1.5f, 0.5f, 0f), 0.45f);
            AddSprite(world, "Pickup", new Vector3(2f, -1.4f, 0f), 0.65f);

            var cameraRoot = CreateChild(gameRoot.transform, "CameraRoot");
            CreateCamera(cameraRoot);
            CreateEventSystem();

            var canvas = CreateCanvas("Canvas");
            canvas.transform.SetParent(gameRoot.transform, false);
            var combatHud = CreatePanel(canvas.transform, "CombatHUD", new Color32(10, 16, 28, 96));
            SetRect(combatHud.rectTransform, new Vector2(0f, 1f), new Vector2(520f, 160f),
                new Vector2(280f, -95f));
            CreateText(combatHud.transform, "HP", "HP 100 / 100", 26, TextAlignmentOptions.Left,
                new Vector2(0.5f, 0.7f), new Vector2(450f, 42f), Vector2.zero);
            CreateText(combatHud.transform, "Body", "BODY 4  |  [S] [A] [L] [C]", 22, TextAlignmentOptions.Left,
                new Vector2(0.5f, 0.35f), new Vector2(450f, 42f), Vector2.zero);
            CreateText(canvas.transform, "Timer", "00:00", 34, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.95f), new Vector2(280f, 52f), Vector2.zero);
            CreateText(canvas.transform, "FoundationLabel", "STEP 01 FOUNDATION SCENE", 20,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.08f), new Vector2(600f, 45f), Vector2.zero);
            CreateInactivePanel(canvas.transform, "BodyRoleSelectionPanel");
            CreateInactivePanel(canvas.transform, "LevelUpPanel");
            CreateInactivePanel(canvas.transform, "TutorialLayer");
            CreateInactivePanel(canvas.transform, "ResultPanel");
            CreateBuildInfoText(canvas.transform);

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = BuildScenes;
        }

        private static void ConfigureBuildProfiles()
        {
#if UNITY_6000_0_OR_NEWER
            CreateOrUpdateBuildProfile("Windows", "Windows Development",
                Root + "/BuildProfiles/Windows Development.asset");
            CreateOrUpdateBuildProfile("Web", "WebGL Development",
                Root + "/BuildProfiles/WebGL Development.asset");
#endif
        }

#if UNITY_6000_0_OR_NEWER
        private static void CreateOrUpdateBuildProfile(string platformDisplayName, string profileName, string targetPath)
        {
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(targetPath);
            if (profile == null)
            {
                var modules = BuildProfile.GetInstalledPlatformModules();
                BuildProfile created = null;
                for (var i = 0; i < modules.Count; i++)
                {
                    if (modules[i].displayName == platformDisplayName)
                    {
                        created = BuildProfile.CreateBuildProfile(modules[i].platformGuid, profileName, null);
                        break;
                    }
                }

                if (created == null)
                {
                    Debug.LogWarning($"[OUROBOROS][SETUP] Platform module '{platformDisplayName}' is unavailable; profile not created.");
                    return;
                }

                var sourcePath = AssetDatabase.GetAssetPath(created);
                if (sourcePath != targetPath)
                {
                    var moveError = AssetDatabase.MoveAsset(sourcePath, targetPath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        throw new InvalidOperationException(moveError);
                    }
                }

                profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(targetPath);
            }

            if (profile == null)
            {
                throw new InvalidOperationException($"Build profile was not created at '{targetPath}'.");
            }

            profile.overrideGlobalScenes = true;
            profile.scenes = BuildScenes;
            EditorUtility.SetDirty(profile);
        }
#endif

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(8, 13, 24, 255);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
        }

        private static void CreateGlobalLight(Transform parent)
        {
            var lightObject = new GameObject("Global Light 2D");
            lightObject.transform.SetParent(parent, false);
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        private static Canvas CreateCanvas(string name)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
            {
                throw new InvalidOperationException($"Input Action Asset is missing: {InputActionsPath}");
            }

            inputModule.actionsAsset = actions;
            inputModule.point = InputActionReference.Create(actions.FindAction("UI/Point", true));
            inputModule.leftClick = InputActionReference.Create(actions.FindAction("UI/Click", true));
            inputModule.move = InputActionReference.Create(actions.FindAction("UI/Navigate", true));
            inputModule.submit = InputActionReference.Create(actions.FindAction("UI/Submit", true));
            inputModule.cancel = InputActionReference.Create(actions.FindAction("UI/Cancel", true));
        }

        private static Image CreatePanel(Transform parent, string name, Color32 color)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.GetComponent<Image>();
            image.color = color;
            Stretch(panelObject.GetComponent<RectTransform>());
            return image;
        }

        private static void CreateInactivePanel(Transform parent, string name)
        {
            var image = CreatePanel(parent, name, new Color32(18, 28, 48, 230));
            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(780f, 520f), Vector2.zero);
            CreateText(image.transform, "Label", name, 34, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(700f, 100f), Vector2.zero);
            image.gameObject.SetActive(false);
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string value,
            float fontSize,
            TextAlignmentOptions alignment,
            Vector2 anchor,
            Vector2 size,
            Vector2 position)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color32(235, 245, 255, 255);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            SetRect(text.rectTransform, anchor, size, position);
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 size)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color32(38, 177, 196, 255);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            SetRect(buttonObject.GetComponent<RectTransform>(), anchor, size, Vector2.zero);
            CreateText(buttonObject.transform, "Label", label, 34, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), size, Vector2.zero);
            return button;
        }

        private static void CreateBuildInfoText(Transform parent)
        {
            var text = CreateText(parent, "BuildInfo", OSBuildInfo.Label, 17, TextAlignmentOptions.BottomRight,
                new Vector2(1f, 0f), new Vector2(1100f, 40f), new Vector2(-565f, 26f));
            text.gameObject.AddComponent<OSBuildInfoPresenter>();
        }

        private static void AddSprite(Transform parent, string assetName, Vector3 localPosition, float scale)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Art/Placeholders/{assetName}.png");
            var spriteObject = new GameObject(assetName, typeof(SpriteRenderer));
            spriteObject.transform.SetParent(parent, false);
            spriteObject.transform.localPosition = localPosition;
            spriteObject.transform.localScale = Vector3.one * scale;
            spriteObject.GetComponent<SpriteRenderer>().sprite = sprite;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetRect(
            RectTransform rectTransform,
            Vector2 anchor,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
        }

        private enum IconKind
        {
            Head,
            Shield,
            Attack,
            Laser,
            Control,
            Enemy,
            Projectile,
            Pickup
        }
    }
}
