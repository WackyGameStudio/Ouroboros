using System;
using Ouroboros.Runtime;
using Ouroboros.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Ouroboros.Editor
{
    public static class OSStep03SessionSetup
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";
        private const string InputActionsPath = "Assets/Ouroboros/Input/OSInputActions.inputactions";

        [MenuItem("Ouroboros/Setup/Apply Step 03 Session Foundation")]
        public static void ApplyStep03SessionFoundation()
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
                var routerObject = RequireTransform(gameRoot.transform, "Systems/OSInputRouter").gameObject;
                var controllerObject = RequireTransform(gameRoot.transform, "Systems/OSGameSessionController").gameObject;
                var canvas = RequireTransform(gameRoot.transform, "Canvas");

                var router = GetOrAdd<OSInputRouter>(routerObject);
                var controller = GetOrAdd<OSGameSessionController>(controllerObject);
                var presenter = GetOrAdd<OSSessionStatePresenter>(canvas.gameObject);
                var uiModule = FindInScene<InputSystemUIInputModule>(scene);
                var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath)
                    ?? throw new InvalidOperationException($"Input actions are missing at '{InputActionsPath}'.");

                Assign(router, "inputActions", inputActions);
                Assign(router, "uiInputModule", uiModule);
                Assign(controller, "inputRouter", router);
                Assign(controller, "autoStartSession", true);

                Assign(presenter, "sessionController", controller);
                Assign(presenter, "timerLabel", RequireComponent<TMP_Text>(canvas, "Timer"));
                Assign(presenter, "stateLabel", RequireComponent<TMP_Text>(canvas, "FoundationLabel"));
                Assign(presenter, "combatHud", RequireTransform(canvas, "CombatHUD").gameObject);
                Assign(
                    presenter,
                    "bodyRoleSelectionPanel",
                    RequireTransform(canvas, "BodyRoleSelectionPanel").gameObject);
                Assign(presenter, "levelUpPanel", RequireTransform(canvas, "LevelUpPanel").gameObject);
                Assign(presenter, "resultPanel", RequireTransform(canvas, "ResultPanel").gameObject);

                SetPanelText(canvas, "BodyRoleSelectionPanel", "BODY ROLE SELECTION\n[ENTER] CONFIRM");
                SetPanelText(canvas, "LevelUpPanel", "LEVEL UP SELECTION\n[ENTER] CONFIRM");
                SetPanelText(canvas, "ResultPanel", "SESSION RESULT\n[ENTER] CONTINUE / RESTART");
                RequireComponent<TMP_Text>(canvas, "FoundationLabel").text = "STEP 03 SESSION FOUNDATION";

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[OUROBOROS][SETUP] Step 03 session foundation applied.");
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

        private static Transform RequireTransform(Transform parent, string path)
        {
            return parent.Find(path)
                   ?? throw new InvalidOperationException($"Transform '{parent.name}/{path}' is missing.");
        }

        private static T RequireComponent<T>(Transform parent, string path) where T : Component
        {
            var target = RequireTransform(parent, path);
            return target.GetComponent<T>()
                   ?? throw new InvalidOperationException($"{typeof(T).Name} is missing from '{target.name}'.");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            return target.GetComponent<T>() ?? target.AddComponent<T>();
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            throw new InvalidOperationException($"{typeof(T).Name} is missing from '{scene.path}'.");
        }

        private static void Assign(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                           ?? throw new InvalidOperationException(
                               $"Serialized property '{propertyName}' is missing on {target.GetType().Name}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void Assign(UnityEngine.Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                           ?? throw new InvalidOperationException(
                               $"Serialized property '{propertyName}' is missing on {target.GetType().Name}.");
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetPanelText(Transform canvas, string panelName, string value)
        {
            var panel = RequireTransform(canvas, panelName);
            var label = panel.GetComponentInChildren<TMP_Text>(true)
                        ?? throw new InvalidOperationException($"TMP label is missing from '{panelName}'.");
            label.text = value;
            EditorUtility.SetDirty(label);
        }
    }
}
