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
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ouroboros.Editor
{
    public static class OSStep12LevelUpSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string PlayerBalancePath = "Assets/Ouroboros/Data/Balance/OSPlayerBalance.asset";
        private const string BodyBalancePath = "Assets/Ouroboros/Data/Balance/OSBodyBalance.asset";
        private const string UpgradeCatalogPath = "Assets/Ouroboros/Data/Upgrades/OSUpgradeCatalog.asset";

        [MenuItem("Ouroboros/Setup/Apply Step 12 Level Up And Upgrades")]
        public static void ApplyStep12LevelUpAndUpgrades()
        {
            if (!HasStep11Foundation())
            {
                OSStep11BodyRolesSetup.ApplyStep11BodyRoles();
            }

            ConfigureScene(
                LoadRequired<OSPlayerBalanceData>(PlayerBalancePath),
                LoadRequired<OSBodyBalanceData>(BodyBalancePath),
                LoadRequired<OSUpgradeCatalog>(UpgradeCatalogPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 12 level-up and upgrades applied.");
        }

        private static bool HasStep11Foundation()
        {
            var scene = SceneManager.GetSceneByPath(GameScenePath);
            var openedForCheck = !scene.isLoaded;
            if (openedForCheck)
            {
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == "GameRoot");
                if (root == null)
                {
                    return false;
                }

                return root.GetComponentInChildren<OSBodyRoleRegistry>(true) != null &&
                       root.GetComponentInChildren<OSAttackBodyRole>(true) != null &&
                       root.GetComponentInChildren<OSLaserBodyRole>(true) != null &&
                       root.GetComponentInChildren<OSControlBodyRole>(true) != null &&
                       root.GetComponentInChildren<OSShieldBodyRole>(true) != null;
            }
            finally
            {
                if (openedForCheck && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [MenuItem("Ouroboros/Build/Build Step 12 WebGL")]
        public static void BuildStep12WebGL()
        {
            var outputPath = Path.GetFullPath(Path.Combine("Builds", "Step12", "WebGL"));
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
                    $"Step 12 WebGL build failed: {summary.result}, errors {summary.totalErrors}, " +
                    $"warnings {summary.totalWarnings}.");
            }

            Debug.Log(
                $"[OUROBOROS][BUILD] Step 12 WebGL succeeded: {outputPath}, " +
                $"errors {summary.totalErrors}, warnings {summary.totalWarnings}, " +
                $"size {summary.totalSize} bytes.");
        }

        private static void ConfigureScene(
            OSPlayerBalanceData playerBalance,
            OSBodyBalanceData bodyBalance,
            OSUpgradeCatalog upgradeCatalog)
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
                var canvas = RequireTransform(gameRoot.transform, "Canvas");
                var session = RequireComponent<OSGameSessionController>(systems, "OSGameSessionController");
                var health = head.GetComponent<OSPlayerHealth>()
                             ?? throw new InvalidOperationException("OSPlayerHealth is missing from Head.");
                var player = head.GetComponent<OSPlayerController>()
                             ?? throw new InvalidOperationException("OSPlayerController is missing from Head.");
                var headWeapon = head.GetComponent<OSHeadWeapon>()
                                 ?? throw new InvalidOperationException("OSHeadWeapon is missing from Head.");
                var growth = systems.GetComponentInChildren<OSBodyGrowthController>(true)
                             ?? throw new InvalidOperationException("OSBodyGrowthController is missing.");
                var pickupSpawner = systems.GetComponentInChildren<OSPickupSpawner>(true)
                                    ?? throw new InvalidOperationException("OSPickupSpawner is missing.");
                var bodyDash = systems.GetComponentInChildren<OSBodyDashController>(true)
                               ?? throw new InvalidOperationException("OSBodyDashController is missing.");
                var attack = systems.GetComponentInChildren<OSAttackBodyRole>(true)
                             ?? throw new InvalidOperationException("OSAttackBodyRole is missing.");
                var laser = systems.GetComponentInChildren<OSLaserBodyRole>(true)
                            ?? throw new InvalidOperationException("OSLaserBodyRole is missing.");
                var control = systems.GetComponentInChildren<OSControlBodyRole>(true)
                              ?? throw new InvalidOperationException("OSControlBodyRole is missing.");
                var shield = systems.GetComponentInChildren<OSShieldBodyRole>(true)
                             ?? throw new InvalidOperationException("OSShieldBodyRole is missing.");

                var levelRoot = GetOrCreateChild(systems, "OSLevelUpSystem");
                var levelController = GetOrAdd<OSLevelUpController>(levelRoot.gameObject);
                Assign(levelController, "sessionController", session);
                Assign(levelController, "upgradeCatalog", upgradeCatalog);
                Assign(levelController, "playerBalance", playerBalance);
                Assign(levelController, "bodyBalance", bodyBalance);
                Assign(levelController, "playerHealth", health);
                Assign(levelController, "playerController", player);
                Assign(levelController, "headWeapon", headWeapon);
                Assign(levelController, "bodyGrowth", growth);
                Assign(levelController, "pickupSpawner", pickupSpawner);
                Assign(levelController, "bodyDashController", bodyDash);
                Assign(levelController, "attackBodyRole", attack);
                Assign(levelController, "laserBodyRole", laser);
                Assign(levelController, "controlBodyRole", control);
                Assign(levelController, "shieldBodyRole", shield);
                AssignInt(levelController, "runSeed", 12012);

                Assign(pickupSpawner, "levelUpController", levelController);
                Assign(pickupSpawner, "playerHealth", health);

                ConfigureLevelUpPanel(canvas, session, levelController);
                ConfigureHud(canvas, levelController);

                var foundationLabel = RequireComponent<TMP_Text>(canvas, "FoundationLabel");
                foundationLabel.text = "STEP 12  |  XP · LEVEL UP · 15 UPGRADES ONLINE";
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

        private static void ConfigureLevelUpPanel(
            Transform canvas,
            OSGameSessionController session,
            OSLevelUpController levelController)
        {
            var panelTransform = RequireTransform(canvas, "LevelUpPanel");
            var panelRect = (RectTransform)panelTransform;
            panelRect.sizeDelta = new Vector2(1160f, 540f);
            ClearChildren(panelTransform);

            var font = RequireComponent<TMP_Text>(canvas, "FoundationLabel").font;
            var title = CreateText(
                panelTransform,
                "Title",
                "LEVEL UP  |  CHOOSE ONE UPGRADE",
                font,
                30f,
                TextAlignmentOptions.Center);
            SetRect(title.rectTransform, new Vector2(0f, 212f), new Vector2(1080f, 90f));

            var colors = new[]
            {
                new Color32(47, 105, 164, 255),
                new Color32(102, 70, 170, 255),
                new Color32(36, 137, 112, 255)
            };
            var buttons = new Button[OSUpgradeRunState.CandidateCount];
            var labels = new TMP_Text[OSUpgradeRunState.CandidateCount];
            for (var index = 0; index < buttons.Length; index++)
            {
                var card = new GameObject(
                    $"UpgradeCard_{index}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                card.transform.SetParent(panelTransform, false);
                SetRect(
                    (RectTransform)card.transform,
                    new Vector2(-330f + (index * 330f), -36f),
                    new Vector2(300f, 354f));
                var image = card.GetComponent<Image>();
                image.color = colors[index];
                var button = card.GetComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.ColorTint;
                buttons[index] = button;

                var label = CreateText(
                    card.transform,
                    "Label",
                    "UPGRADE",
                    font,
                    20f,
                    TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.Normal;
                SetRect(label.rectTransform, Vector2.zero, new Vector2(274f, 330f));
                labels[index] = label;
            }

            var panel = GetOrAdd<OSLevelUpPanel>(panelTransform.gameObject);
            UnityEventTools.AddPersistentListener(buttons[0].onClick, panel.SelectCandidate0);
            UnityEventTools.AddPersistentListener(buttons[1].onClick, panel.SelectCandidate1);
            UnityEventTools.AddPersistentListener(buttons[2].onClick, panel.SelectCandidate2);

            for (var index = 0; index < buttons.Length; index++)
            {
                buttons[index].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = buttons[(index + buttons.Length - 1) % buttons.Length],
                    selectOnRight = buttons[(index + 1) % buttons.Length]
                };
                EditorUtility.SetDirty(buttons[index]);
            }

            Assign(panel, "sessionController", session);
            Assign(panel, "levelUpController", levelController);
            Assign(panel, "titleLabel", title);
            AssignArray(panel, "candidateButtons", buttons);
            AssignArray(panel, "candidateLabels", labels);
        }

        private static void ConfigureHud(Transform canvas, OSLevelUpController levelController)
        {
            var combatHud = RequireTransform(canvas, "CombatHUD");
            var font = RequireComponent<TMP_Text>(canvas, "FoundationLabel").font;
            var progress = CreateOrUpdateHudText(
                combatHud,
                "LevelProgressLabel",
                "LEVEL 1  |  XP 0.0/15  |  UPGRADES 0",
                font,
                new Vector2(0f, -116f),
                17f,
                new Color32(255, 226, 120, 255));
            var feedback = CreateOrUpdateHudText(
                combatHud,
                "UpgradeFeedbackLabel",
                string.Empty,
                font,
                new Vector2(0f, -145f),
                16f,
                new Color32(142, 255, 191, 255));
            var result = RequireComponent<TMP_Text>(canvas, "ResultPanel/Label");
            var presenter = GetOrAdd<OSLevelProgressPresenter>(progress.gameObject);
            presenter.Configure(levelController, progress, feedback, result);
            EditorUtility.SetDirty(presenter);
        }

        private static TMP_Text CreateOrUpdateHudText(
            Transform parent,
            string name,
            string value,
            TMP_FontAsset font,
            Vector2 position,
            float fontSize,
            Color color)
        {
            var target = parent.Find(name);
            TMP_Text label;
            if (target == null)
            {
                label = CreateText(parent, name, value, font, fontSize, TextAlignmentOptions.Center);
            }
            else
            {
                label = target.GetComponent<TMP_Text>()
                        ?? throw new InvalidOperationException($"TMP_Text is missing from '{name}'.");
            }

            label.text = value;
            label.font = font;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            SetRect(label.rectTransform, position, new Vector2(700f, 28f));
            EditorUtility.SetDirty(label);
            return label;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string value,
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
            label.text = value;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = new Color32(235, 245, 255, 255);
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

        private static void ClearChildren(Transform parent)
        {
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
            }
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

        private static void AssignInt(UnityEngine.Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                           ?? throw new InvalidOperationException(
                               $"Serialized property '{propertyName}' is missing from '{target.name}'.");
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignArray<T>(UnityEngine.Object target, string propertyName, T[] values)
            where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                           ?? throw new InvalidOperationException(
                               $"Serialized property '{propertyName}' is missing from '{target.name}'.");
            property.arraySize = values?.Length ?? 0;
            for (var index = 0; index < property.arraySize; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
