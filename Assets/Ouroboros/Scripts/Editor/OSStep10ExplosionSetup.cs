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
    public static class OSStep10ExplosionSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string BodyBalancePath = "Assets/Ouroboros/Data/Balance/OSBodyBalance.asset";
        private const int TelegraphCircleCapacity = 20;

        [MenuItem("Ouroboros/Setup/Apply Step 10 Encirclement Explosion")]
        public static void ApplyStep10EncirclementExplosion()
        {
            OSStep09DamageSetup.ApplyStep09DamageAndCutting();
            var bodyBalance = LoadRequired<OSBodyBalanceData>(BodyBalancePath);
            ConfigureScene(bodyBalance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 10 encirclement explosion applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 10 WebGL")]
        public static void BuildStep10WebGL()
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step10", "WebGL"));
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
                    $"Step 10 WebGL build failed: {summary.result}, errors {summary.totalErrors}, warnings {summary.totalWarnings}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 10 WebGL succeeded: {outputPath}, " +
                $"errors {summary.totalErrors}, warnings {summary.totalWarnings}, size {summary.totalSize} bytes.");
        }

        private static void ConfigureScene(OSBodyBalanceData bodyBalance)
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
                var bodyChain = RequireComponent<OSBodyChain>(gameRoot.transform, "PlayerRoot/BodyChain");
                var session = RequireComponent<OSGameSessionController>(systems, "OSGameSessionController");
                var registry = RequireComponent<OSEnemyRegistry>(systems, "OSEnemyRegistry");
                var growth = RequireComponent<OSBodyGrowthController>(systems, "OSBodyGrowthController");
                var health = head.GetComponent<OSPlayerHealth>()
                             ?? throw new InvalidOperationException("Head is missing OSPlayerHealth.");

                var controllerObject = GetOrCreateChild(systems, "OSExplosionController").gameObject;
                var controller = GetOrAdd<OSExplosionController>(controllerObject);
                Assign(controller, "sessionController", session);
                Assign(controller, "bodyChain", bodyChain);
                Assign(controller, "enemyRegistry", registry);
                Assign(controller, "playerHealth", health);
                Assign(controller, "bodyGrowth", growth);
                Assign(controller, "bodyBalance", bodyBalance);

                var circleRoot = GetOrCreateChild(systems, "OSExplosionTelegraphView");
                for (var index = circleRoot.childCount - 1; index >= 0; index--)
                {
                    UnityEngine.Object.DestroyImmediate(circleRoot.GetChild(index).gameObject);
                }

                var headRenderer = head.GetComponent<SpriteRenderer>();
                var circles = new LineRenderer[TelegraphCircleCapacity];
                for (var index = 0; index < circles.Length; index++)
                {
                    var circleObject = new GameObject($"ReservedCircle_{index:00}", typeof(LineRenderer));
                    circleObject.transform.SetParent(circleRoot, false);
                    var line = circleObject.GetComponent<LineRenderer>();
                    line.enabled = false;
                    line.sortingOrder = 120;
                    line.sharedMaterial = headRenderer != null ? headRenderer.sharedMaterial : null;
                    circles[index] = line;
                }

                ConfigureHud(canvas, controller, bodyChain, circles);

                var foundationLabel = RequireComponent<TMP_Text>(canvas, "FoundationLabel");
                foundationLabel.text = "STEP 10  |  RESERVE → TELEGRAPH → BLAST → REGROW";
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

        private static void ConfigureHud(
            Transform canvas,
            OSExplosionController controller,
            OSBodyChain bodyChain,
            LineRenderer[] circles)
        {
            var combatHud = RequireTransform(canvas, "CombatHUD");
            var font = RequireComponent<TMP_Text>(canvas, "FoundationLabel").font;
            var hudRoot = GetOrCreateRectChild(combatHud, "ExplosionHUD");
            var rootRect = hudRoot as RectTransform
                           ?? throw new InvalidOperationException("ExplosionHUD must use RectTransform.");
            SetRect(rootRect, new Vector2(0f, -36f), new Vector2(510f, 66f));

            for (var index = hudRoot.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(hudRoot.GetChild(index).gameObject);
            }

            var status = CreateText(
                hudRoot,
                "ExplosionStatusLabel",
                "BLAST [SPACE]  |  BODY 0/4",
                font,
                18f,
                TextAlignmentOptions.Center);
            status.color = new Color32(255, 214, 116, 255);
            SetRect(status.rectTransform, new Vector2(0f, 13f), new Vector2(510f, 28f));

            var feedback = CreateText(
                hudRoot,
                "ExplosionFeedbackLabel",
                string.Empty,
                font,
                16f,
                TextAlignmentOptions.Center);
            feedback.color = new Color32(255, 126, 92, 255);
            SetRect(feedback.rectTransform, new Vector2(0f, -14f), new Vector2(510f, 26f));

            var presenter = GetOrAdd<OSExplosionPresenter>(hudRoot.gameObject);
            Assign(presenter, "explosionController", controller);
            Assign(presenter, "bodyChain", bodyChain);
            Assign(presenter, "statusLabel", status);
            Assign(presenter, "feedbackLabel", feedback);
            AssignArray(presenter, "reservationCircles", circles);
            EditorUtility.SetDirty(hudRoot.gameObject);
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var target = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            target.transform.SetParent(parent, false);
            var label = target.GetComponent<TextMeshProUGUI>();
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
            var property = serialized.FindProperty(propertyName)
                           ?? throw new InvalidOperationException(
                               $"Serialized property '{propertyName}' is missing from '{target.name}'.");
            property.objectReferenceValue = value;
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
