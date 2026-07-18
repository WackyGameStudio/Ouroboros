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

namespace Ouroboros.Editor
{
    public static class OSStep15ArenaCollisionSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string HeadProjectilePath =
            "Assets/Ouroboros/Prefabs/Projectiles/PF_HeadProjectile.prefab";
        private const string ControlProjectilePath =
            "Assets/Ouroboros/Prefabs/Projectiles/PF_ControlProjectile.prefab";
        private const string EnemyProjectilePath =
            "Assets/Ouroboros/Prefabs/Projectiles/PF_EnemyProjectile.prefab";

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

        [MenuItem("Ouroboros/Setup/Apply Step 15.1 Arena Collision")]
        public static void ApplyStep15ArenaCollision()
        {
            if (!HasStep15Foundation())
            {
                OSStep15ReadabilitySetup.ApplyStep15Readability();
            }

            OSStep04MovementSetup.ApplyStep04MovementFoundation();

            var worldBlockerLayer = RequireLayer("WorldBlocker");
            var enemyBodyLayer = RequireLayer("EnemyBody");
            var enemyHurtboxLayer = RequireLayer("EnemyHurtbox");
            var playerHeadHurtboxLayer = RequireLayer("PlayerHeadHurtbox");
            var playerBodyHurtboxLayer = RequireLayer("PlayerBodyHurtbox");
            var playerProjectileLayer = RequireLayer("PlayerProjectile");
            var enemyProjectileLayer = EnsureLayer("EnemyProjectile");

            ConfigureCollisionMatrix(
                worldBlockerLayer,
                enemyBodyLayer,
                enemyHurtboxLayer,
                playerHeadHurtboxLayer,
                playerBodyHurtboxLayer,
                playerProjectileLayer,
                enemyProjectileLayer);
            ConfigureEnemyPrefabs(worldBlockerLayer);
            ConfigurePlayerProjectile<OSProjectile>(
                HeadProjectilePath,
                playerProjectileLayer,
                worldBlockerLayer);
            ConfigurePlayerProjectile<OSControlProjectile>(
                ControlProjectilePath,
                playerProjectileLayer,
                worldBlockerLayer);
            ConfigureEnemyProjectile(enemyProjectileLayer, worldBlockerLayer);
            ConfigureSceneLabel();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 15.1 expanded arena and solid blocker contract applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 15.1 WebGL")]
        public static void BuildStep15ArenaCollisionWebGL()
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step15_1", "WebGL"));
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
                    $"Step 15.1 WebGL build failed: {summary.result}, errors {summary.totalErrors}, " +
                    $"warnings {summary.totalWarnings}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 15.1 WebGL succeeded: {outputPath}, " +
                $"errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static void ConfigureCollisionMatrix(
            int worldBlockerLayer,
            int enemyBodyLayer,
            int enemyHurtboxLayer,
            int playerHeadHurtboxLayer,
            int playerBodyHurtboxLayer,
            int playerProjectileLayer,
            int enemyProjectileLayer)
        {
            Physics2D.IgnoreLayerCollision(enemyBodyLayer, worldBlockerLayer, false);

            for (var other = 0; other < 32; other++)
            {
                Physics2D.IgnoreLayerCollision(playerProjectileLayer, other, true);
                Physics2D.IgnoreLayerCollision(enemyProjectileLayer, other, true);
            }

            Physics2D.IgnoreLayerCollision(playerProjectileLayer, enemyHurtboxLayer, false);
            Physics2D.IgnoreLayerCollision(playerProjectileLayer, worldBlockerLayer, false);
            Physics2D.IgnoreLayerCollision(enemyProjectileLayer, playerHeadHurtboxLayer, false);
            Physics2D.IgnoreLayerCollision(enemyProjectileLayer, playerBodyHurtboxLayer, false);
            Physics2D.IgnoreLayerCollision(enemyProjectileLayer, worldBlockerLayer, false);
        }

