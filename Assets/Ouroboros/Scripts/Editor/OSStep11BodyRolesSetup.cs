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

namespace Ouroboros.Editor
{
    public static class OSStep11BodyRolesSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string BodyBalancePath = "Assets/Ouroboros/Data/Balance/OSBodyBalance.asset";
        private const string ControlProjectilePrefabPath =
            "Assets/Ouroboros/Prefabs/Projectiles/PF_ControlProjectile.prefab";
        private const string ProjectileSpritePath = "Assets/Ouroboros/Art/Placeholders/Projectile.png";
        private const string ControlProjectilePoolKey = "body_control_projectile";
        private const int RoleCapacity = 64;

        [MenuItem("Ouroboros/Setup/Apply Step 11 Body Roles")]
        public static void ApplyStep11BodyRoles()
        {
            OSStep10BodyDashSetup.ApplyStep10BodyDash();
            var projectileLayer = RequireLayer("PlayerProjectile");
            var worldBlockerLayer = RequireLayer("WorldBlocker");
            var enemyHurtboxLayer = RequireLayer("EnemyHurtbox");
            var bodyBalance = LoadRequired<OSBodyBalanceData>(BodyBalancePath);
            var controlProjectile = CreateOrUpdateControlProjectilePrefab(
                projectileLayer,
                worldBlockerLayer);
            ConfigureScene(bodyBalance, controlProjectile, enemyHurtboxLayer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 11 body roles applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 11 WebGL")]
        public static void BuildStep11WebGL()
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step11", "WebGL"));
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
                    $"Step 11 WebGL build failed: {summary.result}, errors {summary.totalErrors}, " +
                    $"warnings {summary.totalWarnings}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 11 WebGL succeeded: {outputPath}, " +
                $"errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static OSControlProjectile CreateOrUpdateControlProjectilePrefab(
            int projectileLayer,
            int worldBlockerLayer)
        {
            var sprite = LoadRequired<Sprite>(ProjectileSpritePath);
            var root = new GameObject(
                "PF_ControlProjectile",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(OSControlProjectile));

            try
            {
                root.layer = projectileLayer;
                root.transform.localScale = new Vector3(0.32f, 0.32f, 1f);

                var renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color32(95, 231, 165, 255);
                renderer.sortingOrder = 6;

                var body = root.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.useFullKinematicContacts = true;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;

                var collider = root.GetComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.3f;

                var projectile = root.GetComponent<OSControlProjectile>();
                Assign(projectile, "body", body);
                Assign(projectile, "projectileCollider", collider);
                AssignLayerMask(projectile, "worldBlockerMask", 1 << worldBlockerLayer);
                AssignFloat(projectile, "moveSpeed", 9f);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, ControlProjectilePrefabPath)
                             ?? throw new InvalidOperationException(
                                 $"Unable to save control projectile at '{ControlProjectilePrefabPath}'.");
                return prefab.GetComponent<OSControlProjectile>()
                       ?? throw new InvalidOperationException(
                           "Saved control projectile prefab is missing OSControlProjectile.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureScene(
            OSBodyBalanceData bodyBalance,
            OSControlProjectile controlProjectile,
            int enemyHurtboxLayer)
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
                var bodyChain = RequireComponent<OSBodyChain>(gameRoot.transform, "PlayerRoot/BodyChain");
                var session = RequireComponent<OSGameSessionController>(systems, "OSGameSessionController");
                var enemyRegistry = RequireComponent<OSEnemyRegistry>(systems, "OSEnemyRegistry");
                var poolObject = RequireTransform(systems, "OSPoolRegistry").gameObject;
                var pool = poolObject.GetComponent<OSPoolRegistry>()
                           ?? throw new InvalidOperationException("OSPoolRegistry component is missing.");
                var poolRoot = RequireTransform(world, "RuntimePools");
                var resolver = systems.GetComponentInChildren<OSPlayerCombatResolver>(true)
                               ?? throw new InvalidOperationException(
                                   "OSPlayerCombatResolver is missing below Systems.");

                var poolSerialized = new SerializedObject(pool);
                poolSerialized.FindProperty("poolRoot").objectReferenceValue = poolRoot;
                UpsertPoolEntry(
                    poolSerialized.FindProperty("entries"),
                    ControlProjectilePoolKey,
                    controlProjectile,
                    RoleCapacity);
                poolSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pool);

                var roleRoot = GetOrCreateChild(systems, "OSBodyRoleSystems");
                var registry = GetOrAdd<OSBodyRoleRegistry>(roleRoot.gameObject);
                Assign(registry, "bodyChain", bodyChain);

                var attack = GetOrAdd<OSAttackBodyRole>(roleRoot.gameObject);
                AssignCommonRoleReferences(
                    attack,
                    registry,
                    enemyRegistry,
                    pool,
                    session,
                    bodyBalance);
                AssignString(attack, "projectilePoolKey", "head_projectile");

                var laser = GetOrAdd<OSLaserBodyRole>(roleRoot.gameObject);
                Assign(laser, "roleRegistry", registry);
                Assign(laser, "enemyRegistry", enemyRegistry);
                Assign(laser, "sessionController", session);
                Assign(laser, "bodyBalance", bodyBalance);
                AssignLayerMask(laser, "enemyHurtboxMask", 1 << enemyHurtboxLayer);

                var control = GetOrAdd<OSControlBodyRole>(roleRoot.gameObject);
                AssignCommonRoleReferences(
                    control,
                    registry,
                    enemyRegistry,
                    pool,
                    session,
                    bodyBalance);
                AssignString(control, "projectilePoolKey", ControlProjectilePoolKey);

                var shield = GetOrAdd<OSShieldBodyRole>(roleRoot.gameObject);
                Assign(shield, "roleRegistry", registry);
                Assign(shield, "sessionController", session);
                Assign(shield, "bodyBalance", bodyBalance);

                var viewRoot = GetOrCreateChild(roleRoot, "RoleViews");
                ClearChildren(viewRoot);
                var headRenderer = head.GetComponentInChildren<SpriteRenderer>(true);
                var material = headRenderer != null ? headRenderer.sharedMaterial : null;
                var laserViews = CreateLaserViews(viewRoot, material);
                var shieldViews = CreateShieldViews(viewRoot, material);
                AssignArray(laser, "telegraphViews", laserViews);
                AssignArray(shield, "rangeViews", shieldViews);

                Assign(resolver, "shieldBodyRole", shield);
                ConfigureHud(canvas, attack, laser, control, shield);

                var oldDebugPresenter = systems.GetComponent<OSCombatDebugPresenter>();
                if (oldDebugPresenter != null)
                {
                    oldDebugPresenter.enabled = false;
                    EditorUtility.SetDirty(oldDebugPresenter);
                }

                var foundationLabel = RequireComponent<TMP_Text>(canvas, "FoundationLabel");
                foundationLabel.text = "STEP 11  |  ATTACK · LASER · CONTROL · SHIELD ONLINE";
                EditorUtility.SetDirty(foundationLabel);

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

        private static void AssignCommonRoleReferences(
            UnityEngine.Object target,
            OSBodyRoleRegistry registry,
            OSEnemyRegistry enemies,
            OSPoolRegistry pool,
            OSGameSessionController session,
            OSBodyBalanceData balance)
        {
            Assign(target, "roleRegistry", registry);
            Assign(target, "enemyRegistry", enemies);
            Assign(target, "poolRegistry", pool);
            Assign(target, "sessionController", session);
            Assign(target, "bodyBalance", balance);
        }

        private static LineRenderer[] CreateLaserViews(Transform parent, Material material)
        {
            var root = GetOrCreateChild(parent, "LaserTelegraphs");
            var views = new LineRenderer[RoleCapacity];
            for (var index = 0; index < views.Length; index++)
            {
                var target = new GameObject($"Laser_{index:00}", typeof(LineRenderer));
                target.transform.SetParent(root, false);
                var line = target.GetComponent<LineRenderer>();
                line.enabled = false;
                line.sharedMaterial = material;
                line.sortingOrder = 125;
                line.startColor = new Color32(225, 125, 255, 180);
                line.endColor = new Color32(255, 76, 190, 220);
                line.numCapVertices = 2;
                views[index] = line;
            }

            return views;
        }

        private static LineRenderer[] CreateShieldViews(Transform parent, Material material)
        {
            var root = GetOrCreateChild(parent, "ShieldRanges");
            var views = new LineRenderer[RoleCapacity];
            for (var index = 0; index < views.Length; index++)
            {
                var target = new GameObject($"Shield_{index:00}", typeof(LineRenderer));
                target.transform.SetParent(root, false);
                var line = target.GetComponent<LineRenderer>();
                line.enabled = false;
                line.sharedMaterial = material;
                line.sortingOrder = 115;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                views[index] = line;
            }

            return views;
        }

        private static void ConfigureHud(
            Transform canvas,
            OSAttackBodyRole attack,
            OSLaserBodyRole laser,
            OSControlBodyRole control,
            OSShieldBodyRole shield)
        {
            var combatHud = RequireTransform(canvas, "CombatHUD");
            var font = RequireComponent<TMP_Text>(canvas, "FoundationLabel").font;
            var labelTransform = GetOrCreateRectChild(combatHud, "RoleCombatStatusLabel");
            var label = GetOrAdd<TextMeshProUGUI>(labelTransform.gameObject);
            label.font = font;
            label.text = "ROLE FX  |  A 0:0  L 0:0  C 0:0  S 0/0";
            label.fontSize = 17f;
            label.color = new Color32(187, 239, 255, 255);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            SetRect(
                (RectTransform)labelTransform,
                new Vector2(0f, -82f),
                new Vector2(620f, 30f));

            var presenter = GetOrAdd<OSBodyRoleCombatPresenter>(label.gameObject);
            presenter.Configure(attack, laser, control, shield, label);
            EditorUtility.SetDirty(label);
            EditorUtility.SetDirty(presenter);
        }

        private static void UpsertPoolEntry(
            SerializedProperty entries,
            string key,
            OSPoolableBehaviour prefab,
            int capacity)
        {
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
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path)
                   ?? throw new InvalidOperationException($"Required asset is missing at '{path}'.");
        }

