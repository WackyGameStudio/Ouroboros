using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSFoundationPlayModeTests
    {
        [UnityTest]
        public IEnumerator BootToMainMenuToGameFlowCompletes()
        {
            yield return SceneManager.LoadSceneAsync("00_Boot", LoadSceneMode.Single);
            yield return WaitForScene("10_MainMenu", 300);

            var menuRoot = GameObject.Find("MainMenuRoot");
            Assert.That(menuRoot, Is.Not.Null);
            var menu = menuRoot.GetComponent<OSMainMenuController>();
            Assert.That(menu, Is.Not.Null);

            menu.StartGame();
            yield return WaitForScene("20_Game", 300);
            Assert.That(GameObject.Find("GameRoot"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator InvalidFoundationBlocksGameAndLogsOnce()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            var sceneBeforeValidation = SceneManager.GetActiveScene().name;

            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\[OUROBOROS\]\[BOOT\] Game entry blocked\.[\s\S]*OSInputActions asset is not assigned\."));

            var invalidBoot = new GameObject("InvalidBoot");
            var bootstrap = invalidBoot.AddComponent<OSBootstrap>();

            yield return null;
            yield return null;

            Assert.That(bootstrap.ValidationPassed, Is.False);
            Assert.That(bootstrap.LastValidationMessage, Does.Contain("OSInputActions asset is not assigned"));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneBeforeValidation));

            Object.Destroy(invalidBoot);
        }

        private static IEnumerator WaitForScene(string sceneName, int frameLimit)
        {
            for (var frame = 0; frame < frameLimit; frame++)
            {
                if (SceneManager.GetActiveScene().name == sceneName)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Scene '{sceneName}' did not become active within {frameLimit} frames.");
        }
    }
}
