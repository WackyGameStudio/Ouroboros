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
            CollectionAssert.AllItemsAreUnique(new[]
            {
                experience.Payload.VisualSprite.name,
                fragment.Payload.VisualSprite.name,
                heal.Payload.VisualSprite.name
            });
            CollectionAssert.AllItemsAreUnique(new[]
            {
                experience.Payload.VisualColor,
                fragment.Payload.VisualColor,
                heal.Payload.VisualColor
            });
            Assert.That(experience.Payload.VisualSprite.name, Does.StartWith("Projectile"));
            Assert.That(fragment.Payload.VisualSprite.name, Does.StartWith("Body_Control"));
            Assert.That(heal.Payload.VisualSprite.name, Does.StartWith("Pickup"));
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
            var activeBodyScale = chain.GetActiveSegment(0).View.transform.localScale.x;

            var cut = chain.TryCutFrom(0, chain.GetActiveSegment(0).View.transform.position);
            Assert.That(cut.IsAccepted, Is.True, cut.ReasonKey);
            Assert.That(cut.Payload, Is.EqualTo(4));
            Assert.That(chain.ActiveCount, Is.Zero);
            Assert.That(recovery.LastCutDropCount, Is.EqualTo(4));
            Assert.That(recovery.LastCutSpawnedCount, Is.EqualTo(4));
            Assert.That(recovery.PendingDropCount, Is.Zero);

            var drops = Object.FindObjectsByType<OSPickup>(FindObjectsInactive.Exclude)
                .Where(pickup => pickup.IsRented && pickup.PickupType == OSPickupType.SeveredBody)
                .ToArray();
            Assert.That(drops, Has.Length.EqualTo(4));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    OSBodyRoleType.Shield,
                    OSBodyRoleType.Attack,
                    OSBodyRoleType.Laser,
                    OSBodyRoleType.Control
                },
                drops.Select(pickup => pickup.BodyRole));
            Assert.That(
                drops.Single(pickup => pickup.BodyRole == OSBodyRoleType.Shield).VisualSprite.name,
                Does.StartWith("Body_Shield"));
            Assert.That(
                drops.Single(pickup => pickup.BodyRole == OSBodyRoleType.Attack).VisualSprite.name,
                Does.StartWith("Body_Attack"));
            Assert.That(
                drops.Single(pickup => pickup.BodyRole == OSBodyRoleType.Laser).VisualSprite.name,
                Does.StartWith("Body_Laser"));
            Assert.That(
                drops.Single(pickup => pickup.BodyRole == OSBodyRoleType.Control).VisualSprite.name,
                Does.StartWith("Body_Control"));
            Assert.That(
                drops[0].transform.localScale.x,
                Is.LessThan(activeBodyScale));

            var laserDrop = drops.Single(pickup => pickup.BodyRole == OSBodyRoleType.Laser);
            var collect = laserDrop.TryCollect(collector);
            Assert.That(collect.IsAccepted, Is.True, collect.ReasonKey);
            Assert.That(collect.Payload, Is.EqualTo(1));
            Assert.That(laserDrop.IsRented, Is.False);
            Assert.That(laserDrop.VisualSprite.name, Does.StartWith("Pickup"));
            Assert.That(chain.ActiveCount, Is.EqualTo(1));
            Assert.That(chain.GetActiveSegment(0).Role, Is.EqualTo(OSBodyRoleType.Laser));
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(session.PendingBodySelectionCount, Is.Zero);
            Assert.That(spawner.ActiveCount, Is.EqualTo(3));
        }

        private static void AssertSeparated(OSPickup first, OSPickup second, float minimumDistance)
        {
            Assert.That(Vector2.Distance(first.Position, second.Position),
                Is.GreaterThanOrEqualTo(minimumDistance - 0.0001f));
        }
    }
}
