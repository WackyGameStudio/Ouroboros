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
    public static class OSStep15LaserCoverageSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string WebGLProfilePath =
            "Assets/Ouroboros/BuildProfiles/WebGL Development.asset";

        [MenuItem("Ouroboros/Setup/Apply Step 15.4 Laser Collider Coverage")]
        public static void ApplyStep15LaserCoverage()
        {
            var enemyHurtboxLayer = LayerMask.NameToLayer("EnemyHurtbox");
            if (enemyHurtboxLayer < 0)
            {
                throw new InvalidOperationException("Required layer 'EnemyHurtbox' is missing.");
            }

            var scene = SceneManager.GetSceneByPath(GameScenePath);
            var openedForSetup = !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var laser = scene.GetRootGameObjects()
                    .Select(root => root.GetComponentInChildren<OSLaserBodyRole>(true))
                    .FirstOrDefault(candidate => candidate != null)
                    ?? throw new InvalidOperationException("OSLaserBodyRole is missing from 20_Game.");
                var serialized = new SerializedObject(laser);
                var mask = serialized.FindProperty("enemyHurtboxMask")
                           ?? throw new InvalidOperationException(
                               "OSLaserBodyRole.enemyHurtboxMask is missing.");
                mask.intValue = 1 << enemyHurtboxLayer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(laser);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log(
                    "[OUROBOROS][SETUP] Step 15.4 laser EnemyHurtbox collider coverage applied.");
            }
            finally
            {
                if (openedForSetup && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [MenuItem("Ouroboros/Build/Build Step 15.4 WebGL")]
        public static void BuildStep15LaserCoverageWebGL()
        {
            ApplyStep15LaserCoverage();
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step15_4", "WebGL"));
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
                    $"Step 15.4 WebGL build failed: {summary.result}, errors {summary.totalErrors}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 15.4 WebGL succeeded at '{outputPath}' " +
                $"with errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }
    }
}
