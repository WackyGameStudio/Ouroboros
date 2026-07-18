using System;
using System.Collections.Generic;
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

namespace Ouroboros.Editor
{
    public static class OSStep13WaveSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string EncounterPath = "Assets/Ouroboros/Data/Enemies/OSEncounterBalance.asset";
        private const string WavePath = "Assets/Ouroboros/Data/Waves/OSWaveSchedule.asset";
        private const string ChaserPrefabPath = "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_Chaser.prefab";
        private const string ProjectileSpritePath = "Assets/Ouroboros/Art/Placeholders/Projectile.png";
        private const string EnemyProjectilePath =
            "Assets/Ouroboros/Prefabs/Projectiles/PF_EnemyProjectile.prefab";

        private static readonly EnemyPrefabSpec[] EnemySpecs =
        {
            new("enemy_chaser", ChaserPrefabPath, new Color32(255, 113, 138, 255), 0.76f, false, false),
            new("enemy_charger", "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_Charger.prefab",
                new Color32(255, 158, 72, 255), 0.92f, true, false),
            new("enemy_shooter", "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_Shooter.prefab",
                new Color32(83, 220, 255, 255), 0.72f, true, false),
            new("enemy_splitter", "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_Splitter.prefab",
                new Color32(183, 104, 255, 255), 0.86f, false, false),
            new("enemy_splitter_spawn", "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_SplitterSpawn.prefab",
                new Color32(225, 164, 255, 255), 0.5f, false, false),
            new("enemy_elite_accelerator", "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_EliteAccelerator.prefab",
                new Color32(255, 213, 74, 255), 1.16f, false, true)
        };

