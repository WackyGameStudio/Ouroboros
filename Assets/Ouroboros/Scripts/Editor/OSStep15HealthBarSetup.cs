using System;
using System.IO;
using System.Linq;
using Ouroboros.UI;
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
    public static class OSStep15HealthBarSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string WebGLProfilePath =
            "Assets/Ouroboros/BuildProfiles/WebGL Development.asset";

        [MenuItem("Ouroboros/Setup/Apply Step 15.16 Player HP Bar")]
        public static void ApplyStep15PlayerHealthBar()
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
                var canvas = gameRoot.transform.Find("Canvas")
                             ?? throw new InvalidOperationException("Canvas is missing.");
                var panel = canvas.Find("ReadabilityHUD/CombatSummaryPanel")
                            ?? throw new InvalidOperationException(
                                "Consolidated combat summary panel is missing.");
                var primary = panel.Find("PrimaryLabel")?.GetComponent<TMP_Text>()
                              ?? throw new InvalidOperationException("PrimaryLabel is missing.");
                var hud = gameRoot.GetComponentInChildren<OSCombatHudPresenter>(true)
                          ?? throw new InvalidOperationException(
                              "OSCombatHudPresenter is missing.");

                if (!HasCurrentConfiguration(canvas, panel, primary, hud))
                {
                    var fill = ConfigureConsolidatedHealthBar(canvas, panel, primary);
                    hud.ConfigureHealthFill(fill);
                    EditorUtility.SetDirty(hud);
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
                "[OUROBOROS][SETUP] Step 15.16 consolidated player HP bar applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 15.16 WebGL")]
        public static void BuildStep15PlayerHealthBarWebGL()
        {
            ApplyStep15PlayerHealthBar();
            BuildWebGL("Step15_16", "Step 15.16");
        }

        [MenuItem("Ouroboros/Setup/Apply Step 15.18 HP Bar Visual Fix")]
        public static void ApplyStep15PlayerHealthBarVisualFix()
        {
            ApplyStep15PlayerHealthBar();
            Debug.Log(
                "[OUROBOROS][SETUP] Step 15.18 visible HP depletion and upper HUD placement applied.");
        }

        [MenuItem("Ouroboros/Build/Build Step 15.18 WebGL")]
        public static void BuildStep15PlayerHealthBarVisualFixWebGL()
        {
            ApplyStep15PlayerHealthBarVisualFix();
            BuildWebGL("Step15_18", "Step 15.18");
        }

        private static void BuildWebGL(string stepFolder, string stepLabel)
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", stepFolder, "WebGL"));
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
                    $"{stepLabel} WebGL build failed: {summary.result}, " +
                    $"errors {summary.totalErrors}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] {stepLabel} WebGL succeeded at '{outputPath}' " +
                $"with errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        internal static Image ConfigureConsolidatedHealthBar(
            Transform canvas,
            Transform panel,
            TMP_Text primaryLabel)
        {
            var background = GetOrCreateImage(panel, "HealthBarBackground");
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = new Vector2(0f, 1f);
            backgroundRect.anchorMax = new Vector2(1f, 1f);
            backgroundRect.pivot = new Vector2(0.5f, 1f);
            backgroundRect.anchoredPosition = new Vector2(0f, -36f);
            backgroundRect.sizeDelta = new Vector2(-28f, 12f);
            background.color = new Color32(31, 38, 56, 235);
            background.raycastTarget = false;

            var fill = GetOrCreateImage(background.transform, "HealthBarFill");
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(-4f, -4f);
            fill.color = new Color32(80, 224, 142, 255);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")
                          ?? throw new InvalidOperationException(
                              "Built-in UI fill sprite is unavailable.");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillClockwise = true;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            var primaryRect = (RectTransform)primaryLabel.transform;
            primaryRect.offsetMin = new Vector2(14f, 8f);
            primaryRect.offsetMax = new Vector2(-14f, -8f);

            var legacy = canvas.Find("CombatHUD/PlayerHealthHUD");
            if (legacy != null)
            {
                legacy.gameObject.SetActive(false);
                EditorUtility.SetDirty(legacy.gameObject);
            }

            EditorUtility.SetDirty(background);
            EditorUtility.SetDirty(fill);
            EditorUtility.SetDirty(primaryLabel);
            return fill;
        }

        private static Image GetOrCreateImage(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)).transform;
                child.SetParent(parent, false);
            }

            return child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>();
        }

        private static bool HasCurrentConfiguration(
            Transform canvas,
            Transform panel,
            TMP_Text primaryLabel,
            OSCombatHudPresenter hud)
        {
            var background = panel.Find("HealthBarBackground")?.GetComponent<Image>();
            var fill = background != null
                ? background.transform.Find("HealthBarFill")?.GetComponent<Image>()
                : null;
            if (background == null || fill == null)
            {
                return false;
            }

            var backgroundRect = (RectTransform)background.transform;
            var fillRect = (RectTransform)fill.transform;
            var primaryRect = (RectTransform)primaryLabel.transform;
            var legacy = canvas.Find("CombatHUD/PlayerHealthHUD");
            var serializedHud = new SerializedObject(hud);
            var healthFill = serializedHud.FindProperty("healthFill");

            return healthFill != null
                   && healthFill.objectReferenceValue == fill
                   && (legacy == null || !legacy.gameObject.activeSelf)
                   && Approximately(backgroundRect.anchorMin, new Vector2(0f, 1f))
                   && Approximately(backgroundRect.anchorMax, new Vector2(1f, 1f))
                   && Approximately(backgroundRect.pivot, new Vector2(0.5f, 1f))
                   && Approximately(backgroundRect.anchoredPosition, new Vector2(0f, -36f))
                   && Approximately(backgroundRect.sizeDelta, new Vector2(-28f, 12f))
                   && Approximately(background.color, new Color32(31, 38, 56, 235))
                   && !background.raycastTarget
                   && Approximately(fillRect.anchorMin, Vector2.zero)
                   && Approximately(fillRect.anchorMax, Vector2.one)
                   && Approximately(fillRect.pivot, new Vector2(0.5f, 0.5f))
                   && Approximately(fillRect.anchoredPosition, Vector2.zero)
                   && Approximately(fillRect.sizeDelta, new Vector2(-4f, -4f))
                   && Approximately(fill.color, new Color32(80, 224, 142, 255))
                   && fill.sprite != null
                   && fill.sprite.name == "UISprite"
                   && fill.type == Image.Type.Filled
                   && fill.fillMethod == Image.FillMethod.Horizontal
                   && fill.fillOrigin == (int)Image.OriginHorizontal.Left
                   && fill.fillClockwise
                   && !fill.raycastTarget
                   && Approximately(primaryRect.offsetMin, new Vector2(14f, 8f))
                   && Approximately(primaryRect.offsetMax, new Vector2(-14f, -8f));
        }

        private static bool Approximately(Vector2 actual, Vector2 expected)
        {
            return (actual - expected).sqrMagnitude <= 0.000001f;
        }

        private static bool Approximately(Color actual, Color32 expected)
        {
            var expectedColor = (Color)expected;
            return Mathf.Abs(actual.r - expectedColor.r) <= 0.0001f
                   && Mathf.Abs(actual.g - expectedColor.g) <= 0.0001f
                   && Mathf.Abs(actual.b - expectedColor.b) <= 0.0001f
                   && Mathf.Abs(actual.a - expectedColor.a) <= 0.0001f;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name)
                   ?? throw new InvalidOperationException(
                       $"Root '{name}' is missing from '{scene.path}'.");
        }
    }
}
