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
    public static class OSStep10BodyDashSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string BodyBalancePath = "Assets/Ouroboros/Data/Balance/OSBodyBalance.asset";
        [MenuItem("Ouroboros/Setup/Apply Step 10 Body Convergence Dash")]
        public static void ApplyStep10BodyDash()
        {
            OSStep09DamageSetup.ApplyStep09DamageAndCutting();
            var bodyBalance = LoadRequired<OSBodyBalanceData>(BodyBalancePath);
            ConfigureScene(bodyBalance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 10 body convergence dash applied.");
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
                var growth = RequireComponent<OSBodyGrowthController>(systems, "OSBodyGrowthController");
                var player = head.GetComponent<OSPlayerController>()
                             ?? throw new InvalidOperationException("Head is missing OSPlayerController.");

                var controllerTransform = systems.Find("OSBodyDashController") ??
                                          systems.Find("OSExplosionController") ??
                                          GetOrCreateChild(systems, "OSBodyDashController");
                controllerTransform.name = "OSBodyDashController";
                var controller = GetOrAdd<OSBodyDashController>(controllerTransform.gameObject);
                Assign(controller, "sessionController", session);
                Assign(controller, "bodyChain", bodyChain);
                Assign(controller, "playerController", player);
                Assign(controller, "bodyGrowth", growth);
                Assign(controller, "bodyBalance", bodyBalance);

                var legacyTelegraph = systems.Find("OSExplosionTelegraphView");
                if (legacyTelegraph != null)
                {
                    UnityEngine.Object.DestroyImmediate(legacyTelegraph.gameObject);
                }

                ConfigureHud(canvas, controller, bodyChain);

                var foundationLabel = RequireComponent<TMP_Text>(canvas, "FoundationLabel");
                foundationLabel.text = "STEP 10  |  BODY CONVERGE → HEAD DASH → REFORM";
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
            OSBodyDashController controller,
            OSBodyChain bodyChain)
        {
            var combatHud = RequireTransform(canvas, "CombatHUD");
            var font = RequireComponent<TMP_Text>(canvas, "FoundationLabel").font;
            var hudRoot = combatHud.Find("BodyDashHUD") ?? combatHud.Find("ExplosionHUD") ??
                          GetOrCreateRectChild(combatHud, "BodyDashHUD");
            hudRoot.name = "BodyDashHUD";
            var rootRect = hudRoot as RectTransform
                           ?? throw new InvalidOperationException("BodyDashHUD must use RectTransform.");
            SetRect(rootRect, new Vector2(0f, -36f), new Vector2(510f, 66f));

            for (var index = hudRoot.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(hudRoot.GetChild(index).gameObject);
            }

            var status = CreateText(
                hudRoot,
                "BodyDashStatusLabel",
                "DASH READY [SPACE]  |  4.5u / 0.5s",
                font,
                18f,
                TextAlignmentOptions.Center);
            status.color = new Color32(255, 214, 116, 255);
            SetRect(status.rectTransform, new Vector2(0f, 13f), new Vector2(510f, 28f));

            var feedback = CreateText(
                hudRoot,
                "BodyDashFeedbackLabel",
                string.Empty,
                font,
                16f,
                TextAlignmentOptions.Center);
            feedback.color = new Color32(255, 126, 92, 255);
            SetRect(feedback.rectTransform, new Vector2(0f, -14f), new Vector2(510f, 26f));

            var presenter = GetOrAdd<OSBodyDashPresenter>(hudRoot.gameObject);
            presenter.Configure(controller, bodyChain, status, feedback);
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

    }
}
