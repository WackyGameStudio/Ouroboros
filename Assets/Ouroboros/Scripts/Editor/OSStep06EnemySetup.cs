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
    public static class OSStep06EnemySetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string EncounterPath = "Assets/Ouroboros/Data/Enemies/OSEncounterBalance.asset";
        private const string ChaserPrefabPath = "Assets/Ouroboros/Prefabs/Enemies/PF_Enemy_Chaser.prefab";
        private const string ChaserSpritePath = "Assets/Ouroboros/Art/Placeholders/Enemy_Chaser.png";
        private const string ChaserKey = "enemy_chaser";
        private const int ChaserPoolCapacity = 200;

        [MenuItem("Ouroboros/Setup/Apply Step 06 Enemy Foundation")]
        public static void ApplyStep06EnemyFoundation()
        {
            var worldBlockerLayer = RequireLayer("WorldBlocker");
            var playerHeadLayer = RequireLayer("PlayerHeadSolid");
            var playerBodyLayer = RequireLayer("PlayerBodyHurtbox");
            var playerHeadHurtboxLayer = EnsureLayer("PlayerHeadHurtbox");
            var enemyBodyLayer = EnsureLayer("EnemyBody");
            var enemyHurtboxLayer = EnsureLayer("EnemyHurtbox");
            var enemyHitboxLayer = EnsureLayer("EnemyHitbox");
            ConfigureCollisionMatrix(
                worldBlockerLayer,
                playerHeadLayer,
                playerBodyLayer,
                playerHeadHurtboxLayer,
                enemyBodyLayer,
                enemyHurtboxLayer,
                enemyHitboxLayer);

            var encounter = AssetDatabase.LoadAssetAtPath<OSEncounterBalanceData>(EncounterPath)
                            ?? throw new InvalidOperationException(
                                $"Encounter balance is missing at '{EncounterPath}'.");
            var chaserPrefab = CreateOrUpdateChaserPrefab(
                encounter,
                enemyBodyLayer,
                enemyHurtboxLayer,
                enemyHitboxLayer);
            ConfigureChaserDefinition(encounter, chaserPrefab.gameObject);
            ConfigureScene(encounter, chaserPrefab, playerHeadHurtboxLayer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 06 enemy foundation applied.");
        }

        private static OSEnemyController CreateOrUpdateChaserPrefab(
            OSEncounterBalanceData encounter,
            int enemyBodyLayer,
            int enemyHurtboxLayer,
            int enemyHitboxLayer)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ChaserSpritePath)
                         ?? throw new InvalidOperationException(
                             $"Chaser sprite is missing at '{ChaserSpritePath}'.");
            var root = new GameObject(
                "PF_Enemy_Chaser",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(OSEnemyController));

            try
            {
                root.layer = enemyBodyLayer;
                root.transform.localScale = new Vector3(0.76f, 0.76f, 1f);

                var renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color32(255, 113, 138, 255);
                renderer.sortingOrder = 2;

                var body = root.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.useFullKinematicContacts = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;

                var bodyCollider = root.GetComponent<CircleCollider2D>();
                bodyCollider.isTrigger = false;
                bodyCollider.radius = 0.42f;

                var controller = root.GetComponent<OSEnemyController>();
                var hurtbox = CreateCircleChild(root.transform, "EnemyHurtbox", enemyHurtboxLayer, 0.45f);
                var hitbox = CreateCircleChild(root.transform, "EnemyHitbox", enemyHitboxLayer, 0.58f);
                var hitboxRelay = hitbox.gameObject.AddComponent<OSEnemyContactHitbox>();
                hitboxRelay.Configure(controller);
                GetOrCreateChild(root.transform, "DropAnchor").localPosition = Vector3.zero;
                GetOrCreateChild(root.transform, "EffectAnchor").localPosition = Vector3.zero;

                var serialized = new SerializedObject(controller);
                serialized.FindProperty("encounterBalance").objectReferenceValue = encounter;
                serialized.FindProperty("definitionId").stringValue = ChaserKey;
                serialized.FindProperty("body").objectReferenceValue = body;
                serialized.FindProperty("bodyRenderer").objectReferenceValue = renderer;
                serialized.FindProperty("reclaimDistance").floatValue = 32f;
                serialized.FindProperty("separationRadius").floatValue = 0.72f;
                serialized.FindProperty("separationStrength").floatValue = 0.55f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, ChaserPrefabPath)
                             ?? throw new InvalidOperationException(
                                 $"Unable to save chaser prefab at '{ChaserPrefabPath}'.");
                return prefab.GetComponent<OSEnemyController>()
                       ?? throw new InvalidOperationException("Saved chaser prefab is missing its controller.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static CircleCollider2D CreateCircleChild(
            Transform parent,
            string name,
            int layer,
            float radius)
        {
            var child = new GameObject(name, typeof(CircleCollider2D));
            child.layer = layer;
            child.transform.SetParent(parent, false);
            var collider = child.GetComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = radius;
            return collider;
        }

        private static void ConfigureChaserDefinition(
            OSEncounterBalanceData encounter,
            GameObject chaserPrefab)
        {
            var serialized = new SerializedObject(encounter);
            var definitions = serialized.FindProperty("enemyDefinitions")
                              ?? throw new InvalidOperationException(
                                  "Encounter balance enemyDefinitions are missing.");
            for (var index = 0; index < definitions.arraySize; index++)
            {
                var definition = definitions.GetArrayElementAtIndex(index);
                if (definition.FindPropertyRelative("id").stringValue != ChaserKey)
                {
                    continue;
                }

                definition.FindPropertyRelative("prefab").objectReferenceValue = chaserPrefab;
                definition.FindPropertyRelative("poolCapacity").intValue = ChaserPoolCapacity;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(encounter);
                return;
            }

            throw new InvalidOperationException($"Enemy definition '{ChaserKey}' is missing.");
        }

        private static void ConfigureScene(
            OSEncounterBalanceData encounter,
            OSEnemyController chaserPrefab,
            int playerHeadHurtboxLayer)
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
                var session = RequireComponent<OSGameSessionController>(
                    systems,
                    "OSGameSessionController");
                var poolRoot = RequireTransform(world, "RuntimePools");

                var registryObject = RequireTransform(systems, "OSEnemyRegistry").gameObject;
                var enemyRegistry = GetOrAdd<OSEnemyRegistry>(registryObject);
                Assign(enemyRegistry, "capacity", ChaserPoolCapacity);

                var poolObject = RequireTransform(systems, "OSPoolRegistry").gameObject;
                var poolRegistry = GetOrAdd<OSPoolRegistry>(poolObject);
                var enemyContext = GetOrAdd<OSEnemyPoolContext>(poolObject);
                enemyContext.Configure(enemyRegistry, session, head);
                EditorUtility.SetDirty(enemyContext);

                var poolSerialized = new SerializedObject(poolRegistry);
                poolSerialized.FindProperty("poolRoot").objectReferenceValue = poolRoot;
                poolSerialized.FindProperty("rentInitializer").objectReferenceValue = enemyContext;
                var entries = poolSerialized.FindProperty("entries");
                entries.arraySize = 1;
                var entry = entries.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("key").stringValue = ChaserKey;
                entry.FindPropertyRelative("prefab").objectReferenceValue = chaserPrefab;
                entry.FindPropertyRelative("capacity").intValue = ChaserPoolCapacity;
                poolSerialized.ApplyModifiedPropertiesWithoutUndo();

                var headHurtbox = GetOrCreateChild(head, "HeadHurtbox");
                headHurtbox.gameObject.layer = playerHeadHurtboxLayer;
                var headCollider = GetOrAdd<CircleCollider2D>(headHurtbox.gameObject);
                headCollider.isTrigger = true;
                headCollider.radius = 0.4f;
                var identity = GetOrAdd<OSCombatTargetIdentity>(headHurtbox.gameObject);
                identity.Configure(1, OSTargetKind.PlayerHead);
                EditorUtility.SetDirty(headCollider);
                EditorUtility.SetDirty(identity);

                var spawnerObject = GetOrCreateChild(world, "EnemyDebugSpawner").gameObject;
                var spawner = GetOrAdd<OSEnemyDebugSpawner>(spawnerObject);
                Assign(spawner, "poolRegistry", poolRegistry);
                Assign(spawner, "enemyRegistry", enemyRegistry);
                Assign(spawner, "sessionController", session);
                Assign(spawner, "target", head);
                Assign(spawner, "poolKey", ChaserKey);
                Assign(spawner, "initialEnemyCount", 12);
                Assign(spawner, "minimumRadius", 6.5f);
                Assign(spawner, "maximumRadius", 11.5f);
                Assign(spawner, "replacementDelay", 1.5f);

                var foundationLabel = RequireComponent<TMP_Text>(gameRoot.transform, "Canvas/FoundationLabel");
                foundationLabel.text = "STEP 06 ENEMY POOL  |  ACTIVE 12 / PREWARM 200 / REPLACE 1.5s";
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

        private static void ConfigureCollisionMatrix(
            int worldBlockerLayer,
            int playerHeadLayer,
            int playerBodyLayer,
            int playerHeadHurtboxLayer,
            int enemyBodyLayer,
            int enemyHurtboxLayer,
            int enemyHitboxLayer)
        {
            var isolatedLayers = new[]
            {
                playerHeadHurtboxLayer,
                enemyBodyLayer,
                enemyHurtboxLayer,
                enemyHitboxLayer
            };

            for (var index = 0; index < isolatedLayers.Length; index++)
            {
                for (var other = 0; other < 32; other++)
                {
                    Physics2D.IgnoreLayerCollision(isolatedLayers[index], other, true);
                }
            }

            Physics2D.IgnoreLayerCollision(enemyBodyLayer, worldBlockerLayer, false);
            Physics2D.IgnoreLayerCollision(enemyHitboxLayer, playerHeadHurtboxLayer, false);
            Physics2D.IgnoreLayerCollision(enemyHitboxLayer, playerBodyLayer, false);
            Physics2D.IgnoreLayerCollision(enemyBodyLayer, enemyBodyLayer, true);
            Physics2D.IgnoreLayerCollision(enemyBodyLayer, playerHeadLayer, true);
            Physics2D.IgnoreLayerCollision(enemyBodyLayer, playerBodyLayer, true);
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

        private static void Assign(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void Assign(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void Assign(UnityEngine.Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void Assign(UnityEngine.Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
