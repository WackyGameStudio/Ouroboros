using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ouroboros.Runtime
{
    public readonly struct OSProjectValidationResult
    {
        public OSProjectValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }

        public bool IsValid { get; }

        public string Message { get; }
    }

    public static class OSProjectSettingsValidator
    {
        public const string MainMenuScene = "10_MainMenu";
        public const string GameScene = "20_Game";

        private static readonly string[] RequiredPlayerActions = { "Move", "Explosion" };
        private static readonly string[] RequiredUIActions = { "Point", "Click", "Navigate", "Submit", "Cancel" };

        public static OSProjectValidationResult Validate(InputActionAsset inputActions)
        {
            return Validate(inputActions, Application.CanStreamedLevelBeLoaded);
        }

        public static OSProjectValidationResult Validate(
            InputActionAsset inputActions,
            Func<string, bool> canLoadScene)
        {
            var errors = new List<string>(8);

            if (Application.unityVersion != OSBuildInfo.ExpectedUnityVersion)
            {
                errors.Add($"Unity version mismatch. Expected {OSBuildInfo.ExpectedUnityVersion}, actual {Application.unityVersion}.");
            }

            ValidateScene(MainMenuScene, canLoadScene, errors);
            ValidateScene(GameScene, canLoadScene, errors);
            ValidateInputActions(inputActions, errors);

            if (errors.Count == 0)
            {
                return new OSProjectValidationResult(true, "Project foundation validation passed.");
            }

            var builder = new StringBuilder("Project foundation validation failed:");
            for (var i = 0; i < errors.Count; i++)
            {
                builder.Append("\n- ").Append(errors[i]);
            }

            return new OSProjectValidationResult(false, builder.ToString());
        }

        private static void ValidateScene(
            string sceneName,
            Func<string, bool> canLoadScene,
            List<string> errors)
        {
            if (canLoadScene == null || !canLoadScene(sceneName))
            {
                errors.Add($"Required build scene '{sceneName}' is missing or disabled.");
            }
        }

        private static void ValidateInputActions(InputActionAsset inputActions, List<string> errors)
        {
            if (inputActions == null)
            {
                errors.Add("OSInputActions asset is not assigned.");
                return;
            }

            ValidateActionMap(inputActions, "Player", RequiredPlayerActions, errors);
            ValidateActionMap(inputActions, "UI", RequiredUIActions, errors);
        }

        private static void ValidateActionMap(
            InputActionAsset inputActions,
            string mapName,
            IReadOnlyList<string> requiredActions,
            List<string> errors)
        {
            var map = inputActions.FindActionMap(mapName, false);
            if (map == null)
            {
                errors.Add($"Required input map '{mapName}' is missing.");
                return;
            }

            for (var i = 0; i < requiredActions.Count; i++)
            {
                if (map.FindAction(requiredActions[i], false) == null)
                {
                    errors.Add($"Required input action '{mapName}/{requiredActions[i]}' is missing.");
                }
            }
        }
    }
}
