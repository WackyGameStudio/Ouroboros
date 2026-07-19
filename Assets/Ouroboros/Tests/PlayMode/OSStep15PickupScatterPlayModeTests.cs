using System.Collections;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep15PickupScatterPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameScene_DifferentPickupTypesAtOneDeathPositionAreSeparated()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var spawner = Object.FindAnyObjectByType<OSPickupSpawner>();
            Assert.That(spawner, Is.Not.Null);
            Assert.That(spawner.SpawnSeparation, Is.EqualTo(0.55f).Within(0.0001f));

            var deathPosition = new Vector2(10f, 10f);
            var experience = spawner.Spawn(OSPickupType.Experience, 1, deathPosition);
            var fragment = spawner.Spawn(OSPickupType.BodyFragment, 1, deathPosition);
            var heal = spawner.Spawn(OSPickupType.Heal, 1, deathPosition);

            Assert.That(experience.IsAccepted, Is.True, experience.ReasonKey);
            Assert.That(fragment.IsAccepted, Is.True, fragment.ReasonKey);
            Assert.That(heal.IsAccepted, Is.True, heal.ReasonKey);
            AssertSeparated(experience.Payload, fragment.Payload, spawner.SpawnSeparation);
            AssertSeparated(experience.Payload, heal.Payload, spawner.SpawnSeparation);
            AssertSeparated(fragment.Payload, heal.Payload, spawner.SpawnSeparation);
        }

        private static void AssertSeparated(OSPickup first, OSPickup second, float minimumDistance)
        {
            Assert.That(Vector2.Distance(first.Position, second.Position),
                Is.GreaterThanOrEqualTo(minimumDistance - 0.0001f));
        }
    }
}