        private static void ConfigureEnemyPrefabs(int worldBlockerLayer)
        {
            for (var index = 0; index < EnemyPrefabPaths.Length; index++)
            {
                var path = EnemyPrefabPaths[index];
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var body = root.GetComponent<Rigidbody2D>()
                               ?? throw new InvalidOperationException($"Enemy Rigidbody2D is missing at '{path}'.");
                    var collider = root.GetComponent<CircleCollider2D>()
                                   ?? throw new InvalidOperationException(
                                       $"Enemy body CircleCollider2D is missing at '{path}'.");
                    var enemy = root.GetComponent<OSEnemyController>()
                                ?? throw new InvalidOperationException(
                                    $"OSEnemyController is missing at '{path}'.");
                    body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                    var serialized = new SerializedObject(enemy);
                    serialized.FindProperty("body").objectReferenceValue = body;
                    serialized.FindProperty("bodyCollider").objectReferenceValue = collider;
                    serialized.FindProperty("worldBlockerMask").intValue = 1 << worldBlockerLayer;
                    serialized.FindProperty("reclaimDistance").floatValue = 60f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ConfigurePlayerProjectile<T>(
            string path,
            int projectileLayer,
            int worldBlockerLayer)
            where T : Component
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.layer = projectileLayer;
                var body = root.GetComponent<Rigidbody2D>()
                           ?? throw new InvalidOperationException($"Projectile Rigidbody2D is missing at '{path}'.");
                var collider = root.GetComponent<Collider2D>()
                               ?? throw new InvalidOperationException($"Projectile Collider2D is missing at '{path}'.");
                var projectile = root.GetComponent<T>()
                                 ?? throw new InvalidOperationException(
                                     $"Projectile component '{typeof(T).Name}' is missing at '{path}'.");
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                var serialized = new SerializedObject(projectile);
                serialized.FindProperty("body").objectReferenceValue = body;
                serialized.FindProperty("projectileCollider").objectReferenceValue = collider;
                serialized.FindProperty("worldBlockerMask").intValue = 1 << worldBlockerLayer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureEnemyProjectile(int projectileLayer, int worldBlockerLayer)
        {
            var root = PrefabUtility.LoadPrefabContents(EnemyProjectilePath);
            try
            {
                root.layer = projectileLayer;
                var body = root.GetComponent<Rigidbody2D>()
                           ?? throw new InvalidOperationException("Enemy projectile Rigidbody2D is missing.");
                var collider = root.GetComponent<Collider2D>()
                               ?? throw new InvalidOperationException("Enemy projectile Collider2D is missing.");
                var projectile = root.GetComponent<OSEnemyProjectile>()
                                 ?? throw new InvalidOperationException("OSEnemyProjectile is missing.");
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                var serialized = new SerializedObject(projectile);
                serialized.FindProperty("body").objectReferenceValue = body;
                serialized.FindProperty("projectileCollider").objectReferenceValue = collider;
                serialized.FindProperty("worldBlockerMask").intValue = 1 << worldBlockerLayer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, EnemyProjectilePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureSceneLabel()
        {
            var scene = SceneManager.GetSceneByPath(GameScenePath);
            var openedForSetup = !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var gameRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "GameRoot")
                               ?? throw new InvalidOperationException("GameRoot is missing.");
                var head = gameRoot.transform.Find("PlayerRoot/Head")
                           ?? throw new InvalidOperationException("Player head is missing.");
                foreach (var renderer in head.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 220);
                    EditorUtility.SetDirty(renderer);
                }

                var label = gameRoot.transform.Find("Canvas/FoundationLabel")?.GetComponent<TMP_Text>()
                            ?? throw new InvalidOperationException("FoundationLabel is missing.");
                label.text = "STEP 15.1  |  48×30 ARENA · SOLID WORLD BLOCKERS";
                EditorUtility.SetDirty(label);
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

        private static bool HasStep15Foundation()
        {
            var scene = SceneManager.GetSceneByPath(GameScenePath);
            var openedForSetup = !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }

            try
            {
                return scene.GetRootGameObjects()
                    .Any(root => root.GetComponentInChildren<OSCombatHudPresenter>(true) != null);
            }
            finally
            {
                if (openedForSetup && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static int RequireLayer(string name)
        {
            var layer = LayerMask.NameToLayer(name);
            return layer >= 0
                ? layer
                : throw new InvalidOperationException($"Required layer '{name}' is missing.");
        }

        private static int EnsureLayer(string layerName)
        {
            var existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
            {
                return existing;
            }

            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets.Length == 0)
            {
                throw new InvalidOperationException("Unable to load ProjectSettings/TagManager.asset.");
            }

            var serialized = new SerializedObject(tagManagerAssets[0]);
            var layers = serialized.FindProperty("layers")
                         ?? throw new InvalidOperationException("TagManager layers property is missing.");
            for (var index = 8; index < layers.arraySize; index++)
            {
                var layer = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = layerName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return index;
            }

            throw new InvalidOperationException($"No free user layer is available for '{layerName}'.");
        }
    }
}