        [MenuItem("Ouroboros/Setup/Apply Step 13 Timed Waves")]
        public static void ApplyStep13TimedWaves()
        {
            if (!HasStep12Foundation())
            {
                OSStep12LevelUpSetup.ApplyStep12LevelUpAndUpgrades();
            }

            var encounter = LoadRequired<OSEncounterBalanceData>(EncounterPath);
            var waves = LoadRequired<OSWaveScheduleData>(WavePath);
            var enemyHitboxLayer = RequireLayer("EnemyHitbox");
            var prefabs = CreateEnemyPrefabs(encounter);
            var enemyProjectile = CreateEnemyProjectilePrefab(enemyHitboxLayer);
            ConfigureEncounterPrefabs(encounter, prefabs);
            ConfigureScene(encounter, waves, prefabs, enemyProjectile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 13 timed waves and enemy archetypes applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 13 WebGL")]
        public static void BuildStep13WebGL()
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step13", "WebGL"));
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
                    $"Step 13 WebGL build failed: {summary.result}, errors {summary.totalErrors}, " +
                    $"warnings {summary.totalWarnings}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 13 WebGL succeeded: {outputPath}, " +
                $"errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static Dictionary<string, OSEnemyController> CreateEnemyPrefabs(
            OSEncounterBalanceData encounter)
        {
            var prefabs = new Dictionary<string, OSEnemyController>(StringComparer.Ordinal);
            for (var index = 0; index < EnemySpecs.Length; index++)
            {
                var spec = EnemySpecs[index];
                var root = PrefabUtility.LoadPrefabContents(ChaserPrefabPath);
                try
                {
                    root.name = Path.GetFileNameWithoutExtension(spec.Path);
                    root.transform.localScale = Vector3.one * spec.Scale;
                    var renderer = root.GetComponent<SpriteRenderer>()
                                   ?? throw new InvalidOperationException("Enemy SpriteRenderer is missing.");
                    renderer.color = spec.Color;

                    var controller = root.GetComponent<OSEnemyController>()
                                     ?? throw new InvalidOperationException("Enemy controller is missing.");
                    var telegraph = ConfigureTelegraph(root, spec.HasTelegraph, spec.Color);
                    ConfigureAura(root, spec.HasAura);

                    var serialized = new SerializedObject(controller);
                    serialized.FindProperty("encounterBalance").objectReferenceValue = encounter;
                    serialized.FindProperty("definitionId").stringValue = spec.Id;
                    serialized.FindProperty("bodyRenderer").objectReferenceValue = renderer;
                    serialized.FindProperty("telegraphLine").objectReferenceValue = telegraph;
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    var saved = PrefabUtility.SaveAsPrefabAsset(root, spec.Path)
                                ?? throw new InvalidOperationException(
                                    $"Unable to save enemy prefab '{spec.Path}'.");
                    prefabs.Add(spec.Id, saved.GetComponent<OSEnemyController>());
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return prefabs;
        }

        private static LineRenderer ConfigureTelegraph(GameObject root, bool required, Color color)
        {
            var line = root.GetComponent<LineRenderer>();
            if (!required)
            {
                if (line != null)
                {
                    UnityEngine.Object.DestroyImmediate(line);
                }

                return null;
            }

            if (line == null)
            {
                line = root.AddComponent<LineRenderer>();
            }
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.08f;
            line.endWidth = 0.025f;
            line.startColor = new Color(color.r, color.g, color.b, 0.92f);
            line.endColor = new Color(1f, 1f, 1f, 0.25f);
            line.sortingOrder = 6;
            line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
            line.enabled = false;
            return line;
        }

        private static void ConfigureAura(GameObject root, bool required)
        {
            var aura = root.transform.Find("AccelerationAura");
            if (!required)
            {
                if (aura != null)
                {
                    UnityEngine.Object.DestroyImmediate(aura.gameObject);
                }

                return;
            }

            if (aura == null)
            {
                aura = new GameObject("AccelerationAura", typeof(LineRenderer)).transform;
                aura.SetParent(root.transform, false);
            }

            var line = aura.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 64;
            line.startWidth = 0.07f;
            line.endWidth = 0.07f;
            line.startColor = new Color(1f, 0.76f, 0.12f, 0.65f);
            line.endColor = line.startColor;
            line.sortingOrder = 1;
            line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
            var localRadius = 4.5f / Mathf.Max(0.01f, root.transform.localScale.x);
            for (var index = 0; index < line.positionCount; index++)
            {
                var angle = index * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(index, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * localRadius);
            }
        }

        private static OSEnemyProjectile CreateEnemyProjectilePrefab(int layer)
        {
            var sprite = LoadRequired<Sprite>(ProjectileSpritePath);
            var root = new GameObject(
                "PF_EnemyProjectile",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(OSEnemyProjectile));
            try
            {
                root.layer = layer;
                root.transform.localScale = new Vector3(0.28f, 0.28f, 1f);
                var renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color32(92, 226, 255, 255);
                renderer.sortingOrder = 5;
                var body = root.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.useFullKinematicContacts = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                var collider = root.GetComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.34f;

                var projectile = root.GetComponent<OSEnemyProjectile>();
                var serialized = new SerializedObject(projectile);
                serialized.FindProperty("body").objectReferenceValue = body;
                serialized.FindProperty("moveSpeed").floatValue = 6f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var saved = PrefabUtility.SaveAsPrefabAsset(root, EnemyProjectilePath)
                            ?? throw new InvalidOperationException(
                                $"Unable to save enemy projectile at '{EnemyProjectilePath}'.");
                return saved.GetComponent<OSEnemyProjectile>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureEncounterPrefabs(
            OSEncounterBalanceData encounter,
            IReadOnlyDictionary<string, OSEnemyController> prefabs)
        {
            var serialized = new SerializedObject(encounter);
            var definitions = serialized.FindProperty("enemyDefinitions");
            for (var index = 0; index < definitions.arraySize; index++)
            {
                var definition = definitions.GetArrayElementAtIndex(index);
                var id = definition.FindPropertyRelative("id").stringValue;
                if (prefabs.TryGetValue(id, out var prefab))
                {
                    definition.FindPropertyRelative("prefab").objectReferenceValue = prefab.gameObject;
                }
            }

            var elite = serialized.FindProperty("eliteDefinition");
            if (prefabs.TryGetValue(elite.FindPropertyRelative("id").stringValue, out var elitePrefab))
            {
                elite.FindPropertyRelative("prefab").objectReferenceValue = elitePrefab.gameObject;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(encounter);
        }

        private static void ConfigureScene(
            OSEncounterBalanceData encounter,
            OSWaveScheduleData waves,
            IReadOnlyDictionary<string, OSEnemyController> enemyPrefabs,
            OSEnemyProjectile enemyProjectile)
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
                var world = RequireTransform(gameRoot.transform, "World");
                var head = RequireTransform(gameRoot.transform, "PlayerRoot/Head");
                var canvas = RequireTransform(gameRoot.transform, "Canvas");
                var session = systems.GetComponentInChildren<OSGameSessionController>(true)
                              ?? throw new InvalidOperationException("Session controller is missing.");
                var registry = systems.GetComponentInChildren<OSEnemyRegistry>(true)
                               ?? throw new InvalidOperationException("Enemy registry is missing.");
                var pools = systems.GetComponentInChildren<OSPoolRegistry>(true)
                            ?? throw new InvalidOperationException("Pool registry is missing.");
                var poolContext = pools.GetComponent<OSEnemyPoolContext>()
                                  ?? throw new InvalidOperationException("Enemy pool context is missing.");
                var pickups = systems.GetComponentInChildren<OSPickupSpawner>(true);
                var resolver = systems.GetComponentInChildren<OSPlayerCombatResolver>(true);
                var player = head.GetComponent<OSPlayerController>();
                var camera = gameRoot.GetComponentInChildren<Camera>(true);

                var waveRoot = GetOrCreateChild(systems, "OSWaveSystem");
                var director = GetOrAdd<OSWaveDirector>(waveRoot.gameObject);
                AssignObject(director, "waveSchedule", waves);
                AssignObject(director, "encounterBalance", encounter);
                AssignInt(director, "runSeed", 13013);
                AssignObject(director, "sessionController", session);
                AssignObject(director, "poolRegistry", pools);
                AssignObject(director, "enemyRegistry", registry);
                AssignObject(director, "playerTarget", head);
                AssignObject(director, "playerController", player);
                AssignObject(director, "gameplayCamera", camera);
                AssignInt(director, "worldBlockerMask", 1 << RequireLayer("WorldBlocker"));

                poolContext.Configure(registry, session, head, pickups, resolver, director);
                EditorUtility.SetDirty(poolContext);

                foreach (var pair in enemyPrefabs)
                {
                    UpsertPoolEntry(pools, pair.Key, pair.Value, FindCapacity(encounter, pair.Key));
                }

                UpsertPoolEntry(pools, "enemy_projectile", enemyProjectile, 64);

                var debugSpawner = world.GetComponentInChildren<OSEnemyDebugSpawner>(true);
                if (debugSpawner != null)
                {
                    debugSpawner.enabled = false;
                    EditorUtility.SetDirty(debugSpawner);
                }

                ConfigureHud(canvas, director);
                var foundation = RequireTransform(canvas, "FoundationLabel").GetComponent<TMP_Text>();
                foundation.text = "STEP 13  |  10:00 TIMED SWARM · 4 ARCHETYPES · ELITE WAVES";
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

        private static void ConfigureHud(Transform canvas, OSWaveDirector director)
        {
            var combatHud = RequireTransform(canvas, "CombatHUD");
            var font = RequireTransform(canvas, "FoundationLabel").GetComponent<TMP_Text>().font;
            MoveExistingHudLabel(combatHud, canvas, "WaveStatusLabel");
            MoveExistingHudLabel(combatHud, canvas, "WaveEventLabel");
            var wave = CreateOrUpdateText(
                canvas,
                "WaveStatusLabel",
                "TIME 00:00  |  SWARM 0/25  |  CAP 180  |  CHARGER IN 60s",
                font,
                new Vector2(0f, 135f),
                18f,
                new Color32(235, 245, 255, 255));
            var special = CreateOrUpdateText(
                canvas,
                "WaveEventLabel",
                string.Empty,
                font,
                new Vector2(0f, 165f),
                19f,
                new Color32(255, 189, 76, 255));
            SetBottomCenter(wave.rectTransform, new Vector2(0f, 135f), new Vector2(900f, 30f));
            SetBottomCenter(special.rectTransform, new Vector2(0f, 165f), new Vector2(900f, 30f));
            var presenter = GetOrAdd<OSWavePresenter>(wave.gameObject);
            presenter.Configure(director, wave, special);
            EditorUtility.SetDirty(presenter);
        }

        private static TMP_Text CreateOrUpdateText(
            Transform parent,
            string name,
            string value,
            TMP_FontAsset font,
            Vector2 position,
            float fontSize,
            Color color)
        {
            var target = parent.Find(name);
            if (target == null)
            {
                target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI)).transform;
                target.SetParent(parent, false);
            }

            var label = target.GetComponent<TMP_Text>();
            label.text = value;
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(900f, 30f);
            EditorUtility.SetDirty(label);
            return label;
        }

        private static void MoveExistingHudLabel(Transform currentParent, Transform targetParent, string name)
        {
            var existing = currentParent.Find(name);
            if (existing != null)
            {
                existing.SetParent(targetParent, false);
            }
        }

        private static void SetBottomCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            EditorUtility.SetDirty(rect);
        }

        private static int FindCapacity(OSEncounterBalanceData encounter, string id)
        {
            for (var index = 0; index < encounter.EnemyDefinitions.Count; index++)
            {
                var definition = encounter.EnemyDefinitions[index];
                if (definition != null && definition.Id == id)
                {
                    return definition.PoolCapacity;
                }
            }

            return encounter.EliteDefinition != null && encounter.EliteDefinition.Id == id
                ? encounter.EliteDefinition.PoolCapacity
                : 1;
        }

        private static void UpsertPoolEntry(
            OSPoolRegistry registry,
            string key,
            OSPoolableBehaviour prefab,
            int capacity)
        {
            var serialized = new SerializedObject(registry);
            var entries = serialized.FindProperty("entries");
            var entryIndex = -1;
            for (var index = 0; index < entries.arraySize; index++)
            {
                if (entries.GetArrayElementAtIndex(index).FindPropertyRelative("key").stringValue == key)
                {
                    entryIndex = index;
                    break;
                }
            }

            if (entryIndex < 0)
            {
                entryIndex = entries.arraySize;
                entries.arraySize++;
            }

            var entry = entries.GetArrayElementAtIndex(entryIndex);
            entry.FindPropertyRelative("key").stringValue = key;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("capacity").intValue = Mathf.Max(1, capacity);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
        }

        private static bool HasStep12Foundation()
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
                    .Any(root => root.GetComponentInChildren<OSLevelUpController>(true) != null);
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

        private static int RequireLayer(string name)
        {
            var layer = LayerMask.NameToLayer(name);
            return layer >= 0
                ? layer
                : throw new InvalidOperationException($"Required layer '{name}' is missing.");
        }

        private static void AssignObject(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignInt(UnityEngine.Object target, string property, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private readonly struct EnemyPrefabSpec
        {
            public EnemyPrefabSpec(
                string id,
                string path,
                Color color,
                float scale,
                bool hasTelegraph,
                bool hasAura)
            {
                Id = id;
                Path = path;
                Color = color;
                Scale = scale;
                HasTelegraph = hasTelegraph;
                HasAura = hasAura;
            }

            public string Id { get; }
            public string Path { get; }
            public Color Color { get; }
            public float Scale { get; }
            public bool HasTelegraph { get; }
            public bool HasAura { get; }
        }
    }
}
