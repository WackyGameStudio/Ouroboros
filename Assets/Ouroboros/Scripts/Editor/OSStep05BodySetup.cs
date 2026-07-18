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
    public static class OSStep05BodySetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string BodyBalancePath = "Assets/Ouroboros/Data/Balance/OSBodyBalance.asset";
        private const string SegmentPrefabPath = "Assets/Ouroboros/Prefabs/Player/PF_BodySegment.prefab";
        private const string IconSpritePath = "Assets/Ouroboros/Art/Placeholders/Projectile.png";
        private const string PlayerBodyLayerName = "PlayerBodyHurtbox";
        private const string PlayerHeadLayerName = "PlayerHeadSolid";
        private const string WorldBlockerLayerName = "WorldBlocker";
        private static readonly string[] RoleSpritePaths =
        {
            "Assets/Ouroboros/Art/Placeholders/Body_Shield.png",
            "Assets/Ouroboros/Art/Placeholders/Body_Attack.png",
            "Assets/Ouroboros/Art/Placeholders/Body_Laser.png",
            "Assets/Ouroboros/Art/Placeholders/Body_Control.png"
        };

        [MenuItem("Ouroboros/Setup/Apply Step 05 Body Path Foundation")]
        public static void ApplyStep05BodyPathFoundation()
        {
            var bodyLayer = EnsureLayer(PlayerBodyLayerName);
            var headLayer = RequireLayer(PlayerHeadLayerName);
            var blockerLayer = RequireLayer(WorldBlockerLayerName);
            ConfigureCollisionMatrix(bodyLayer, headLayer, blockerLayer);
            var segmentPrefab = CreateOrUpdateSegmentPrefab(bodyLayer);

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
                var bodyChainRoot = RequireTransform(gameRoot.transform, "PlayerRoot/BodyChain");
                var session = RequireComponent<OSGameSessionController>(
                    gameRoot.transform,
                    "Systems/OSGameSessionController");
                var playerController = head.GetComponent<OSPlayerController>()
                                       ?? throw new InvalidOperationException(
                                           "Step 04 OSPlayerController is missing from the head.");
                var bodyBalance = AssetDatabase.LoadAssetAtPath<OSBodyBalanceData>(BodyBalancePath)
                                  ?? throw new InvalidOperationException(
                                      $"Body balance is missing at '{BodyBalancePath}'.");

                RemoveStep01BodyPlaceholders(bodyChainRoot);
                var poolRoot = GetOrCreateChild(bodyChainRoot, "SegmentPool");
                poolRoot.localPosition = Vector3.zero;
                poolRoot.localRotation = Quaternion.identity;
                poolRoot.localScale = Vector3.one;

                var chain = GetOrAdd<OSBodyChain>(bodyChainRoot.gameObject);
                Assign(chain, "head", head);
                Assign(chain, "playerController", playerController);
                Assign(chain, "sessionController", session);
                Assign(chain, "bodyBalance", bodyBalance);
                Assign(chain, "segmentPrefab", segmentPrefab);
                Assign(chain, "poolRoot", poolRoot);
                Assign(chain, "initialDebugSegmentCount", 20);

                var foundationLabel = RequireComponent<TMP_Text>(gameRoot.transform, "Canvas/FoundationLabel");
                foundationLabel.text = "STEP 05 PATH BODY  |  DEBUG 20 / POOL 64";
                EditorUtility.SetDirty(foundationLabel);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[OUROBOROS][SETUP] Step 05 body path foundation applied.");
            }
            finally
            {
                if (openedForSetup && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static OSBodySegmentView CreateOrUpdateSegmentPrefab(int bodyLayer)
        {
            var roleSprites = new Sprite[RoleSpritePaths.Length];
            for (var index = 0; index < RoleSpritePaths.Length; index++)
            {
                roleSprites[index] = AssetDatabase.LoadAssetAtPath<Sprite>(RoleSpritePaths[index])
                                     ?? throw new InvalidOperationException(
                                         $"Role sprite is missing at '{RoleSpritePaths[index]}'.");
            }

            var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconSpritePath)
                             ?? throw new InvalidOperationException(
                                 $"Role icon sprite is missing at '{IconSpritePath}'.");

            var prefabRoot = new GameObject("PF_BodySegment");
            try
            {
                prefabRoot.layer = bodyLayer;
                prefabRoot.transform.localScale = new Vector3(0.72f, 0.72f, 1f);

                var bodyVisual = new GameObject("SpriteRenderer", typeof(SpriteRenderer));
                bodyVisual.transform.SetParent(prefabRoot.transform, false);
                var bodyRenderer = bodyVisual.GetComponent<SpriteRenderer>();
                bodyRenderer.sprite = roleSprites[0];
                bodyRenderer.color = Color.white;
                bodyRenderer.sortingOrder = 0;

                var iconVisual = new GameObject("RoleIconRenderer", typeof(SpriteRenderer));
                iconVisual.transform.SetParent(prefabRoot.transform, false);
                iconVisual.transform.localScale = new Vector3(0.24f, 0.24f, 1f);
                var iconRenderer = iconVisual.GetComponent<SpriteRenderer>();
                iconRenderer.sprite = iconSprite;
                iconRenderer.color = new Color32(92, 207, 255, 255);
                iconRenderer.sortingOrder = 1;

                var hurtboxObject = new GameObject("BodyHurtbox", typeof(CircleCollider2D));
                hurtboxObject.layer = bodyLayer;
                hurtboxObject.transform.SetParent(prefabRoot.transform, false);
                var hurtbox = hurtboxObject.GetComponent<CircleCollider2D>();
                hurtbox.isTrigger = true;
                hurtbox.radius = 0.43f;

                GetOrCreateChild(prefabRoot.transform, "Muzzle").localPosition = new Vector3(0.48f, 0f, 0f);
                GetOrCreateChild(prefabRoot.transform, "EffectAnchor").localPosition = Vector3.zero;

                var view = prefabRoot.AddComponent<OSBodySegmentView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("bodyRenderer").objectReferenceValue = bodyRenderer;
                serialized.FindProperty("roleIconRenderer").objectReferenceValue = iconRenderer;
                serialized.FindProperty("bodyHurtbox").objectReferenceValue = hurtbox;
                var spriteArray = serialized.FindProperty("roleSprites");
                spriteArray.arraySize = roleSprites.Length;
                for (var index = 0; index < roleSprites.Length; index++)
                {
                    spriteArray.GetArrayElementAtIndex(index).objectReferenceValue = roleSprites[index];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                prefabRoot.SetActive(false);
                var prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, SegmentPrefabPath)
                             ?? throw new InvalidOperationException(
                                 $"Unable to save segment prefab at '{SegmentPrefabPath}'.");
                return prefab.GetComponent<OSBodySegmentView>()
                       ?? throw new InvalidOperationException("Saved segment prefab is missing its view.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        private static void RemoveStep01BodyPlaceholders(Transform bodyChainRoot)
        {
            for (var index = bodyChainRoot.childCount - 1; index >= 0; index--)
            {
                var child = bodyChainRoot.GetChild(index);
                if (child.name is "Body_Shield" or "Body_Attack" or "Body_Laser" or "Body_Control")
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private static void ConfigureCollisionMatrix(int bodyLayer, int headLayer, int blockerLayer)
        {
            Physics2D.IgnoreLayerCollision(bodyLayer, bodyLayer, true);
            Physics2D.IgnoreLayerCollision(bodyLayer, headLayer, true);
            Physics2D.IgnoreLayerCollision(bodyLayer, blockerLayer, true);
        }

        private static int RequireLayer(string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            return layer >= 0
                ? layer
                : throw new InvalidOperationException(
                    $"Required Step 04 layer '{layerName}' is missing.");
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

        private static void Assign(UnityEngine.Object target, string propertyName, int value)
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
