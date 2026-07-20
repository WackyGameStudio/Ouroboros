using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using Ouroboros.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep12ScenePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameScene_LevelSystemCatalogCardsHudAndPickupTypesAreConnected()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var level = Object.FindAnyObjectByType<OSLevelUpController>();
            var panel = Object.FindAnyObjectByType<OSLevelUpPanel>(FindObjectsInactive.Include);
            var presenter = Object.FindAnyObjectByType<OSLevelProgressPresenter>(
                FindObjectsInactive.Include);
            var spawner = Object.FindAnyObjectByType<OSPickupSpawner>();
            var levelPanel = GameObject.Find("Canvas")?.transform.Find("LevelUpPanel");

            Assert.That(level, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(spawner, Is.Not.Null);
            Assert.That(level.GetUpgradeLevel("head_damage"), Is.Zero);
            Assert.That(level.GetUpgradeLevel("elite_priority"), Is.Zero);
            Assert.That(panel.DisplayedCandidateCount, Is.EqualTo(3));
            Assert.That(levelPanel, Is.Not.Null);
            Assert.That(levelPanel.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(3));
            var cardLabels = levelPanel.GetComponentsInChildren<Button>(true)
                .Select(button => button.GetComponentInChildren<TMP_Text>(true))
                .ToArray();
            foreach (var label in cardLabels)
            {
                Assert.That(label, Is.Not.Null);
                Assert.That(label.enableAutoSizing, Is.True);
                Assert.That(label.fontSizeMin, Is.EqualTo(12f).Within(0.001f));
                Assert.That(label.fontSizeMax, Is.EqualTo(18f).Within(0.001f));
                Assert.That(label.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                Assert.That(label.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
            }
            Assert.That(GameObject.Find("Canvas")?.transform.Find("CombatHUD/LevelProgressLabel"),
                Is.Not.Null);
            Assert.That(GameObject.Find("Canvas")?.transform.Find("CombatHUD/UpgradeFeedbackLabel"),
                Is.Not.Null);
            Assert.That(level.transform.name, Is.EqualTo("OSLevelUpSystem"));
            Assert.That(spawner.Capacity, Is.EqualTo(256));
        }
    }
}
