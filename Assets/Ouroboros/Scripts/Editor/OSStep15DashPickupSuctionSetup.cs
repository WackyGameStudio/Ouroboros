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
    public static class OSStep15DashPickupSuctionSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string WebGLProfilePath =
            "Assets/Ouroboros/BuildProfiles/WebGL Development.asset";
        private const float DashSuctionSpeed = 24f;

        [MenuItem("Ouroboros/Setup/Apply Step 15.15 Dash Pickup Suction")]
        public static void ApplyStep15DashPickupSuction()
        {
            ConfigureScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[OUROBOROS][SETUP] Step 15.15 dash-path pickup suction applied at 24 units/second.");
        }

        [MenuItem("Ouroboros/Build/Build Step 15.15 WebGL")]
        public static void BuildStep15DashPickupSuctionWebGL()
        {
            ApplyStep15DashPickupSuction();

            var outputPath = Path.GetFullPath(
                Path.Combine("Builds", "Step15_15", "WebGL"));
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
                    $"Step 15.15 WebGL build failed: {summary.result}, errors {summary.totalErrors}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 15.15 WebGL succeeded at '{outputPath}' " +
                $"with errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
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
                var root = FindRoot(scene, "GameRoot");
                var dash = root.GetComponentInChildren<OSBodyDashController>(true)
                           ?? throw new InvalidOperationException("OSBodyDashController is missing.");
                var spawner = root.GetComponentInChildren<OSPickupSpawner>(true)
                              ?? throw new InvalidOperationException("OSPickupSpawner is missing.");
                var collector = root.GetComponentInChildren<OSPickupCollector>(true)
                                ?? throw new InvalidOperationException("OSPickupCollector is missing.");

                var spawnerSerialized = new SerializedObject(spawner);
                var speedProperty = spawnerSerialized.FindProperty("dashSuctionSpeed");
                var changed = !Mathf.Approximately(speedProperty.floatValue, DashSuctionSpeed);
                if (changed)
                {
                    speedProperty.floatValue = DashSuctionSpeed;
                    spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(spawner);
                }

                var dashSerialized = new SerializedObject(dash);
                var spawnerProperty = dashSerialized.FindProperty("pickupSpawner");
                var collectorProperty = dashSerialized.FindProperty("pickupCollector");
                if (spawnerProperty.objectReferenceValue != spawner ||
                    collectorProperty.objectReferenceValue != collector)
                {
                    spawnerProperty.objectReferenceValue = spawner;
                    collectorProperty.objectReferenceValue = collector;
                    dashSerialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(dash);
                    changed = true;
                }

                if (changed)
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
    }
}
