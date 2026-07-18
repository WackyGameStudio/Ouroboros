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
using UnityEngine.UI;

namespace Ouroboros.Editor
{
    public static class OSStep09DamageSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string PlayerBalancePath = "Assets/Ouroboros/Data/Balance/OSPlayerBalance.asset";
        private const string BodyBalancePath = "Assets/Ouroboros/Data/Balance/OSBodyBalance.asset";
        private const string SegmentPrefabPath = "Assets/Ouroboros/Prefabs/Player/PF_BodySegment.prefab";

        [MenuItem("Ouroboros/Setup/Apply Step 09 Damage And Cutting")]
        public static void ApplyStep09DamageAndCutting()
        {
            var playerBalance = LoadRequired<OSPlayerBalanceData>(PlayerBalancePath);
            var bodyBalance = LoadRequired<OSBodyBalanceData>(BodyBalancePath);
            ConfigureBodySegmentPrefab();
            ConfigureScene(playerBalance, bodyBalance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 09 damage, health, and body cutting applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 09 WebGL")]
        public static void BuildStep09WebGL()
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step09", "WebGL"));
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
                    $"Step 09 WebGL build failed: {summary.result}, errors {summary.totalErrors}, warnings {summary.totalWarnings}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 09 WebGL succeeded: {outputPath}, " +
                $"errors {summary.totalErrors}, warnings {summary.totalWarnings}, size {summary.totalSize} bytes.");
        }

        private static void ConfigureBodySegmentPrefab()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(SegmentPrefabPath);
            try
            {
                var view = prefabRoot.GetComponent<OSBodySegmentView>()
                           ?? throw new InvalidOperationException("Body segment prefab is missing OSBodySegmentView.");
                var hurtbox = prefabRoot.transform.Find("BodyHurtbox")
                              ?? throw new InvalidOperationException("Body segment prefab is missing BodyHurtbox.");
                var identity = GetOrAdd<OSCombatTargetIdentity>(hurtbox.gameObject);
                identity.Configure(1, OSTargetKind.PlayerBody);
                Assign(view, "targetIdentity", identity);
                EditorUtility.SetDirty(identity);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, SegmentPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ConfigureScene(
            OSPlayerBalanceData playerBalance,
            OSBodyBalanceData bodyBalance)
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
                var bodyChain = RequireComponent<OSBodyChain>(gameRoot.transform, "PlayerRoot/BodyChain");
                var session = RequireComponent<OSGameSessionController>(systems, "OSGameSessionController");
                var poolContext = RequireComponent<OSEnemyPoolContext>(systems, "OSPoolRegistry");
                var canvas = RequireTransform(gameRoot.transform, "Canvas");

                Assign(bodyChain, "bodyBalance", bodyBalance);

                var health = GetOrAdd<OSPlayerHealth>(head.gameObject);
                Assign(health, "playerBalance", playerBalance);
                Assign(health, "sessionController", session);

                var resolverObject = GetOrCreateChild(systems, "OSPlayerCombatResolver").gameObject;
                var resolver = GetOrAdd<OSPlayerCombatResolver>(resolverObject);
                Assign(resolver, "sessionController", session);
                Assign(resolver, "playerHealth", health);
                Assign(resolver, "bodyChain", bodyChain);
                Assign(poolContext, "playerCombatResolver", resolver);

                var headHurtbox = RequireTransform(head, "HeadHurtbox");
                var headIdentity = GetOrAdd<OSCombatTargetIdentity>(headHurtbox.gameObject);
                headIdentity.Configure(1, OSTargetKind.PlayerHead);
                EditorUtility.SetDirty(headIdentity);

                ConfigureHealthHud(canvas, health, bodyChain);

                var foundationLabel = RequireComponent<TMP_Text>(canvas, "FoundationLabel");
                foundationLabel.text = "STEP 09  |  CORE HP + DETERMINISTIC BODY CUTTING";
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

        private static void ConfigureHealthHud(
            Transform canvas,
            OSPlayerHealth health,
            OSBodyChain bodyChain)
        {
            var combatHud = RequireTransform(canvas, "CombatHUD");
            var font = RequireComponent<TMP_Text>(canvas, "FoundationLabel").font;
            var healthRoot = GetOrCreateRectChild(combatHud, "PlayerHealthHUD");
            var rootRect = healthRoot as RectTransform
                           ?? throw new InvalidOperationException("PlayerHealthHUD must use RectTransform.");
            SetRect(rootRect, new Vector2(0f, 54f), new Vector2(500f, 78f));

            for (var index = healthRoot.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(healthRoot.GetChild(index).gameObject);
            }

            var background = CreateImage(
                healthRoot,
                "HealthBarBackground",
                new Color32(31, 38, 56, 230));
            SetRect(background.rectTransform, new Vector2(0f, 4f), new Vector2(470f, 22f));

            var fill = CreateImage(
                background.transform,
                "HealthBarFill",
                new Color32(80, 224, 142, 255));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;
            Stretch(fill.rectTransform, 3f);

            var hpLabel = CreateText(
                healthRoot,
                "HealthLabel",
                "CORE HP  100 / 100",
                font,
                19f,
                TextAlignmentOptions.Center);
            SetRect(hpLabel.rectTransform, new Vector2(0f, 4f), new Vector2(470f, 28f));

            var feedbackLabel = CreateText(
                healthRoot,
                "DamageFeedbackLabel",
                string.Empty,
                font,
                18f,
                TextAlignmentOptions.Center);
            feedbackLabel.color = new Color32(255, 196, 103, 255);
            SetRect(feedbackLabel.rectTransform, new Vector2(0f, -25f), new Vector2(500f, 28f));

            var presenter = GetOrAdd<OSPlayerHealthPresenter>(healthRoot.gameObject);
            Assign(presenter, "playerHealth", health);
            Assign(presenter, "bodyChain", bodyChain);
            Assign(presenter, "healthLabel", hpLabel);
            Assign(presenter, "feedbackLabel", feedbackLabel);
            Assign(presenter, "healthFill", fill);
            EditorUtility.SetDirty(healthRoot.gameObject);
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            target.transform.SetParent(parent, false);
            var image = target.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
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
