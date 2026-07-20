using System;
using System.IO;
using System.Linq;
using Ouroboros.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ouroboros.Editor
{
    public static class OSStep15CombatClaritySetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string WebGLProfilePath =
            "Assets/Ouroboros/BuildProfiles/WebGL Development.asset";
        private const float BossTimeLimitSeconds = 150f;

        [MenuItem("Ouroboros/Setup/Apply Step 15.17 Combat Clarity")]
        public static void ApplyStep15CombatClarity()
        {
            OSStep15BodyRecoverySetup.ConfigurePickupVisuals();
            ConfigureBossTimeoutAndLaserViews();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[OUROBOROS][SETUP] Step 15.17 boss timeout 150s, distinct pickup " +
                "silhouettes, and laser visual/hit width parity applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 15.17 WebGL")]
        public static void BuildStep15CombatClarityWebGL()
        {
            ApplyStep15CombatClarity();

            var outputPath = Path.GetFullPath(
                Path.Combine("Builds", "Step15_17", "WebGL"));
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
                    $"Step 15.17 WebGL build failed: {summary.result}, " +
                    $"errors {summary.totalErrors}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 15.17 WebGL succeeded at '{outputPath}' " +
                $"with errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static void ConfigureBossTimeoutAndLaserViews()
        {
            var scene = SceneManager.GetSceneByPath(GameScenePath);
            var openedForSetup = !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var gameRoot = scene.GetRootGameObjects()
                    .FirstOrDefault(root => root.name == "GameRoot")
                    ?? throw new InvalidOperationException("GameRoot is missing.");
                var bossEncounter = gameRoot.GetComponentInChildren<OSBossEncounterController>(true)
                                    ?? throw new InvalidOperationException(
                                        "OSBossEncounterController is missing.");
                var serializedBoss = new SerializedObject(bossEncounter);
                var timeLimit = serializedBoss.FindProperty("timeLimitSeconds")
                                ?? throw new InvalidOperationException(
                                    "OSBossEncounterController.timeLimitSeconds is missing.");
                var sceneChanged = !Mathf.Approximately(
                    timeLimit.floatValue,
                    BossTimeLimitSeconds);
                if (sceneChanged)
                {
                    timeLimit.floatValue = BossTimeLimitSeconds;
                    serializedBoss.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(bossEncounter);
                }

                foreach (var line in gameRoot.GetComponentsInChildren<LineRenderer>(true))
                {
                    if (line.transform.parent == null ||
                        line.transform.parent.name != "LaserTelegraphs" ||
                        Mathf.Approximately(line.widthMultiplier, 1f))
                    {
                        continue;
                    }

                    line.widthMultiplier = 1f;
                    EditorUtility.SetDirty(line);
                    sceneChanged = true;
                }

                if (sceneChanged)
                {
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
        }
    }
}
