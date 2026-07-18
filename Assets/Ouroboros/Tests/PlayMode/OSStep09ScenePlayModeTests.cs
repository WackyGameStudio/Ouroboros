using System.Collections;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using Ouroboros.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep09ScenePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameScene_EnemyContactDamagesHeadCutsBodyAndRestartRestoresHealth()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var health = Object.FindAnyObjectByType<OSPlayerHealth>();
            var resolver = Object.FindAnyObjectByType<OSPlayerCombatResolver>();
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var registry = Object.FindAnyObjectByType<OSEnemyRegistry>();
            var hud = GameObject.Find("Canvas")?.transform.Find("CombatHUD/PlayerHealthHUD");
            var presenter = hud != null ? hud.GetComponent<OSPlayerHealthPresenter>() : null;

            Assert.That(session, Is.Not.Null);
            Assert.That(health, Is.Not.Null);
            Assert.That(resolver, Is.Not.Null);
            Assert.That(growth, Is.Not.Null);
            Assert.That(chain, Is.Not.Null);
            Assert.That(registry, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));

            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(chain.ActiveCount, Is.EqualTo(2));
            Assert.That(registry.Count, Is.EqualTo(12));

            var enemy = registry.GetAt(0);
            var head = GameObject.Find("Head").transform;
            var body = chain.GetActiveSegment(1);
            var bodyIdentity = body.View.BodyHurtbox.GetComponent<OSCombatTargetIdentity>();
            Assert.That(enemy, Is.Not.Null);
            Assert.That(bodyIdentity, Is.Not.Null);
            Assert.That(bodyIdentity.RuntimeId, Is.EqualTo(body.StableId));
            Assert.That(bodyIdentity.TargetKind, Is.EqualTo(OSTargetKind.PlayerBody));

            enemy.BeginContact(1, OSTargetKind.PlayerHead, head);
            enemy.BeginContact(body.StableId, OSTargetKind.PlayerBody, body.View.transform);
            enemy.SimulateStep(0.01f);
            resolver.ProcessPendingForTesting();
            Assert.That(health.CurrentHealth, Is.EqualTo(92f));
            Assert.That(chain.ActiveCount, Is.EqualTo(2), "same attack head+body must keep the body");

            enemy.EndContact();
            enemy.BeginContact(body.StableId, OSTargetKind.PlayerBody, body.View.transform);
            enemy.SimulateStep(1f);
            resolver.ProcessPendingForTesting();
            Assert.That(health.CurrentHealth, Is.EqualTo(92f));
            Assert.That(chain.ActiveCount, Is.EqualTo(1));

            health.SimulateTimeForTesting(0.6f);
            Assert.That(resolver.EnqueueDamage(new OSDamageEvent(
                999,
                0,
                enemy.RuntimeId,
                1,
                OSTargetKind.PlayerHead,
                999f,
                head.position)).IsAccepted, Is.True);
            resolver.ProcessPendingForTesting();
            Assert.That(session.State, Is.EqualTo(OSSessionState.Dead));
            Assert.That(chain.ActiveCount, Is.EqualTo(1), "death tick must not perform another cut");

            Assert.That(session.ConfirmResult().IsAccepted, Is.True);
            Assert.That(session.RestartSession().IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.StartBodySelection));
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(health.IsInvulnerable, Is.False);
        }
    }
}
