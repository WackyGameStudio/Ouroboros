using NUnit.Framework;
using Ouroboros.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSFoundationEditModeTests
    {
        private const string InputActionsPath = "Assets/Ouroboros/Input/OSInputActions.inputactions";

        [Test]
        public void ProjectSettingsMatchFoundationPolicy()
        {
            Assert.That(Application.unityVersion, Is.EqualTo(OSBuildInfo.ExpectedUnityVersion));
            Assert.That(PlayerSettings.colorSpace, Is.EqualTo(ColorSpace.Linear));
            Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo(1920));
            Assert.That(PlayerSettings.defaultScreenHeight, Is.EqualTo(1080));
            Assert.That(PlayerSettings.defaultWebScreenWidth, Is.EqualTo(960));
            Assert.That(PlayerSettings.defaultWebScreenHeight, Is.EqualTo(540));
            Assert.That(EditorSettings.serializationMode, Is.EqualTo(SerializationMode.ForceText));
        }

        [Test]
        public void BuildScenesAreOrdered()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes, Has.Length.EqualTo(3));
            Assert.That(scenes[0].path, Is.EqualTo("Assets/Ouroboros/Scenes/00_Boot.unity"));
            Assert.That(scenes[1].path, Is.EqualTo("Assets/Ouroboros/Scenes/10_MainMenu.unity"));
            Assert.That(scenes[2].path, Is.EqualTo("Assets/Ouroboros/Scenes/20_Game.unity"));
            Assert.That(scenes[0].enabled && scenes[1].enabled && scenes[2].enabled, Is.True);
        }

        [Test]
        public void InputAssetPassesBootValidation()
        {
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(inputActions, Is.Not.Null);

            var result = OSProjectSettingsValidator.Validate(inputActions, IsEnabledBuildScene);
            Assert.That(result.IsValid, Is.True, result.Message);
        }

        [Test]
        public void MissingInputAssetIsRejectedWithConcreteReason()
        {
            var result = OSProjectSettingsValidator.Validate(null, IsEnabledBuildScene);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Message, Does.Contain("OSInputActions asset is not assigned"));
        }

        private static bool IsEnabledBuildScene(string sceneName)
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && scene.path.EndsWith($"/{sceneName}.unity"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
