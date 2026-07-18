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
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ouroboros.Editor
{
    public static class OSStep08GrowthSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string PickupPrefabPath = "Assets/Ouroboros/Prefabs/Pickups/PF_Pickup_BodyFragment.prefab";
        private const string PickupSpritePath = "Assets/Ouroboros/Art/Placeholders/Pickup.png";
        private const string BodyBalancePath = "Assets/Ouroboros/Data/Balance/OSBodyBalance.asset";
        private const string PlayerBalancePath = "Assets/Ouroboros/Data/Balance/OSPlayerBalance.asset";
        private const string EncounterPath = "Assets/Ouroboros/Data/Enemies/OSEncounterBalance.asset";
        private const string PickupPoolKey = "body_fragment_pickup";

        [MenuItem("Ouroboros/Setup/Apply Step 08 Body Growth")]
        public static void ApplyStep08BodyGrowth()
        {
            var pickupLayer = EnsureLayer("Pickup");
            var collectorLayer = EnsureLayer("PickupCollector");
            ConfigureCollisionMatrix(pickupLayer, collectorLayer);

            var bodyBalance = LoadRequired<OSBodyBalanceData>(BodyBalancePath);
            var playerBalance = LoadRequired<OSPlayerBalanceData>(PlayerBalancePath);
            var encounter = LoadRequired<OSEncounterBalanceData>(EncounterPath);
            var pickupPrefab = CreateOrUpdatePickupPrefab(pickupLayer);
            ConfigureScene(
                bodyBalance,
                playerBalance,
                encounter,
                pickupPrefab,
                collectorLayer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 08 body growth applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 08 WebGL")]
        public static void BuildStep08WebGL()
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step08", "WebGL"));
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
                    $"Step 08 WebGL build failed: {summary.result}, errors {summary.totalErrors}, warnings {summary.totalWarnings}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 08 WebGL succeeded: {outputPath}, " +
                $"errors {summary.totalErrors}, warnings {summary.totalWarnings}, size {summary.totalSize} bytes.");
        }

        private static OSPickup CreateOrUpdatePickupPrefab(int pickupLayer)
        {
            var sprite = LoadRequired<Sprite>(PickupSpritePath);
            var root = new GameObject(
                "PF_Pickup_BodyFragment",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(OSPickup));

            try
            {
                root.layer = pickupLayer;
                root.transform.localScale = new Vector3(0.42f, 0.42f, 1f);

                var renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color32(255, 218, 92, 255);
                renderer.sortingOrder = 4;

                var body = root.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;

                var collider = root.GetComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.34f;

                var pickup = root.GetComponent<OSPickup>();
                Assign(pickup, "body", body);
                Assign(pickup, "magnetSpeed", 8f);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PickupPrefabPath)
                            ?? throw new InvalidOperationException(
                                $"Unable to save pickup prefab at '{PickupPrefabPath}'.");
                return saved.GetComponent<OSPickup>()
                       ?? throw new InvalidOperationException("Saved pickup prefab has no OSPickup.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureScene(
            OSBodyBalanceData bodyBalance,
            OSPlayerBalanceData playerBalance,
            OSEncounterBalanceData encounter,
            OSPickup pickupPrefab,
            int collectorLayer)
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
                var session = RequireComponent<OSGameSessionController>(systems, "OSGameSessionController");
                var bodyChain = RequireComponent<OSBodyChain>(gameRoot.transform, "PlayerRoot/BodyChain");
                var poolRegistry = RequireComponent<OSPoolRegistry>(systems, "OSPoolRegistry");
                var poolContext = RequireComponent<OSEnemyPoolContext>(systems, "OSPoolRegistry");
                var enemyRegistry = RequireComponent<OSEnemyRegistry>(systems, "OSEnemyRegistry");

                Assign(bodyChain, "initialDebugSegmentCount", 0);

                var growthObject = GetOrCreateChild(systems, "OSBodyGrowthController").gameObject;
                var growth = GetOrAdd<OSBodyGrowthController>(growthObject);
                Assign(growth, "sessionController", session);
                Assign(growth, "bodyChain", bodyChain);
                Assign(growth, "bodyBalance", bodyBalance);

                var spawnerObject = GetOrCreateChild(systems, "OSPickupSpawner").gameObject;
                var spawner = GetOrAdd<OSPickupSpawner>(spawnerObject);
                Assign(spawner, "poolRegistry", poolRegistry);
                Assign(spawner, "sessionController", session);
                Assign(spawner, "bodyGrowth", growth);
                Assign(spawner, "collectionTarget", head);
                Assign(spawner, "pickupPoolKey", PickupPoolKey);
                Assign(spawner, "capacity", encounter.PickupLimit);
                Assign(spawner, "mergeRadius", 1.5f);
                Assign(spawner, "magnetRadius", playerBalance.MagnetRadius);

                Assign(poolContext, "enemyRegistry", enemyRegistry);
                Assign(poolContext, "sessionController", session);
                Assign(poolContext, "target", head);
                Assign(poolContext, "pickupSpawner", spawner);

                var poolSerialized = new SerializedObject(poolRegistry);
                UpsertPoolEntry(
                    poolSerialized.FindProperty("entries"),
                    PickupPoolKey,
                    pickupPrefab,
                    encounter.PickupLimit);
                poolSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(poolRegistry);

                // Step 01 created this as an empty placeholder. Recreate it instead of
                // reusing a potentially broken/missing-script component state from an
                // earlier partial setup run.
                var existingCollector = head.Find("PickupCollector");
                if (existingCollector != null)
                {
                    UnityEngine.Object.DestroyImmediate(existingCollector.gameObject);
                }

                var collectorObject = new GameObject("PickupCollector", typeof(CircleCollider2D));
                collectorObject.transform.SetParent(head, false);
                collectorObject.layer = collectorLayer;
                var collectorCollider = collectorObject.GetComponent<CircleCollider2D>();
                collectorCollider.isTrigger = true;
                collectorCollider.radius = 0.46f;
                collectorObject.AddComponent<OSPickupCollector>();
                EditorUtility.SetDirty(collectorCollider);

                var weapon = head.GetComponent<OSHeadWeapon>()
                             ?? throw new InvalidOperationException("OSHeadWeapon is missing from PlayerRoot/Head.");
                Assign(weapon, "bodyBalance", bodyBalance);
                Assign(weapon, "bodyChain", bodyChain);

                ConfigureRolePanel(canvas, session, growth, bodyChain, bodyBalance);
                ConfigureGrowthHud(canvas, growth, bodyChain);

                var foundationLabel = RequireComponent<TMP_Text>(canvas, "FoundationLabel");
                foundationLabel.text = "STEP 08  |  BODY GROWTH + FIXED ROLE SELECTION";
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

        private static void ConfigureRolePanel(
            Transform canvas,
            OSGameSessionController session,
            OSBodyGrowthController growth,
            OSBodyChain bodyChain,
            OSBodyBalanceData bodyBalance)
        {
            var panelTransform = RequireTransform(canvas, "BodyRoleSelectionPanel");
            var panelRect = panelTransform as RectTransform;
            panelRect.sizeDelta = new Vector2(1160f, 540f);
            for (var index = panelTransform.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(panelTransform.GetChild(index).gameObject);
            }

            var font = RequireComponent<TMP_Text>(canvas, "FoundationLabel").font;
            var title = CreateText(
                panelTransform,
                "Title",
                "BODY ROLE SELECTION",
                font,
                32f,
                TextAlignmentOptions.Center);
            SetRect(title.rectTransform, new Vector2(0f, 214f), new Vector2(1080f, 90f));

            var roles = new[]
            {
                OSBodyRoleType.Shield,
                OSBodyRoleType.Attack,
                OSBodyRoleType.Laser,
                OSBodyRoleType.Control
            };
            var colors = new[]
            {
                new Color32(50, 119, 170, 255),
                new Color32(172, 62, 70, 255),
                new Color32(122, 63, 166, 255),
                new Color32(42, 139, 101, 255)
            };
            var buttons = new Button[4];
            var labels = new TMP_Text[4];
            for (var index = 0; index < roles.Length; index++)
            {
                var card = new GameObject(
                    $"RoleCard_{roles[index]}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                card.transform.SetParent(panelTransform, false);
                SetRect(
                    (RectTransform)card.transform,
                    new Vector2(-420f + (index * 280f), -34f),
                    new Vector2(252f, 350f));
                var image = card.GetComponent<Image>();
                image.color = colors[index];
                var button = card.GetComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.ColorTint;
                buttons[index] = button;

                var label = CreateText(
                    card.transform,
                    "Label",
                    roles[index].ToString(),
                    font,
                    22f,
                    TextAlignmentOptions.Center);
                SetRect(label.rectTransform, Vector2.zero, new Vector2(228f, 326f));
                labels[index] = label;
            }

            var panel = GetOrAdd<OSBodyRoleSelectionPanel>(panelTransform.gameObject);
            UnityEventTools.AddPersistentListener(buttons[0].onClick, panel.SelectShield);
            UnityEventTools.AddPersistentListener(buttons[1].onClick, panel.SelectAttack);
            UnityEventTools.AddPersistentListener(buttons[2].onClick, panel.SelectLaser);
            UnityEventTools.AddPersistentListener(buttons[3].onClick, panel.SelectControl);

            for (var index = 0; index < buttons.Length; index++)
            {
                var navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = buttons[(index + buttons.Length - 1) % buttons.Length],
                    selectOnRight = buttons[(index + 1) % buttons.Length]
                };
                buttons[index].navigation = navigation;
                EditorUtility.SetDirty(buttons[index]);
            }

            var serialized = new SerializedObject(panel);
            serialized.FindProperty("sessionController").objectReferenceValue = session;
            serialized.FindProperty("bodyGrowth").objectReferenceValue = growth;
            serialized.FindProperty("bodyChain").objectReferenceValue = bodyChain;
            serialized.FindProperty("bodyBalance").objectReferenceValue = bodyBalance;
            serialized.FindProperty("titleLabel").objectReferenceValue = title;
            AssignArray(serialized.FindProperty("roleButtons"), buttons);
            AssignArray(serialized.FindProperty("roleLabels"), labels);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);
        }

        private static void ConfigureGrowthHud(
            Transform canvas,
            OSBodyGrowthController growth,
            OSBodyChain bodyChain)
        {
            var combatHud = RequireTransform(canvas, "CombatHUD");
            var font = RequireComponent<TMP_Text>(canvas, "FoundationLabel").font;
            var legacyBodyLabel = combatHud.Find("Body");
            if (legacyBodyLabel != null)
            {
                legacyBodyLabel.gameObject.SetActive(false);
                EditorUtility.SetDirty(legacyBodyLabel.gameObject);
            }

            var labelTransform = combatHud.Find("BodyGrowthLabel");
            TMP_Text label;
            if (labelTransform == null)
            {
                label = CreateText(
                    combatHud,
                    "BodyGrowthLabel",
                    "BODY 0\nFRAGMENT 0/12",
                    font,
                    20f,
                    TextAlignmentOptions.TopLeft);
            }
            else
            {
                label = labelTransform.GetComponent<TMP_Text>();
            }

            SetRect(label.rectTransform, new Vector2(0f, -118f), new Vector2(500f, 90f));
            var presenter = GetOrAdd<OSBodyGrowthPresenter>(label.gameObject);
            Assign(presenter, "bodyGrowth", growth);
            Assign(presenter, "bodyChain", bodyChain);
            Assign(presenter, "label", label);
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            var label = gameObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
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
                if (entries.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("key").stringValue == key)
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
            entry.FindPropertyRelative("capacity").intValue = capacity;
        }

        private static void ConfigureCollisionMatrix(int pickupLayer, int collectorLayer)
        {
            for (var layer = 0; layer < 32; layer++)
            {
                Physics2D.IgnoreLayerCollision(pickupLayer, layer, true);
                Physics2D.IgnoreLayerCollision(collectorLayer, layer, true);
            }

            Physics2D.IgnoreLayerCollision(pickupLayer, collectorLayer, false);
        }

        private static int EnsureLayer(string layerName)
        {
            var existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
            {
                return existing;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
            {
                throw new InvalidOperationException("Unable to load ProjectSettings/TagManager.asset.");
            }

            var serialized = new SerializedObject(assets[0]);
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

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path)
                   ?? throw new InvalidOperationException($"Required asset is missing at '{path}'.");
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

        private static void AssignArray<T>(SerializedProperty property, T[] values)
            where T : UnityEngine.Object
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }
    }
}
