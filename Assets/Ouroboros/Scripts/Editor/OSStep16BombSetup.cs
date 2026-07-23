using System;
using System.IO;
using System.Linq;
using Ouroboros.Core;
using Ouroboros.Runtime;
using Ouroboros.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ouroboros.Editor
{
    public static class OSStep16BombSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string InputActionsPath = "Assets/Ouroboros/Input/OSInputActions.inputactions";
        private const string BodyBalancePath = "Assets/Ouroboros/Data/Balance/OSBodyBalance.asset";
        private const string UpgradeCatalogPath = "Assets/Ouroboros/Data/Upgrades/OSUpgradeCatalog.asset";
        private const string FeedbackCatalogPath =
            "Assets/Ouroboros/Data/Balance/OSFeedbackCatalog.asset";
        private const string ProjectileSpritePath =
            "Assets/Ouroboros/Art/Placeholders/Projectile.png";
        private const string WebGLProfilePath =
            "Assets/Ouroboros/BuildProfiles/WebGL Development.asset";

        [MenuItem("Ouroboros/Setup/Apply Step 16.1 Bomb")]
        public static void ApplyStep16Bomb()
        {
            EnsureBombInput();
            var bodyBalance = ConfigureBodyBalance();
            ConfigureUpgradeCatalog();
            ConfigureFeedbackCatalog();
            ConfigureGameScene(bodyBalance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[OUROBOROS][SETUP] Step 16.1 Bomb input, data, upgrades, visuals, and scene wiring applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 16.1 WebGL")]
        public static void BuildStep16BombWebGL()
        {
            ApplyStep16Bomb();
            BuildWebGL();
        }

        private static void EnsureBombInput()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath)
                          ?? throw new InvalidOperationException(
                              $"Input actions are missing at '{InputActionsPath}'.");
            var player = actions.FindActionMap("Player", true);
            var bomb = player.FindAction("Bomb", false);
            var changed = false;
            if (bomb == null)
            {
                bomb = player.AddAction(
                    "Bomb",
                    InputActionType.Button,
                    expectedControlLayout: "Button",
                    interactions: "Press");
                changed = true;
            }

            var hasBinding = bomb.bindings.Any(binding =>
                string.Equals(binding.path, "<Keyboard>/b", StringComparison.OrdinalIgnoreCase));
            if (!hasBinding)
            {
                bomb.AddBinding("<Keyboard>/b", groups: "Keyboard&Mouse");
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            File.WriteAllText(Path.GetFullPath(InputActionsPath), actions.ToJson());
            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceUpdate);
        }

        private static OSBodyBalanceData ConfigureBodyBalance()
        {
            var balance = AssetDatabase.LoadAssetAtPath<OSBodyBalanceData>(BodyBalancePath)
                          ?? throw new InvalidOperationException(
                              $"Body balance is missing at '{BodyBalancePath}'.");
            var serialized = new SerializedObject(balance);
            serialized.FindProperty("dataVersion").stringValue = "step16.1-v1";
            var bomb = serialized.FindProperty("bomb")
                       ?? throw new InvalidOperationException("OSBodyBalanceData.bomb is missing.");
            bomb.FindPropertyRelative("minimumBodyCount").intValue = 10;
            bomb.FindPropertyRelative("consumeRate").floatValue = 0.1f;
            bomb.FindPropertyRelative("drawDuration").floatValue = 1f;
            bomb.FindPropertyRelative("gatherDuration").floatValue = 0.5f;
            bomb.FindPropertyRelative("damage").floatValue = 100f;
            bomb.FindPropertyRelative("cooldown").floatValue = 10f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(balance);
            return balance;
        }

        private static void ConfigureUpgradeCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OSUpgradeCatalog>(UpgradeCatalogPath)
                          ?? throw new InvalidOperationException(
                              $"Upgrade catalog is missing at '{UpgradeCatalogPath}'.");
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("dataVersion").stringValue = "step16.1-v1";
            var entries = serialized.FindProperty("entries")
                          ?? throw new InvalidOperationException("OSUpgradeCatalog.entries is missing.");
            entries.arraySize = OSUpgradeCatalog.RequiredUpgradeCount;
            ConfigureUpgrade(
                entries.GetArrayElementAtIndex(15),
                "bomb_damage",
                OSUpgradeCategory.Bomb,
                OSUpgradeOperation.AddBombDamageMultiplier,
                0.2f,
                3,
                1f,
                3f);
            ConfigureUpgrade(
                entries.GetArrayElementAtIndex(16),
                "bomb_cooldown",
                OSUpgradeCategory.Bomb,
                OSUpgradeOperation.AddBombCooldownDelta,
                -1f,
                3,
                5f,
                10f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void ConfigureUpgrade(
            SerializedProperty entry,
            string id,
            OSUpgradeCategory category,
            OSUpgradeOperation operation,
            float perLevel,
            int maxLevel,
            float clampMinimum,
            float clampMaximum)
        {
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("category").enumValueIndex = (int)category;
            entry.FindPropertyRelative("operation").enumValueIndex = (int)operation;
            entry.FindPropertyRelative("perLevelValue").floatValue = perLevel;
            entry.FindPropertyRelative("maxLevel").intValue = maxLevel;
            entry.FindPropertyRelative("clampMinimum").floatValue = clampMinimum;
            entry.FindPropertyRelative("clampMaximum").floatValue = clampMaximum;
            entry.FindPropertyRelative("candidateWeight").floatValue = 1f;
        }

        private static void ConfigureFeedbackCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OSFeedbackCatalog>(FeedbackCatalogPath)
                          ?? throw new InvalidOperationException(
                              $"Feedback catalog is missing at '{FeedbackCatalogPath}'.");
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("dataVersion").stringValue = "step16.1-v1";
            AppendUnique(serialized.FindProperty("telegraphKeys"), "bomb_ring");
            AppendUnique(serialized.FindProperty("audioKeys"), "bomb");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void AppendUnique(SerializedProperty values, string value)
        {
            for (var index = 0; index < values.arraySize; index++)
            {
                if (values.GetArrayElementAtIndex(index).stringValue == value)
                {
                    return;
                }
            }

            values.InsertArrayElementAtIndex(values.arraySize);
            values.GetArrayElementAtIndex(values.arraySize - 1).stringValue = value;
        }

        private static void ConfigureGameScene(OSBodyBalanceData bodyBalance)
        {
            var scene = SceneManager.GetSceneByPath(GameScenePath);
            var openedForSetup = !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var gameRoot = FindRoot(scene, "GameRoot");
                var systems = gameRoot.transform.Find("Systems")
                              ?? throw new InvalidOperationException("GameRoot/Systems is missing.");
                var session = RequireComponent<OSGameSessionController>(gameRoot);
                var player = RequireComponent<OSPlayerController>(gameRoot);
                var health = RequireComponent<OSPlayerHealth>(gameRoot);
                var chain = RequireComponent<OSBodyChain>(gameRoot);
                var growth = RequireComponent<OSBodyGrowthController>(gameRoot);
                var enemies = RequireComponent<OSEnemyRegistry>(gameRoot);
                var level = RequireComponent<OSLevelUpController>(gameRoot);
                var summary = RequireComponent<OSRunSummaryController>(gameRoot);
                var hud = RequireComponent<OSCombatHudPresenter>(gameRoot);

                var bomb = systems.GetComponent<OSBombController>();
                if (bomb == null)
                {
                    bomb = systems.gameObject.AddComponent<OSBombController>();
                }
                var viewRoot = GetOrCreateChild(systems, "BombVfx");
                var line = GetOrCreateChild(viewRoot, "BombRing").gameObject
                    .GetComponent<LineRenderer>();
                if (line == null)
                {
                    line = viewRoot.Find("BombRing").gameObject.AddComponent<LineRenderer>();
                }

                line.enabled = false;
                line.useWorldSpace = true;
                line.loop = false;
                line.startWidth = 0.14f;
                line.endWidth = 0.14f;
                line.startColor = new Color32(80, 235, 255, 235);
                line.endColor = new Color32(35, 130, 255, 235);
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.sortingOrder = 190;
                line.sharedMaterial =
                    AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");

                var fillObject = GetOrCreateChild(viewRoot, "BombExplosionFill").gameObject;
                var fill = fillObject.GetComponent<SpriteRenderer>();
                if (fill == null)
                {
                    fill = fillObject.AddComponent<SpriteRenderer>();
                }
                fill.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectileSpritePath)
                              ?? throw new InvalidOperationException(
                                  $"Bomb fill sprite is missing at '{ProjectileSpritePath}'.");
                fill.color = new Color(0.32f, 0.92f, 1f, 0f);
                fill.sortingOrder = 189;
                fill.enabled = false;

                var view = viewRoot.GetComponent<OSBombView>();
                if (view == null)
                {
                    view = viewRoot.gameObject.AddComponent<OSBombView>();
                }
                view.Configure(line, fill);

                var hurtboxLayer = LayerMask.NameToLayer("EnemyHurtbox");
                if (hurtboxLayer < 0)
                {
                    throw new InvalidOperationException("EnemyHurtbox layer is missing.");
                }

                bomb.Configure(
                    session,
                    player,
                    health,
                    chain,
                    growth,
                    enemies,
                    bodyBalance,
                    view,
                    1 << hurtboxLayer);
                level.ConfigureBomb(bomb);
                summary.ConfigureBomb(bomb);
                hud.ConfigureBomb(bomb);
                ConfigureActionHud(gameRoot.transform);

                EditorUtility.SetDirty(bomb);
                EditorUtility.SetDirty(view);
                EditorUtility.SetDirty(line);
                EditorUtility.SetDirty(fill);
                EditorUtility.SetDirty(level);
                EditorUtility.SetDirty(summary);
                EditorUtility.SetDirty(hud);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedForSetup && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ConfigureActionHud(Transform gameRoot)
        {
            var panel = gameRoot.Find("Canvas/ReadabilityHUD/ActionSummaryPanel");
            var label = panel?.Find("ActionLabel")?.GetComponent<TMP_Text>();
            if (panel == null || label == null)
            {
                return;
            }

            var rect = (RectTransform)panel;
            rect.sizeDelta = new Vector2(916f, 84f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 18f;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            EditorUtility.SetDirty(rect);
            EditorUtility.SetDirty(label);
        }

        private static void BuildWebGL()
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step16_1", "WebGL"));
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(WebGLProfilePath)
                          ?? throw new BuildFailedException(
                              $"WebGL build profile is missing at '{WebGLProfilePath}'.");
            BuildProfile.SetActiveBuildProfile(profile);
            var scenes = profile.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes are configured for WebGL.");
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.Development
            });
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Step 16.1 WebGL build failed: {summary.result}, errors {summary.totalErrors}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 16.1 WebGL succeeded at '{outputPath}' " +
                $"with errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name)
                   ?? throw new InvalidOperationException(
                       $"Root '{name}' is missing from '{scene.path}'.");
        }

        private static T RequireComponent<T>(GameObject root) where T : Component
        {
            return root.GetComponentInChildren<T>(true)
                   ?? throw new InvalidOperationException(
                       $"Required component '{typeof(T).Name}' is missing.");
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created.transform;
        }
    }
}
