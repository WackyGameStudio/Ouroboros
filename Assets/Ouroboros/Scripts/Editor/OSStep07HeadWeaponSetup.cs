using System;
using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ouroboros.Editor
{
    public static class OSStep07HeadWeaponSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string PlayerBalancePath = "Assets/Ouroboros/Data/Balance/OSPlayerBalance.asset";
        private const string EncounterPath = "Assets/Ouroboros/Data/Enemies/OSEncounterBalance.asset";
        private const string ProjectilePrefabPath =
            "Assets/Ouroboros/Prefabs/Projectiles/PF_HeadProjectile.prefab";
        private const string ProjectileSpritePath = "Assets/Ouroboros/Art/Placeholders/Projectile.png";
        private const string ProjectilePoolKey = "head_projectile";

        [MenuItem("Ouroboros/Setup/Apply Step 07 Head Weapon")]
        public static void ApplyStep07HeadWeapon()
        {
            var enemyHurtboxLayer = RequireLayer("EnemyHurtbox");
            var worldBlockerLayer = RequireLayer("WorldBlocker");
            var projectileLayer = EnsureLayer("PlayerProjectile");
            ConfigureCollisionMatrix(projectileLayer, enemyHurtboxLayer, worldBlockerLayer);

            var playerBalance = AssetDatabase.LoadAssetAtPath<OSPlayerBalanceData>(PlayerBalancePath)
                                ?? throw new InvalidOperationException(
                                    $"Player balance is missing at '{PlayerBalancePath}'.");
            var encounter = AssetDatabase.LoadAssetAtPath<OSEncounterBalanceData>(EncounterPath)
                            ?? throw new InvalidOperationException(
                                $"Encounter balance is missing at '{EncounterPath}'.");
            var projectilePrefab = CreateOrUpdateProjectilePrefab(projectileLayer);
            ConfigureScene(playerBalance, encounter, projectilePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 07 head weapon applied.");
        }

        private static OSProjectile CreateOrUpdateProjectilePrefab(int projectileLayer)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectileSpritePath)
                         ?? throw new InvalidOperationException(
                             $"Projectile sprite is missing at '{ProjectileSpritePath}'.");
            var root = new GameObject(
                "PF_HeadProjectile",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(OSProjectile));

            try
            {
                root.layer = projectileLayer;
                root.transform.localScale = new Vector3(0.38f, 0.38f, 1f);

                var renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color32(255, 239, 86, 255);
                renderer.sortingOrder = 5;

                var body = root.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.useFullKinematicContacts = true;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;

                var collider = root.GetComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.28f;

                var projectile = root.GetComponent<OSProjectile>();
                var serialized = new SerializedObject(projectile);
                serialized.FindProperty("body").objectReferenceValue = body;
                serialized.FindProperty("projectileCollider").objectReferenceValue = collider;
                serialized.FindProperty("worldBlockerMask").intValue =
                    1 << RequireLayer("WorldBlocker");
                serialized.FindProperty("moveSpeed").floatValue = 12f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath)
                             ?? throw new InvalidOperationException(
                                 $"Unable to save projectile prefab at '{ProjectilePrefabPath}'.");
                return prefab.GetComponent<OSProjectile>()
                       ?? throw new InvalidOperationException(
                           "Saved projectile prefab is missing OSProjectile.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureScene(
            OSPlayerBalanceData playerBalance,
            OSEncounterBalanceData encounter,
            OSProjectile projectilePrefab)
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
                var poolRoot = RequireTransform(world, "RuntimePools");
                var session = RequireComponent<OSGameSessionController>(
                    systems,
                    "OSGameSessionController");
                var registry = RequireComponent<OSEnemyRegistry>(systems, "OSEnemyRegistry");
                var poolObject = RequireTransform(systems, "OSPoolRegistry").gameObject;
                var pool = poolObject.GetComponent<OSPoolRegistry>()
                           ?? throw new InvalidOperationException("OSPoolRegistry component is missing.");
                var context = poolObject.GetComponent<OSEnemyPoolContext>()
                              ?? throw new InvalidOperationException("OSEnemyPoolContext component is missing.");
                context.Configure(registry, session, head);
                EditorUtility.SetDirty(context);

                var poolSerialized = new SerializedObject(pool);
                poolSerialized.FindProperty("poolRoot").objectReferenceValue = poolRoot;
                poolSerialized.FindProperty("rentInitializer").objectReferenceValue = context;
                UpsertPoolEntry(
                    poolSerialized.FindProperty("entries"),
                    ProjectilePoolKey,
                    projectilePrefab,
                    encounter.ProjectileLimit);
                poolSerialized.ApplyModifiedPropertiesWithoutUndo();

                var muzzle = GetOrCreateChild(head, "ProjectileMuzzle");
                muzzle.localPosition = new Vector3(0.42f, 0f, 0f);
                var weapon = GetOrAdd<OSHeadWeapon>(head.gameObject);
                weapon.Configure(playerBalance, pool, registry, session, muzzle);
                EditorUtility.SetDirty(weapon);

                var oldPlaceholder = world.Find("Projectile");
                if (oldPlaceholder != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldPlaceholder.gameObject);
                }

                var foundationLabel = RequireComponent<TMP_Text>(gameRoot.transform, "Canvas/FoundationLabel");
                foundationLabel.text = "STEP 07 AUTO FIRE  |  ENEMY 100  |  SHOT 0  HIT 0  KILL 0";
                EditorUtility.SetDirty(foundationLabel);
                var presenter = GetOrAdd<OSCombatDebugPresenter>(systems.gameObject);
                presenter.Configure(weapon, registry, foundationLabel);
                EditorUtility.SetDirty(presenter);

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

        private static void ConfigureCollisionMatrix(
            int projectileLayer,
            int enemyHurtboxLayer,
            int worldBlockerLayer)
        {
            for (var other = 0; other < 32; other++)
            {
                Physics2D.IgnoreLayerCollision(projectileLayer, other, true);
            }

            Physics2D.IgnoreLayerCollision(projectileLayer, enemyHurtboxLayer, false);
            Physics2D.IgnoreLayerCollision(projectileLayer, worldBlockerLayer, false);
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
    }
}
