using System.Collections;
using System.Linq;
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

        [UnityTest]
        public IEnumerator CutBody_DropsEveryOriginalRoleAndCollectingOneReattachesItImmediately()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var spawner = Object.FindAnyObjectByType<OSPickupSpawner>();
            var collector = Object.FindAnyObjectByType<OSPickupCollector>();
            var recovery = Object.FindAnyObjectByType<OSSeveredBodyDropController>();
            var attack = Object.FindAnyObjectByType<OSAttackBodyRole>();
            var laser = Object.FindAnyObjectByType<OSLaserBodyRole>();

            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(recovery, Is.Not.Null);
            Assert.That(attack.Damage, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(laser.Length, Is.EqualTo(14f).Within(0.0001f));
            Assert.That(chain.SetDebugSegmentCount(4).IsAccepted, Is.True);

            var cut = chain.TryCutFrom(1, chain.GetActiveSegment(1).View.transform.position);
            Assert.That(cut.IsAccepted, Is.True, cut.ReasonKey);
            Assert.That(cut.Payload, Is.EqualTo(3));
            Assert.That(chain.ActiveCount, Is.EqualTo(1));
            Assert.That(recovery.LastCutDropCount, Is.EqualTo(3));
            Assert.That(recovery.LastCutSpawnedCount, Is.EqualTo(3));
            Assert.That(recovery.PendingDropCount, Is.Zero);

            var drops = Object.FindObjectsByType<OSPickup>(FindObjectsInactive.Exclude)
                .Where(pickup => pickup.IsRented && pickup.PickupType == OSPickupType.SeveredBody)
                .ToArray();
            Assert.That(drops, Has.Length.EqualTo(3));
            CollectionAssert.AreEquivalent(
                new[] { OSBodyRoleType.Attack, OSBodyRoleType.Laser, OSBodyRoleType.Control },
                drops.Select(pickup => pickup.BodyRole));

            var laserDrop = drops.Single(pickup => pickup.BodyRole == OSBodyRoleType.Laser);
            var collect = laserDrop.TryCollect(collector);
            Assert.That(collect.IsAccepted, Is.True, collect.ReasonKey);
            Assert.That(collect.Payload, Is.EqualTo(1));
            Assert.That(laserDrop.IsRented, Is.False);
            Assert.That(chain.ActiveCount, Is.EqualTo(2));
            Assert.That(chain.GetActiveSegment(1).Role, Is.EqualTo(OSBodyRoleType.Laser));
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(session.PendingBodySelectionCount, Is.Zero);
            Assert.That(spawner.ActiveCount, Is.EqualTo(2));
        }

        private static void AssertSeparated(OSPickup first, OSPickup second, float minimumDistance)
        {
            Assert.That(Vector2.Distance(first.Position, second.Position),
                Is.GreaterThanOrEqualTo(minimumDistance - 0.0001f));
        }
    }
}
