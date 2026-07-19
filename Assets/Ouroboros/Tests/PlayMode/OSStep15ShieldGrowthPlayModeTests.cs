using System.Collections;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep15ShieldGrowthPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameScene_ShieldBlocksTailContactPointAndGrowthRequiresSixFragments()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var shield = Object.FindAnyObjectByType<OSShieldBodyRole>();
            var resolver = Object.FindAnyObjectByType<OSPlayerCombatResolver>();
            var registry = Object.FindAnyObjectByType<OSEnemyRegistry>();
            var pool = Object.FindAnyObjectByType<OSPoolRegistry>();
            var head = Object.FindAnyObjectByType<OSPlayerController>()?.transform;

            Assert.That(session, Is.Not.Null);
            Assert.That(growth, Is.Not.Null);
            Assert.That(chain, Is.Not.Null);
            Assert.That(shield, Is.Not.Null);
            Assert.That(resolver, Is.Not.Null);
            Assert.That(registry, Is.Not.Null);
            Assert.That(pool, Is.Not.Null);
            Assert.That(head, Is.Not.Null);
            Assert.That(growth.FragmentRequirement, Is.EqualTo(6));

            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Time.timeScale = 0f;

            var shieldSegment = chain.GetActiveSegment(0);
            var target = chain.GetActiveSegment(1);
            var shieldPosition = (Vector2)shieldSegment.View.transform.position;
            target.View.transform.position = shieldPosition + Vector2.right * 1.8f;
            Physics2D.SyncTransforms();

            var rent = pool.Rent(
                "enemy_chaser",
                shieldPosition + Vector2.right * 1.48f,
                Quaternion.identity);
            Assert.That(rent.IsAccepted, Is.True, rent.ReasonKey);
            var enemy = rent.Payload as OSEnemyController;
            Assert.That(enemy, Is.Not.Null);
            enemy.ConfigureForTesting(registry, session, head, speed: 0f, damage: 8f);
            enemy.BeginContact(
                target.StableId,
                OSTargetKind.PlayerBody,
                target.View.transform,
                target.View.BodyHurtbox);

            var contactPoint = target.View.BodyHurtbox.ClosestPoint(enemy.Position);
            Assert.That(Vector2.Distance(shieldPosition, target.View.transform.position),
                Is.GreaterThan(shield.Radius));
            Assert.That(Vector2.Distance(shieldPosition, contactPoint),
                Is.LessThanOrEqualTo(shield.Radius));

            enemy.SimulateStep(0.01f);
            resolver.ProcessPendingForTesting();

            Assert.That(chain.ActiveCount, Is.EqualTo(2));
            Assert.That(shield.ChargedCount, Is.Zero);
        }
    }
}
