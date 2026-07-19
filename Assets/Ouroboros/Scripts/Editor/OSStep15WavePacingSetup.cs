using System;
using System.IO;
using System.Linq;
using Ouroboros.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ouroboros.Editor
{
    public static class OSStep15WavePacingSetup
    {
        private const string WavePath = "Assets/Ouroboros/Data/Waves/OSWaveSchedule.asset";
        private const string WebGLProfilePath =
            "Assets/Ouroboros/BuildProfiles/WebGL Development.asset";

        [MenuItem("Ouroboros/Setup/Apply Step 15.9 Slower Wave Pacing")]
        public static void ApplyStep15WavePacing()
        {
            var waves = AssetDatabase.LoadAssetAtPath<OSWaveScheduleData>(WavePath)
                        ?? throw new InvalidOperationException(
                            $"Wave schedule is missing at '{WavePath}'.");
            var serialized = new SerializedObject(waves);
            OSStep02DataSetup.ConfigureWaves(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(waves);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var validation = waves.Validate();
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(validation.Message);
            }

            Debug.Log(
                "[OUROBOROS][SETUP] Step 15.9 reduced wave base spawn rates by 20% " +
                "and uses 1.08 per-minute growth.");
        }

        [MenuItem("Ouroboros/Build/Build Step 15.9 WebGL")]
        public static void BuildStep15WavePacingWebGL()
        {
            ApplyStep15WavePacing();
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step15_9", "WebGL"));
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
                    $"Step 15.9 WebGL build failed: {summary.result}, " +
                    $"errors {summary.totalErrors}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 15.9 WebGL succeeded at '{outputPath}' " +
                $"with errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }
    }
}