        private static int RequireLayer(string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            return layer >= 0
                ? layer
                : throw new InvalidOperationException($"Required layer '{layerName}' is missing.");
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            throw new InvalidOperationException($"Root '{name}' is missing from '{scene.path}'.");
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

        private static Transform GetOrCreateRectChild(Transform parent, string name)
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

        private static void ClearChildren(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
            }
        }

        private static T RequireComponent<T>(Transform parent, string path) where T : Component
        {
            var target = RequireTransform(parent, path);
            return target.GetComponent<T>()
                   ?? throw new InvalidOperationException(
                       $"Component '{typeof(T).Name}' is missing from '{parent.name}/{path}'.");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Assign(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                           ?? throw new InvalidOperationException(
                               $"Serialized property '{propertyName}' is missing from '{target.name}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignFloat(UnityEngine.Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                           ?? throw new InvalidOperationException(
                               $"Serialized property '{propertyName}' is missing from '{target.name}'.");
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignLayerMask(UnityEngine.Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                           ?? throw new InvalidOperationException(
                               $"Serialized property '{propertyName}' is missing from '{target.name}'.");
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                           ?? throw new InvalidOperationException(
                               $"Serialized property '{propertyName}' is missing from '{target.name}'.");
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignArray<T>(UnityEngine.Object target, string propertyName, T[] values)
            where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                           ?? throw new InvalidOperationException(
                               $"Serialized property '{propertyName}' is missing from '{target.name}'.");
            property.arraySize = values?.Length ?? 0;
            for (var index = 0; index < property.arraySize; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
