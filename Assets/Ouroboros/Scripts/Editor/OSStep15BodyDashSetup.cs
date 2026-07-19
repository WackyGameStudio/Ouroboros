using System;
using System.IO;
using System.Linq;
using Ouroboros.Core;
using Ouroboros.Runtime;
using Ouroboros.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ouroboros.Editor
{
    public static class OSStep15BodyDashSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string BodyBalancePath = "Assets/Ouroboros/Data/Balance/OSBodyBalance.asset";
        private const string WebGLProfilePath =
            "Assets/Ouroboros/BuildProfiles/WebGL Development.asset";

        [MenuItem("Ouroboros/Setup/Apply Step 15.7 Body Convergence Dash")]
        public static void ApplyStep15BodyDash()
        {
            ApplyBodyDashSetup(
                "Step 15.7 Space input now runs the 0.5s body convergence dash.");
        }

        [MenuItem("Ouroboros/Setup/Apply Step 15.8 Natural Body Unfold")]
        public static void ApplyStep15NaturalBodyUnfold()
        {
            ApplyBodyDashSetup(
                "Step 15.8 keeps the body clumped at the dash endpoint and unfolds it from head movement.");
        }

        private static void ApplyBodyDashSetup(string message)
        {
            ConfigureBalance();
            ConfigureScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OUROBOROS][SETUP] {message}");
        }

        [MenuItem("Ouroboros/Build/Build Step 15.7 WebGL")]
        public static void BuildStep15BodyDashWebGL()
        {
            ApplyStep15BodyDash();
            BuildWebGL("Step 15.7", "Step15_7");
        }

        [MenuItem("Ouroboros/Build/Build Step 15.8 WebGL")]
        public static void BuildStep15NaturalBodyUnfoldWebGL()
        {
            ApplyStep15NaturalBodyUnfold();
            BuildWebGL("Step 15.8", "Step15_8");
        }

        private static void BuildWebGL(string stepLabel, string outputFolder)
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", outputFolder, "WebGL"));
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(WebGLProfilePath)
                          ?? throw new BuildFailedException(
                              $"WebGL build profile is missing at '{WebGLProfilePath}'.");
            BuildProfile.SetActiveBuildProfile(profile);
            var scenes = profile.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes are configured for WebGL.");
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
                    $"{stepLabel} WebGL build failed: {summary.result}, errors {summary.totalErrors}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] {stepLabel} WebGL succeeded at '{outputPath}' " +
                $"with errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static void ConfigureBalance()
        {
            var balance = AssetDatabase.LoadAssetAtPath<OSBodyBalanceData>(BodyBalancePath)
                          ?? throw new InvalidOperationException(
                              $"Body balance is missing at '{BodyBalancePath}'.");
            var serialized = new SerializedObject(balance);
            var dash = serialized.FindProperty("bodyDash")
                       ?? throw new InvalidOperationException("OSBodyBalanceData.bodyDash is missing.");
            dash.FindPropertyRelative("duration").floatValue = 0.5f;
            dash.FindPropertyRelative("distance").floatValue = 4.5f;
            dash.FindPropertyRelative("cooldown").floatValue = 2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(balance);
        }

        private static void ConfigureScene()
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
                var chain = gameRoot.GetComponentInChildren<OSBodyChain>(true)
                            ?? throw new InvalidOperationException("OSBodyChain is missing.");
                var session = systems.GetComponentInChildren<OSGameSessionController>(true)
                              ?? throw new InvalidOperationException("OSGameSessionController is missing.");
                var growth = systems.GetComponentInChildren<OSBodyGrowthController>(true)
                             ?? throw new InvalidOperationException("OSBodyGrowthController is missing.");
                var player = head.GetComponent<OSPlayerController>()
                             ?? throw new InvalidOperationException("OSPlayerController is missing.");
                var balance = AssetDatabase.LoadAssetAtPath<OSBodyBalanceData>(BodyBalancePath);

                var controller = systems.GetComponentInChildren<OSBodyDashController>(true);
                if (controller == null)
                {
                    var controllerRoot = new GameObject("OSBodyDashController");
                    controllerRoot.transform.SetParent(systems, false);
                    controller = controllerRoot.AddComponent<OSBodyDashController>();
                }

                controller.gameObject.name = "OSBodyDashController";
                Assign(controller, "sessionController", session);
                Assign(controller, "bodyChain", chain);
                Assign(controller, "playerController", player);
                Assign(controller, "bodyGrowth", growth);
                Assign(controller, "bodyBalance", balance);

                AssignIfPresent(systems.GetComponentInChildren<OSLevelUpController>(true),
                    "bodyDashController", controller);
                AssignIfPresent(systems.GetComponentInChildren<OSRunSummaryController>(true),
                    "bodyDashController", controller);
                AssignIfPresent(gameRoot.GetComponentInChildren<OSCombatHudPresenter>(true),
                    "bodyDashController", controller);
                AssignIfPresent(gameRoot.GetComponentInChildren<OSTutorialPresenter>(true),
                    "bodyDashController", controller);
                AssignIfPresent(gameRoot.GetComponentInChildren<OSCombatFeedbackPresenter>(true),
                    "bodyDashController", controller);

                var legacyView = systems.Find("OSExplosionTelegraphView");
                if (legacyView != null)
                {
                    UnityEngine.Object.DestroyImmediate(legacyView.gameObject);
                }

                ConfigureDashHud(gameRoot.transform, controller, chain);
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

        private static void ConfigureDashHud(
            Transform gameRoot,
            OSBodyDashController controller,
            OSBodyChain chain)
        {
            var combatHud = RequireTransform(gameRoot, "Canvas/CombatHUD");
            var hud = combatHud.Find("BodyDashHUD") ?? combatHud.Find("ExplosionHUD");
            if (hud == null)
            {
                return;
            }

            hud.name = "BodyDashHUD";
            var labels = hud.GetComponentsInChildren<TMP_Text>(true);
            var status = labels.FirstOrDefault(label =>
                label.name is "BodyDashStatusLabel" or "ExplosionStatusLabel");
            var feedback = labels.FirstOrDefault(label =>
                label.name is "BodyDashFeedbackLabel" or "ExplosionFeedbackLabel");
            if (status != null)
            {
                status.name = "BodyDashStatusLabel";
                status.text = "DASH READY [SPACE]  |  4.5u / 0.5s";
            }

            if (feedback != null)
            {
                feedback.name = "BodyDashFeedbackLabel";
                feedback.text = string.Empty;
            }

            var legacyActionLabel = combatHud.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(label => label.text.Contains("BLAST", StringComparison.Ordinal));
            if (legacyActionLabel != null)
            {
                legacyActionLabel.text =
                    "DASH READY [SPACE]  4.5u / 0.5s\n" +
                    "SHIELD [O] 0/0   ATTACK [>] 0   LASER [=] 0   CONTROL [+] 0";
                EditorUtility.SetDirty(legacyActionLabel);
            }

            var presenter = hud.GetComponent<OSBodyDashPresenter>() ??
                            hud.gameObject.AddComponent<OSBodyDashPresenter>();
            presenter.Configure(controller, chain, status, feedback);
            EditorUtility.SetDirty(hud.gameObject);
        }

        private static void AssignIfPresent(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            if (target != null)
            {
                Assign(target, propertyName, value);
            }
        }

        private static void Assign(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                           ?? throw new InvalidOperationException(
                               $"Serialized property '{propertyName}' is missing from '{target.name}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
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
                   ?? throw new InvalidOperationException(
                       $"Transform '{parent.name}/{path}' is missing.");
        }
    }
}
