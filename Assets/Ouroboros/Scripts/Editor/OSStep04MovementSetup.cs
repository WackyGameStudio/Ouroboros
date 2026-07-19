using System;
using System.IO;
using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ouroboros.Editor
{
    public static class OSStep04MovementSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string PlayerBalancePath = "Assets/Ouroboros/Data/Balance/OSPlayerBalance.asset";
        private const string ObstacleSpritePath = "Assets/Ouroboros/Art/Placeholders/Obstacle.png";
        private const string DirectionSpritePath = "Assets/Ouroboros/Art/Placeholders/Projectile.png";
        private const string WorldBlockerLayerName = "WorldBlocker";
        private const string PlayerHeadLayerName = "PlayerHeadSolid";
        internal const float GameplayOrthographicSize = 6.5f;
        internal const float HeadSpriteForwardDegrees = 90f;
        private static readonly Vector2 WorldMinimum = new(-24f, -15f);
        private static readonly Vector2 WorldMaximum = new(24f, 15f);

        [MenuItem("Ouroboros/Setup/Apply Step 04 Movement Foundation")]
        public static void ApplyStep04MovementFoundation()
        {
            var worldBlockerLayer = EnsureLayer(WorldBlockerLayerName);
            var playerHeadLayer = EnsureLayer(PlayerHeadLayerName);
            ConfigurePlayerCollisionMatrix(playerHeadLayer, worldBlockerLayer);
            var obstacleSprite = EnsureObstacleSprite();

            var scene = SceneManager.GetSceneByPath(GameScenePath);
            var openedForSetup = !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var gameRoot = FindRoot(scene, "GameRoot");
                var head = RequireTransform(gameRoot.transform, "PlayerRoot/Head");
                var world = RequireTransform(gameRoot.transform, "World");
                var obstacles = RequireTransform(world, "Obstacles");
                var router = RequireComponent<OSInputRouter>(gameRoot.transform, "Systems/OSInputRouter");
                var session = RequireComponent<OSGameSessionController>(
                    gameRoot.transform,
                    "Systems/OSGameSessionController");
                var cameraTransform = RequireTransform(gameRoot.transform, "CameraRoot/Main Camera");
                var camera = cameraTransform.GetComponent<Camera>()
                             ?? throw new InvalidOperationException("Main Camera is missing its Camera component.");
                var playerBalance = AssetDatabase.LoadAssetAtPath<OSPlayerBalanceData>(PlayerBalancePath)
                                    ?? throw new InvalidOperationException(
                                        $"Player balance is missing at '{PlayerBalancePath}'.");

                var playerController = ConfigurePlayerHead(
                    head,
                    router,
                    session,
                    playerBalance,
                    playerHeadLayer,
                    worldBlockerLayer);
                ConfigurePlayerVisual(head, playerController, session);
                ConfigureCamera(cameraTransform, camera, head, playerController);
                ConfigureArena(obstacles, obstacleSprite, worldBlockerLayer);
                RemoveLegacyMapPlaceholder(world, "Enemy_Chaser");
                RemoveLegacyMapPlaceholder(world, "Pickup");

                var foundationLabel = RequireComponent<TMP_Text>(gameRoot.transform, "Canvas/FoundationLabel");
                foundationLabel.text = "STEP 04 MOVEMENT + CAMERA FOUNDATION";
                EditorUtility.SetDirty(foundationLabel);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[OUROBOROS][SETUP] Step 04 movement foundation applied.");
            }
            finally
            {
                if (openedForSetup && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static OSPlayerController ConfigurePlayerHead(
            Transform head,
            OSInputRouter router,
            OSGameSessionController session,
            OSPlayerBalanceData playerBalance,
            int playerLayer,
            int blockerLayer)
        {
            head.gameObject.layer = playerLayer;
            var body = GetOrAdd<Rigidbody2D>(head.gameObject);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.useFullKinematicContacts = true;

            var solidCollider = GetOrAdd<CircleCollider2D>(head.gameObject);
            solidCollider.isTrigger = false;
            solidCollider.radius = 0.52f;

            var controller = GetOrAdd<OSPlayerController>(head.gameObject);
            Assign(controller, "body", body);
            Assign(controller, "solidCollider", solidCollider);
            Assign(controller, "inputRouter", router);
            Assign(controller, "sessionController", session);
            Assign(controller, "playerBalance", playerBalance);
            AssignLayerMask(controller, "worldBlockerMask", 1 << blockerLayer);
            Assign(controller, "worldMin", WorldMinimum);
            Assign(controller, "worldMax", WorldMaximum);
            Assign(controller, "skinWidth", 0.02f);
            return controller;
        }

        private static void ConfigurePlayerVisual(
            Transform head,
            OSPlayerController playerController,
            OSGameSessionController session)
        {
            var core = RequireTransform(head, "Head");
            var indicator = head.Find("DirectionIndicator");
            if (indicator == null)
            {
                var indicatorObject = new GameObject("DirectionIndicator", typeof(SpriteRenderer));
                indicator = indicatorObject.transform;
                indicator.SetParent(head, false);
            }

            var renderer = indicator.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DirectionSpritePath)
                              ?? throw new InvalidOperationException(
                                  $"Direction sprite is missing at '{DirectionSpritePath}'.");
            renderer.color = new Color32(255, 235, 84, 255);
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 2);
            indicator.localPosition = new Vector3(0.72f, 0f, 0f);
            indicator.localRotation = Quaternion.identity;
            indicator.localScale = new Vector3(0.28f, 0.16f, 1f);

            var visual = GetOrAdd<OSPlayerHeadVisual>(head.gameObject);
            Assign(visual, "playerController", playerController);
            Assign(visual, "sessionController", session);
            Assign(visual, "coreVisual", core);
            Assign(visual, "directionIndicator", indicator);
            Assign(visual, "spriteForwardDegrees", HeadSpriteForwardDegrees);
            core.localRotation = Quaternion.Euler(0f, 0f, -HeadSpriteForwardDegrees);
            EditorUtility.SetDirty(core);
        }

        private static void ConfigureCamera(
            Transform cameraTransform,
            Camera camera,
            Transform head,
            OSPlayerController playerController)
        {
            camera.orthographic = true;
            camera.orthographicSize = GameplayOrthographicSize;
            var follower = GetOrAdd<OSCameraFollower>(cameraTransform.gameObject);
            Assign(follower, "target", head);
            Assign(follower, "targetCamera", camera);
            Assign(follower, "playerController", playerController);
            Assign(follower, "smoothTime", 0.1f);
            Assign(follower, "edgePadding", 0.35f);
            Assign(follower, "worldMin", WorldMinimum);
            Assign(follower, "worldMax", WorldMaximum);
        }

        internal static void ApplyGameplayCameraFraming()
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
                var cameraTransform = RequireTransform(gameRoot.transform, "CameraRoot/Main Camera");
                var camera = cameraTransform.GetComponent<Camera>()
                             ?? throw new InvalidOperationException(
                                 "Main Camera is missing its Camera component.");
                camera.orthographic = true;
                camera.orthographicSize = GameplayOrthographicSize;
                EditorUtility.SetDirty(camera);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (openedForSetup && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        internal static void ApplyPlayerHeadFacing()
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
                var head = RequireTransform(gameRoot.transform, "PlayerRoot/Head");
                var playerController = head.GetComponent<OSPlayerController>()
                                       ?? throw new InvalidOperationException(
                                           "Player head is missing OSPlayerController.");
                var session = RequireComponent<OSGameSessionController>(
                    gameRoot.transform,
                    "Systems/OSGameSessionController");
                ConfigurePlayerVisual(head, playerController, session);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (openedForSetup && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ConfigureArena(Transform obstacles, Sprite obstacleSprite, int blockerLayer)
        {
            EnsureObstacle(
                obstacles,
                "Obstacle_Wide",
                new Vector2(-8f, 4f),
                new Vector2(4f, 1.2f),
                new Color32(93, 111, 145, 255),
                obstacleSprite,
                blockerLayer);
            EnsureObstacle(
                obstacles,
                "Obstacle_Tall",
                new Vector2(8f, -3f),
                new Vector2(1.2f, 4f),
                new Color32(118, 87, 145, 255),
                obstacleSprite,
                blockerLayer);
            EnsureObstacle(
                obstacles,
                "Obstacle_Block",
                new Vector2(3f, 8f),
                new Vector2(2.8f, 1.4f),
                new Color32(70, 132, 132, 255),
                obstacleSprite,
                blockerLayer);
            EnsureObstacle(
                obstacles,
                "Obstacle_West",
                new Vector2(-15f, -4f),
                new Vector2(3f, 1.3f),
                new Color32(84, 123, 151, 255),
                obstacleSprite,
                blockerLayer);
            EnsureObstacle(
                obstacles,
                "Obstacle_East",
                new Vector2(15f, 4f),
                new Vector2(3.3f, 1.2f),
                new Color32(131, 91, 147, 255),
                obstacleSprite,
                blockerLayer);
            EnsureObstacle(
                obstacles,
                "Obstacle_South",
                new Vector2(-4f, -9f),
                new Vector2(4f, 1.2f),
                new Color32(67, 130, 137, 255),
                obstacleSprite,
                blockerLayer);
            EnsureObstacle(
                obstacles,
                "Obstacle_North",
                new Vector2(12f, 10f),
                new Vector2(1.3f, 3.8f),
                new Color32(105, 112, 153, 255),
                obstacleSprite,
                blockerLayer);

            var bounds = GetOrCreateChild(obstacles, "WorldBounds");
            EnsureBoundary(bounds, "Boundary_Left", new Vector2(-24.25f, 0f), new Vector2(0.5f, 30f), blockerLayer);
            EnsureBoundary(bounds, "Boundary_Right", new Vector2(24.25f, 0f), new Vector2(0.5f, 30f), blockerLayer);
            EnsureBoundary(bounds, "Boundary_Top", new Vector2(0f, 15.25f), new Vector2(48f, 0.5f), blockerLayer);
            EnsureBoundary(bounds, "Boundary_Bottom", new Vector2(0f, -15.25f), new Vector2(48f, 0.5f), blockerLayer);
        }

        private static void RemoveLegacyMapPlaceholder(Transform world, string name)
        {
            var placeholder = world.Find(name);
            if (placeholder != null)
            {
                UnityEngine.Object.DestroyImmediate(placeholder.gameObject);
            }
        }

        private static void EnsureObstacle(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color32 color,
            Sprite sprite,
            int layer)
        {
            var obstacle = GetOrCreateChild(parent, name);
            obstacle.gameObject.layer = layer;
            obstacle.localPosition = new Vector3(position.x, position.y, 0f);
            obstacle.localRotation = Quaternion.identity;
            obstacle.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = GetOrAdd<SpriteRenderer>(obstacle.gameObject);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = -1;

            var collider = GetOrAdd<BoxCollider2D>(obstacle.gameObject);
            collider.isTrigger = false;
            collider.size = Vector2.one;
        }

        private static void EnsureBoundary(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            int layer)
        {
            var boundary = GetOrCreateChild(parent, name);
            boundary.gameObject.layer = layer;
            boundary.localPosition = new Vector3(position.x, position.y, 0f);
            boundary.localRotation = Quaternion.identity;
            boundary.localScale = Vector3.one;
            var collider = GetOrAdd<BoxCollider2D>(boundary.gameObject);
            collider.isTrigger = false;
            collider.size = size;
        }

        private static Sprite EnsureObstacleSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(ObstacleSpritePath);
            if (existing != null)
            {
                return existing;
            }

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var fill = new Color32(180, 198, 226, 255);
            var edge = new Color32(42, 53, 74, 255);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var isEdge = x < 3 || x >= size - 3 || y < 3 || y >= size - 3;
                    var stripe = ((x + y) / 6) % 2 == 0;
                    pixels[(y * size) + x] = isEdge ? edge : stripe ? fill : new Color32(155, 176, 207, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                              ?? throw new InvalidOperationException("Unable to resolve project root.");
            var absolutePath = Path.Combine(
                projectRoot,
                ObstacleSpritePath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(ObstacleSpritePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(ObstacleSpritePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = size;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(ObstacleSpritePath)
                   ?? throw new InvalidOperationException("Obstacle placeholder sprite was not imported.");
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

        private static void ConfigurePlayerCollisionMatrix(int playerLayer, int blockerLayer)
        {
            for (var layer = 0; layer < 32; layer++)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, layer, layer != blockerLayer);
            }
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
                   ?? throw new InvalidOperationException($"{typeof(T).Name} is missing from '{target.name}'.");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var existing = target.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(target);
        }

        private static void Assign(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var property = GetSerializedProperty(target, propertyName);
            property.objectReferenceValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void Assign(UnityEngine.Object target, string propertyName, Vector2 value)
        {
            var property = GetSerializedProperty(target, propertyName);
            property.vector2Value = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void Assign(UnityEngine.Object target, string propertyName, float value)
        {
            var property = GetSerializedProperty(target, propertyName);
            property.floatValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignLayerMask(UnityEngine.Object target, string propertyName, int value)
        {
            var property = GetSerializedProperty(target, propertyName);
            property.intValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static SerializedProperty GetSerializedProperty(UnityEngine.Object target, string propertyName)
        {
            var serialized = new SerializedObject(target);
            return serialized.FindProperty(propertyName)
                   ?? throw new InvalidOperationException(
                       $"Serialized property '{propertyName}' is missing on {target.GetType().Name}.");
        }
    }
}
