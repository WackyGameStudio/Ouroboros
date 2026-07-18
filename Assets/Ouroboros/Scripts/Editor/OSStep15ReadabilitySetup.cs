using System;
using System.IO;
using System.Linq;
using Ouroboros.Runtime;
using Ouroboros.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ouroboros.Editor
{
    public static class OSStep15ReadabilitySetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private static readonly string[] EnemyPrefabPaths =
        {
            "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_Chaser.prefab",
            "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_Charger.prefab",
            "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_Shooter.prefab",
            "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_Splitter.prefab",
            "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_SplitterSpawn.prefab",
            "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_EliteAccelerator.prefab",
            "Assets/Ouroboros/Prefabs/Enemies/PF_Boss_SwarmCore.prefab"
        };

        [MenuItem("Ouroboros/Setup/Apply Step 15 Readability")]
        public static void ApplyStep15Readability()
        {
            if (!HasStep14Foundation())
            {
                OSStep14BossSetup.ApplyStep14BossAndResults();
            }

            ConfigureEnemyPrefabs();
            ConfigureGameScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 15 consolidated HUD, tutorial and readability applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 15 WebGL")]
        public static void BuildStep15WebGL()
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step15", "WebGL"));
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes are configured for the WebGL build.");
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
                    $"Step 15 WebGL build failed: {summary.result}, errors {summary.totalErrors}, " +
                    $"warnings {summary.totalWarnings}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 15 WebGL succeeded: {outputPath}, " +
                $"errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static void ConfigureGameScene()
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
                var systems = RequireTransform(gameRoot.transform, "Systems");
                var head = RequireTransform(gameRoot.transform, "PlayerRoot/Head");
                var canvas = RequireTransform(gameRoot.transform, "Canvas");
                var font = RequireTransform(canvas, "FoundationLabel").GetComponent<TMP_Text>().font;

                var session = RequireComponent<OSGameSessionController>(systems);
                var player = head.GetComponent<OSPlayerController>();
                var health = head.GetComponent<OSPlayerHealth>();
                var chain = RequireComponent<OSBodyChain>(gameRoot.transform);
                var growth = RequireComponent<OSBodyGrowthController>(systems);
                var roles = RequireComponent<OSBodyRoleRegistry>(systems);
                var attack = RequireComponent<OSAttackBodyRole>(systems);
                var laser = RequireComponent<OSLaserBodyRole>(systems);
                var control = RequireComponent<OSControlBodyRole>(systems);
                var shield = RequireComponent<OSShieldBodyRole>(systems);
                var explosion = RequireComponent<OSExplosionController>(systems);
                var level = RequireComponent<OSLevelUpController>(systems);
                var wave = RequireComponent<OSWaveDirector>(systems);
                var boss = RequireComponent<OSBossEncounterController>(systems);
                var summary = RequireComponent<OSRunSummaryController>(systems);

                var readability = GetOrCreateUiObject(canvas, "ReadabilityHUD");
                Stretch((RectTransform)readability);

                var primaryPanel = CreatePanel(readability, "CombatSummaryPanel", new Color32(7, 15, 28, 228));
                SetTopLeft((RectTransform)primaryPanel, new Vector2(22f, -108f), new Vector2(555f, 156f));
                var primary = CreateLabel(primaryPanel, "PrimaryLabel", font, 19f, TextAlignmentOptions.TopLeft);

                var actionPanel = CreatePanel(readability, "ActionSummaryPanel", new Color32(7, 15, 28, 228));
                SetBottomLeft((RectTransform)actionPanel, new Vector2(22f, 22f), new Vector2(760f, 84f));
                var action = CreateLabel(actionPanel, "ActionLabel", font, 18f, TextAlignmentOptions.MidlineLeft);

                var threatPanel = CreatePanel(readability, "ThreatPriorityPanel", new Color32(50, 8, 24, 238));
                SetTopCenter((RectTransform)threatPanel, new Vector2(0f, -20f), new Vector2(860f, 72f));
                var threat = CreateLabel(threatPanel, "ThreatLabel", font, 19f, TextAlignmentOptions.Center);
                threat.color = new Color32(255, 218, 224, 255);

                var tutorialPanel = CreatePanel(readability, "TutorialPanel", new Color32(12, 42, 62, 238));
                SetBottomCenter((RectTransform)tutorialPanel, new Vector2(0f, 124f), new Vector2(940f, 56f));
                var tutorial = CreateLabel(tutorialPanel, "TutorialLabel", font, 20f, TextAlignmentOptions.Center);
                tutorial.color = new Color32(150, 238, 255, 255);

                var feedbackPanel = CreatePanel(readability, "FeedbackPanel", new Color32(76, 21, 13, 235));
                SetBottomCenter((RectTransform)feedbackPanel, new Vector2(0f, 194f), new Vector2(950f, 52f));
                var feedback = CreateLabel(feedbackPanel, "FeedbackLabel", font, 20f, TextAlignmentOptions.Center);
                feedback.color = new Color32(255, 215, 105, 255);
                feedbackPanel.gameObject.SetActive(true);
                feedback.gameObject.SetActive(false);

                var hud = GetOrAdd<OSCombatHudPresenter>(readability.gameObject);
                hud.Configure(
                    session, health, chain, growth, roles, attack, laser, control, shield,
                    explosion, level, wave, boss, primary, action, threat);

                var tutorialPresenter = GetOrAdd<OSTutorialPresenter>(tutorialPanel.gameObject);
                tutorialPresenter.Configure(session, player, growth, chain, explosion, summary, tutorial);

                var markerRoot = GetOrCreateChild(gameRoot.transform, "ReadabilityFeedback");
                var cutBoundary = CreateLine(markerRoot, "CutBoundary", true, 0.11f, 185);
                var tailDirection = CreateLine(markerRoot, "TailDirection", true, 0.08f, 184);
                var combatFeedback = GetOrAdd<OSCombatFeedbackPresenter>(feedbackPanel.gameObject);
                combatFeedback.Configure(health, chain, explosion, shield, level, feedback, cutBoundary, tailDirection);

                ConfigureSelectionPanel(canvas, "BodyRoleSelectionPanel");
                ConfigureSelectionPanel(canvas, "LevelUpPanel");
                ConfigureHeadReadability(head);
                ConfigureSceneSorting(gameRoot.transform);
                ConfigureResultReadability(canvas);
                HideLegacyHudLabels(canvas);

                var foundation = RequireTransform(canvas, "FoundationLabel").GetComponent<TMP_Text>();
                foundation.text = "STEP 15  |  CONSOLIDATED HUD · CONDITIONAL TUTORIAL · COMBAT READABILITY";
                EditorUtility.SetDirty(foundation);

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

        private static void ConfigureEnemyPrefabs()
        {
            foreach (var path in EnemyPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var enemy = root.GetComponent<OSEnemyController>()
                                ?? throw new InvalidOperationException($"Enemy controller missing in '{path}'.");
                    var controlRing = CreateLine(root.transform, "ControlStatusRing", false, 0.09f, 182);
                    controlRing.loop = false;
                    controlRing.startColor = new Color32(45, 245, 255, 250);
                    controlRing.endColor = new Color32(45, 245, 255, 45);
                    controlRing.enabled = false;
                    enemy.ConfigureReadability(controlRing);

                    var enemySerialized = new SerializedObject(enemy);
                    var telegraph = enemySerialized.FindProperty("telegraphLine").objectReferenceValue as LineRenderer;
                    if (telegraph != null)
                    {
                        telegraph.sortingOrder = 200;
                        EditorUtility.SetDirty(telegraph);
                    }

                    foreach (var line in root.GetComponentsInChildren<LineRenderer>(true))
                    {
                        if (line.name.IndexOf("Telegraph", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            line.sortingOrder = 200;
                        }
                        else if (line.name.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            line.sortingOrder = 175;
                        }
                    }

                    EditorUtility.SetDirty(enemy);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ConfigureHeadReadability(Transform head)
        {
            foreach (var renderer in head.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 220);
                EditorUtility.SetDirty(renderer);
            }

            var ring = CreateLine(head, "CoreReadabilityRing", false, 0.07f, 218);
            ring.loop = true;
            ring.positionCount = 32;
            ring.startColor = ring.endColor = new Color32(140, 242, 255, 230);
            for (var index = 0; index < ring.positionCount; index++)
            {
                var angle = index * Mathf.PI * 2f / ring.positionCount;
                var radius = index % 4 == 0 ? 0.72f : 0.64f;
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            ring.enabled = true;
        }

        private static void ConfigureSceneSorting(Transform gameRoot)
        {
            foreach (var line in gameRoot.GetComponentsInChildren<LineRenderer>(true))
            {
                if (line.name.StartsWith("LaserTelegraph", StringComparison.Ordinal))
                {
                    line.sortingOrder = 198;
                }
                else if (line.name.StartsWith("ExplosionReservation", StringComparison.Ordinal) ||
                         line.transform.parent != null && line.transform.parent.name == "OSExplosionTelegraphView")
                {
                    line.sortingOrder = 190;
                }
                else if (line.name.StartsWith("Shield_", StringComparison.Ordinal))
                {
                    line.sortingOrder = 175;
                }
            }
        }

        private static void ConfigureSelectionPanel(Transform canvas, string path)
        {
            var panel = RequireTransform(canvas, path);
            var buttons = panel.GetComponentsInChildren<Button>(true)
                .OrderBy(button => button.transform.GetSiblingIndex())
                .ToArray();
            var animator = GetOrAdd<OSSelectionPanelAnimator>(panel.gameObject);
            animator.Configure(buttons);
            foreach (var button in buttons)
            {
                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = button.name switch
                    {
                        var name when name.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0 => new Color32(24, 87, 130, 255),
                        var name when name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0 => new Color32(128, 42, 52, 255),
                        var name when name.IndexOf("Laser", StringComparison.OrdinalIgnoreCase) >= 0 => new Color32(87, 43, 129, 255),
                        var name when name.IndexOf("Control", StringComparison.OrdinalIgnoreCase) >= 0 => new Color32(35, 106, 82, 255),
                        _ => new Color32(70, 48, 94, 255)
                    };
                }

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 13f;
                    label.fontSizeMax = 19f;
                }
            }

            EditorUtility.SetDirty(animator);
        }

        private static void ConfigureResultReadability(Transform canvas)
        {
            var panel = RequireTransform(canvas, "ResultPanel");
            var image = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
            image.color = new Color32(8, 12, 24, 248);
            var label = RequireTransform(panel, "Label").GetComponent<TMP_Text>();
            label.color = new Color32(225, 238, 255, 255);
            label.lineSpacing = 7f;
            label.fontSize = 19f;
            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(label);
        }

        private static void HideLegacyHudLabels(Transform canvas)
        {
            var names = new[]
            {
                "HealthLabel", "DamageFeedbackLabel", "BodyGrowthLabel", "RoleCombatStatusLabel",
                "ExplosionStatusLabel", "ExplosionFeedbackLabel", "LevelProgressLabel", "UpgradeFeedbackLabel",
                "WaveStatusLabel", "WaveEventLabel", "BossStatusLabel", "BossPatternLabel"
            };
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!names.Contains(label.name, StringComparer.Ordinal))
                {
                    continue;
                }

                label.enabled = false;
                EditorUtility.SetDirty(label);
            }
        }

        private static Transform GetOrCreateUiObject(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            child = new GameObject(name, typeof(RectTransform)).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform CreatePanel(Transform parent, string name, Color color)
        {
            var panel = GetOrCreateUiObject(parent, name);
            var image = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            EditorUtility.SetDirty(image);
            return panel;
        }

        private static TMP_Text CreateLabel(
            Transform parent,
            string name,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).transform;
                child.SetParent(parent, false);
            }

            Stretch((RectTransform)child, new Vector2(14f, 8f));
            var label = child.GetComponent<TMP_Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            EditorUtility.SetDirty(label);
            return label;
        }

        private static LineRenderer CreateLine(
            Transform parent,
            string name,
            bool worldSpace,
            float width,
            int sortingOrder)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(LineRenderer)).transform;
                child.SetParent(parent, false);
            }

            var line = child.GetComponent<LineRenderer>();
            line.useWorldSpace = worldSpace;
            line.startWidth = width;
            line.endWidth = width;
            line.sortingOrder = sortingOrder;
            line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
            return line;
        }

        private static void Stretch(RectTransform rect, Vector2? padding = null)
        {
            var pad = padding ?? Vector2.zero;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = pad;
            rect.offsetMax = -pad;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetBottomLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetBottomCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static T RequireComponent<T>(Transform root) where T : Component
        {
            return root.GetComponentInChildren<T>(true)
                   ?? throw new InvalidOperationException($"Required component '{typeof(T).Name}' is missing below '{root.name}'.");
        }

        private static bool HasStep14Foundation()
        {
            var scene = SceneManager.GetSceneByPath(GameScenePath);
            var opened = !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }

            try
            {
                return scene.GetRootGameObjects()
                    .Any(root => root.GetComponentInChildren<OSRunSummaryController>(true) != null);
            }
            finally
            {
                if (opened && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name)
                   ?? throw new InvalidOperationException($"Root '{name}' is missing from '{scene.path}'.");
        }

        private static Transform RequireTransform(Transform parent, string path)
        {
            return parent.Find(path)
                   ?? throw new InvalidOperationException($"Transform '{parent.name}/{path}' is missing.");
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            return target.GetComponent<T>() ?? target.AddComponent<T>();
        }
    }
}
