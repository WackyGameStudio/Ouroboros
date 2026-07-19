using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ouroboros.Editor
{
    public static class OSStep15ShieldGrowthSetup
    {
        private const string BodyBalancePath =
            "Assets/Ouroboros/Data/Balance/OSBodyBalance.asset";
        private const string UpgradeCatalogPath =
            "Assets/Ouroboros/Data/Upgrades/OSUpgradeCatalog.asset";
        private const string FragmentEfficiencyId = "body_fragment_efficiency";

        [MenuItem("Ouroboros/Setup/Apply Step 15.2 Shield And Growth")]
        public static void ApplyStep15ShieldAndGrowth()
        {
            ConfigureBodyBalance();
            ConfigureFragmentEfficiencyUpgrade();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[OUROBOROS][SETUP] Step 15.2 contact-point shield defense and 6-fragment growth applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 15.2 WebGL")]
        public static void BuildStep15ShieldGrowthWebGL()
        {
            ApplyStep15ShieldAndGrowth();
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step15_2", "WebGL"));
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
                    $"Step 15.2 WebGL build failed: {summary.result}, errors {summary.totalErrors}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 15.2 WebGL succeeded at '{outputPath}' " +
                $"with errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static void ConfigureBodyBalance()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(BodyBalancePath)
                        ?? throw new InvalidOperationException(
                            $"Body balance asset is missing at '{BodyBalancePath}'.");
            var serialized = new SerializedObject(asset);
            Require(serialized, "fragmentRequirement").intValue = 6;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void ConfigureFragmentEfficiencyUpgrade()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(UpgradeCatalogPath)
                        ?? throw new InvalidOperationException(
                            $"Upgrade catalog is missing at '{UpgradeCatalogPath}'.");
            var serialized = new SerializedObject(asset);
            var entries = Require(serialized, "entries");
            for (var index = 0; index < entries.arraySize; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("id")?.stringValue != FragmentEfficiencyId)
                {
                    continue;
                }

                RequireRelative(entry, "perLevelValue").floatValue = -0.17f;
                RequireRelative(entry, "clampMinimum").floatValue = 4f;
                RequireRelative(entry, "clampMaximum").floatValue = 6f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                return;
            }

            throw new InvalidOperationException(
                $"Upgrade '{FragmentEfficiencyId}' is missing from '{UpgradeCatalogPath}'.");
        }

        private static SerializedProperty Require(SerializedObject serialized, string propertyName)
        {
            return serialized.FindProperty(propertyName)
                   ?? throw new InvalidOperationException(
                       $"Serialized property '{propertyName}' is missing on '{serialized.targetObject.name}'.");
        }

        private static SerializedProperty RequireRelative(
            SerializedProperty parent,
            string propertyName)
        {
            return parent.FindPropertyRelative(propertyName)
                   ?? throw new InvalidOperationException(
                       $"Serialized property '{propertyName}' is missing from upgrade entry.");
        }
    }
}
