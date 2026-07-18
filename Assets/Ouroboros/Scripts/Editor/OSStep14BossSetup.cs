using System;
using System.IO;
using System.Linq;
using Ouroboros.Core;
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
    public static class OSStep14BossSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string EncounterPath = "Assets/Ouroboros/Data/Enemies/OSEncounterBalance.asset";
        private const string WavePath = "Assets/Ouroboros/Data/Waves/OSWaveSchedule.asset";
        private const string ChaserPrefabPath = "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_Chaser.prefab";
        private const string BossPrefabPath = "Assets/Ouroboros/Prefabs/Enemies/PF_Boss_SwarmCore.prefab";

        [MenuItem("Ouroboros/Setup/Apply Step 14 Boss And Results")]
        public static void ApplyStep14BossAndResults()
        {
            if (!HasStep13Foundation())
            {
                OSStep13WaveSetup.ApplyStep13TimedWaves();
            }

            var encounter = LoadRequired<OSEncounterBalanceData>(EncounterPath);
            var waves = LoadRequired<OSWaveScheduleData>(WavePath);
            var bossPrefab = CreateBossPrefab(encounter);
            AssignBossPrefab(encounter, bossPrefab);
            ConfigureScene(encounter, waves, bossPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 14 boss, result summary and restart flow applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 14 WebGL")]
        public static void BuildStep14WebGL()
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step14", "WebGL"));
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
                    $"Step 14 WebGL build failed: {summary.result}, errors {summary.totalErrors}, " +
                    $"warnings {summary.totalWarnings}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 14 WebGL succeeded: {outputPath}, " +
                $"errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static OSEnemyController CreateBossPrefab(OSEncounterBalanceData encounter)
        {
            var root = PrefabUtility.LoadPrefabContents(ChaserPrefabPath);
            try
            {
                root.name = "PF_Boss_SwarmCore";
                root.transform.localScale = Vector3.one * 1.8f;
                var renderer = root.GetComponent<SpriteRenderer>()
                               ?? throw new InvalidOperationException("Boss SpriteRenderer is missing.");
                renderer.color = new Color32(255, 54, 124, 255);
                renderer.sortingOrder = 7;

                var enemy = root.GetComponent<OSEnemyController>()
                            ?? throw new InvalidOperationException("Boss enemy controller is missing.");
                var boss = root.GetComponent<OSBossController>() ?? root.AddComponent<OSBossController>();
                var pattern = CreateLine(root.transform, "BossPatternTelegraph", true, 0.11f, 11);
                pattern.enabled = false;
                var shield = CreateLine(root.transform, "BossShieldRing", false, 0.13f, 9);
                shield.loop = true;
                shield.positionCount = 64;
                shield.startColor = new Color(0.18f, 0.68f, 1f, 0.9f);
                shield.endColor = shield.startColor;
                var localRadius = 1.45f;
                for (var index = 0; index < shield.positionCount; index++)
                {
                    var angle = index * Mathf.PI * 2f / shield.positionCount;
                    shield.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * localRadius);
                }
                shield.enabled = false;

                var enemySerialized = new SerializedObject(enemy);
                enemySerialized.FindProperty("encounterBalance").objectReferenceValue = encounter;
                enemySerialized.FindProperty("definitionId").stringValue = "boss_swarm_core";
                enemySerialized.FindProperty("bodyRenderer").objectReferenceValue = renderer;
                enemySerialized.FindProperty("telegraphLine").objectReferenceValue = null;
                enemySerialized.ApplyModifiedPropertiesWithoutUndo();

                var bossSerialized = new SerializedObject(boss);
                bossSerialized.FindProperty("enemy").objectReferenceValue = enemy;
                bossSerialized.FindProperty("patternTelegraph").objectReferenceValue = pattern;
                bossSerialized.FindProperty("shieldRing").objectReferenceValue = shield;
                bossSerialized.ApplyModifiedPropertiesWithoutUndo();

                var saved = PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath)
                            ?? throw new InvalidOperationException("Unable to save the Swarm Core prefab.");
                return saved.GetComponent<OSEnemyController>();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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

        private static void AssignBossPrefab(
            OSEncounterBalanceData encounter,
            OSEnemyController bossPrefab)
        {
            var serialized = new SerializedObject(encounter);
            serialized.FindProperty("bossDefinition")
                .FindPropertyRelative("prefab").objectReferenceValue = bossPrefab.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(encounter);
        }

        private static void ConfigureScene(
            OSEncounterBalanceData encounter,
            OSWaveScheduleData waves,
            OSEnemyController bossPrefab)
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
                var session = systems.GetComponentInChildren<OSGameSessionController>(true)
                              ?? throw new InvalidOperationException("Session controller is missing.");
                var pools = systems.GetComponentInChildren<OSPoolRegistry>(true)
                            ?? throw new InvalidOperationException("Pool registry is missing.");
                var poolContext = pools.GetComponent<OSEnemyPoolContext>()
                                  ?? throw new InvalidOperationException("Enemy pool context is missing.");
                var registry = systems.GetComponentInChildren<OSEnemyRegistry>(true);
                var waveDirector = systems.GetComponentInChildren<OSWaveDirector>(true);
                var player = head.GetComponent<OSPlayerController>();
                var health = head.GetComponent<OSPlayerHealth>();
                var chain = gameRoot.GetComponentInChildren<OSBodyChain>(true);
                var roles = systems.GetComponentInChildren<OSBodyRoleRegistry>(true);
                var explosion = systems.GetComponentInChildren<OSExplosionController>(true);
                var level = systems.GetComponentInChildren<OSLevelUpController>(true);
                var pickups = systems.GetComponentInChildren<OSPickupSpawner>(true);
                var resolver = systems.GetComponentInChildren<OSPlayerCombatResolver>(true);

                var bossRoot = GetOrCreateChild(systems, "OSBossEncounterSystem");
                var bossEncounter = GetOrAdd<OSBossEncounterController>(bossRoot.gameObject);
                bossEncounter.Configure(session, pools, player, 90f);
                EditorUtility.SetDirty(bossEncounter);

                var summaryRoot = GetOrCreateChild(systems, "OSRunSummarySystem");
                var runSummary = GetOrAdd<OSRunSummaryController>(summaryRoot.gameObject);
                runSummary.Configure(
                    session,
                    health,
                    chain,
                    roles,
                    explosion,
                    level,
                    encounter,
                    waves);
                EditorUtility.SetDirty(runSummary);

                AssignObject(waveDirector, "bossEncounter", bossEncounter);
                UpsertPoolEntry(pools, "boss_swarm_core", bossPrefab, 1);
                poolContext.Configure(
                    registry,
                    session,
                    head,
                    pickups,
                    resolver,
                    waveDirector,
                    bossEncounter,
                    player,
                    runSummary);
                EditorUtility.SetDirty(poolContext);

                ConfigureBossHud(canvas, bossEncounter);
                ConfigureResultPanel(canvas, session, runSummary);
                var foundation = RequireTransform(canvas, "FoundationLabel").GetComponent<TMP_Text>();
                foundation.text = "STEP 14  |  SWARM CORE · 90s BOSS LIMIT · COMPLETE RUN SUMMARY";
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

        private static void ConfigureBossHud(Transform canvas, OSBossEncounterController encounter)
        {
            var font = RequireTransform(canvas, "FoundationLabel").GetComponent<TMP_Text>().font;
            var health = CreateText(
                canvas,
                "BossStatusLabel",
                string.Empty,
                font,
                22f,
                new Color32(255, 92, 148, 255));
            SetTopCenter(health.rectTransform, new Vector2(0f, -88f), new Vector2(980f, 34f));
            var pattern = CreateText(
                canvas,
                "BossPatternLabel",
                string.Empty,
                font,
                20f,
                new Color32(255, 194, 74, 255));
            SetTopCenter(pattern.rectTransform, new Vector2(0f, -120f), new Vector2(980f, 32f));
            var presenter = GetOrAdd<OSBossPresenter>(health.gameObject);
            presenter.Configure(encounter, health, pattern);
            EditorUtility.SetDirty(presenter);
        }

        private static void ConfigureResultPanel(
            Transform canvas,
            OSGameSessionController session,
            OSRunSummaryController summary)
        {
            var panel = RequireTransform(canvas, "ResultPanel");
            var panelRect = (RectTransform)panel;
            panelRect.sizeDelta = new Vector2(900f, 650f);
            var label = RequireTransform(panel, "Label").GetComponent<TMP_Text>();
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -32f);
            label.rectTransform.sizeDelta = new Vector2(840f, 510f);
            label.text = "FINALIZING SESSION RESULT...";
            EditorUtility.SetDirty(label);

            var restart = CreateButton(panel, "RestartButton", "RESTART RUN", new Vector2(-170f, 34f));
            var menu = CreateButton(panel, "MainMenuButton", "MAIN MENU", new Vector2(170f, 34f));
            restart.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = menu,
                selectOnLeft = menu
            };
            menu.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = restart,
                selectOnLeft = restart
            };

            var result = GetOrAdd<OSResultPanel>(panel.gameObject);
            result.Configure(session, summary, label, restart, menu);
            EditorUtility.SetDirty(result);

            var levelProgress = canvas.GetComponentInChildren<OSLevelProgressPresenter>(true);
            if (levelProgress != null)
            {
                AssignObject(levelProgress, "resultLabel", null);
            }
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string text,
            Vector2 position)
        {
            var target = parent.Find(name);
            if (target == null)
            {
                target = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button)).transform;
                target.SetParent(parent, false);
            }

            var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(290f, 58f);
            var image = target.GetComponent<Image>();
            image.color = new Color32(114, 38, 73, 255);
            var button = target.GetComponent<Button>();
            button.targetGraphic = image;

            var labelTransform = target.Find("Label");
            if (labelTransform == null)
            {
                labelTransform = new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI)).transform;
                labelTransform.SetParent(target, false);
            }

            var labelRect = (RectTransform)labelTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelTransform.GetComponent<TMP_Text>();
            label.font = RequireTransform(parent.parent, "FoundationLabel").GetComponent<TMP_Text>().font;
            label.text = text;
            label.fontSize = 19f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return button;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float fontSize,
            Color color)
        {
            var target = parent.Find(name);
            if (target == null)
            {
                target = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI)).transform;
                target.SetParent(parent, false);
            }

            var label = target.GetComponent<TMP_Text>();
            label.text = text;
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static void SetTopCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            EditorUtility.SetDirty(rect);
        }

        private static void UpsertPoolEntry(
            OSPoolRegistry registry,
            string key,
            OSPoolableBehaviour prefab,
            int capacity)
        {
            var serialized = new SerializedObject(registry);
            var entries = serialized.FindProperty("entries");
            var index = -1;
            for (var entryIndex = 0; entryIndex < entries.arraySize; entryIndex++)
            {
                if (entries.GetArrayElementAtIndex(entryIndex)
                        .FindPropertyRelative("key").stringValue == key)
                {
                    index = entryIndex;
                    break;
                }
            }

            if (index < 0)
            {
                index = entries.arraySize;
                entries.arraySize++;
            }

            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("key").stringValue = key;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("capacity").intValue = Mathf.Max(1, capacity);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
        }

        private static bool HasStep13Foundation()
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
                    .Any(root => root.GetComponentInChildren<OSWaveDirector>(true) != null);
            }
            finally
            {
                if (opened && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path)
                   ?? throw new InvalidOperationException($"Required asset is missing at '{path}'.");
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

        private static void AssignObject(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
