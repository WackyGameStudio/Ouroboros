using System;
using System.IO;
using System.Linq;
using Ouroboros.Core;
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
    public static class OSStep15BodyRecoverySetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string BodyBalancePath = "Assets/Ouroboros/Data/Balance/OSBodyBalance.asset";
        private const string WebGLProfilePath =
            "Assets/Ouroboros/BuildProfiles/WebGL Development.asset";

        [MenuItem("Ouroboros/Setup/Apply Step 15.5 Body Arsenal And Recovery")]
        public static void ApplyStep15BodyRecovery()
        {
            ConfigureBodyBalance();
            ConfigureRecoveryController();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[OUROBOROS][SETUP] Step 15.5 attack damage 10, laser length 14, " +
                "and role-preserving severed body recovery applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 15.5 WebGL")]
        public static void BuildStep15BodyRecoveryWebGL()
        {
            ApplyStep15BodyRecovery();
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step15_5", "WebGL"));
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
                    $"Step 15.5 WebGL build failed: {summary.result}, errors {summary.totalErrors}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 15.5 WebGL succeeded at '{outputPath}' " +
                $"with errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static void ConfigureBodyBalance()
        {
            var balance = AssetDatabase.LoadAssetAtPath<OSBodyBalanceData>(BodyBalancePath)
                          ?? throw new InvalidOperationException(
                              $"Body balance asset is missing at '{BodyBalancePath}'.");
            var serialized = new SerializedObject(balance);
            var definitions = serialized.FindProperty("roleDefinitions")
                              ?? throw new InvalidOperationException(
                                  "OSBodyBalanceData.roleDefinitions is missing.");
            var foundAttack = false;
            var foundLaser = false;
            for (var index = 0; index < definitions.arraySize; index++)
            {
                var definition = definitions.GetArrayElementAtIndex(index);
                var role = (OSBodyRoleType)definition.FindPropertyRelative("roleType").enumValueIndex;
                if (role == OSBodyRoleType.Attack)
                {
                    definition.FindPropertyRelative("damage").floatValue = 10f;
                    foundAttack = true;
                }
                else if (role == OSBodyRoleType.Laser)
                {
                    definition.FindPropertyRelative("range").floatValue = 14f;
                    foundLaser = true;
                }
            }

            if (!foundAttack || !foundLaser)
            {
                throw new InvalidOperationException("Attack or Laser role definition is missing.");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(balance);
        }

        private static void ConfigureRecoveryController()
        {
            var scene = SceneManager.GetSceneByPath(GameScenePath);
            var openedForSetup = !scene.isLoaded;
            if (openedForSetup)
            {
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var roots = scene.GetRootGameObjects();
                var chain = roots.Select(root => root.GetComponentInChildren<OSBodyChain>(true))
                    .FirstOrDefault(candidate => candidate != null)
                    ?? throw new InvalidOperationException("OSBodyChain is missing from 20_Game.");
                var spawner = roots.Select(root => root.GetComponentInChildren<OSPickupSpawner>(true))
                    .FirstOrDefault(candidate => candidate != null)
                    ?? throw new InvalidOperationException("OSPickupSpawner is missing from 20_Game.");
                var session = roots.Select(root => root.GetComponentInChildren<OSGameSessionController>(true))
                    .FirstOrDefault(candidate => candidate != null)
                    ?? throw new InvalidOperationException(
                        "OSGameSessionController is missing from 20_Game.");
                var recovery = spawner.GetComponent<OSSeveredBodyDropController>()
                               ?? spawner.gameObject.AddComponent<OSSeveredBodyDropController>();
                var serialized = new SerializedObject(recovery);
                serialized.FindProperty("bodyChain").objectReferenceValue = chain;
                serialized.FindProperty("pickupSpawner").objectReferenceValue = spawner;
                serialized.FindProperty("sessionController").objectReferenceValue = session;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(recovery);
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
    }
}
