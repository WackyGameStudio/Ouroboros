using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ouroboros.Editor
{
    public static class OSStep15RewardClaritySetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string WebGLProfilePath =
            "Assets/Ouroboros/BuildProfiles/WebGL Development.asset";

        [MenuItem("Ouroboros/Setup/Apply Step 15.19 Reward Clarity")]
        public static void ApplyStep15RewardClarity()
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
                var panel = gameRoot.transform.Find("Canvas/LevelUpPanel")
                            ?? throw new InvalidOperationException("LevelUpPanel is missing.");
                var labels = panel.GetComponentsInChildren<Button>(true)
                    .Where(button => button.name.StartsWith(
                        "UpgradeCard_",
                        StringComparison.Ordinal))
                    .OrderBy(button => button.transform.GetSiblingIndex())
                    .Select(button => button.GetComponentInChildren<TMP_Text>(true))
                    .ToArray();
                if (labels.Length != 3 || labels.Any(label => label == null))
                {
                    throw new InvalidOperationException(
                        "LevelUpPanel must contain exactly three reward card labels.");
                }

                if (!HasCurrentLayout(labels))
                {
                    foreach (var label in labels)
                    {
                        label.enableAutoSizing = true;
                        label.fontSizeMin = 12f;
                        label.fontSizeMax = 18f;
                        label.textWrappingMode = TextWrappingModes.Normal;
                        label.overflowMode = TextOverflowModes.Overflow;
                        label.lineSpacing = 1f;
                        EditorUtility.SetDirty(label);
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (openedForSetup && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[OUROBOROS][SETUP] Step 15.19 readable reward descriptions and card layout applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 15.19 WebGL")]
        public static void BuildStep15RewardClarityWebGL()
        {
            ApplyStep15RewardClarity();
            BuildWebGL();
        }

        private static bool HasCurrentLayout(TMP_Text[] labels)
        {
            return labels.All(label =>
                label.enableAutoSizing &&
                Mathf.Approximately(label.fontSizeMin, 12f) &&
                Mathf.Approximately(label.fontSizeMax, 18f) &&
                label.textWrappingMode == TextWrappingModes.Normal &&
                label.overflowMode == TextOverflowModes.Overflow &&
                Mathf.Approximately(label.lineSpacing, 1f));
        }

        private static void BuildWebGL()
        {
            var outputPath = Path.GetFullPath(
                Path.Combine("Builds", "Step15_19", "WebGL"));
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
                throw new BuildFailedException(
                    "No enabled scenes are configured for WebGL.");
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
                    $"Step 15.19 WebGL build failed: {summary.result}, " +
                    $"errors {summary.totalErrors}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 15.19 WebGL succeeded at '{outputPath}' " +
                $"with errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name)
                   ?? throw new InvalidOperationException(
                       $"Root '{name}' is missing from '{scene.path}'.");
        }
    }
}
