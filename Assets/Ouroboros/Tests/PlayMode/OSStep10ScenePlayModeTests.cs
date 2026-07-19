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
    public sealed class OSStep10ScenePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameScene_BodyDashMovesHeadConvergesBodyAndPreservesChain()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var player = Object.FindAnyObjectByType<OSPlayerController>();
            var controller = Object.FindAnyObjectByType<OSBodyDashController>();
            var presenter = Object.FindAnyObjectByType<OSBodyDashPresenter>(FindObjectsInactive.Include);

            Assert.That(session, Is.Not.Null);
            Assert.That(growth, Is.Not.Null);
            Assert.That(chain, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);

            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(chain.SetDebugSegmentCount(4).IsAccepted, Is.True);
            var startPosition = player.Position;
            var roles = new OSBodyRoleType[4];
            for (var index = 0; index < roles.Length; index++)
            {
                roles[index] = chain.GetActiveSegment(index).Role;
            }

            Assert.That(controller.RequestBodyDash().Payload, Is.EqualTo(4));
            Assert.That(session.State, Is.EqualTo(OSSessionState.BodyDash));
            Assert.That(chain.IsBodyConvergenceActive, Is.True);
            for (var step = 0; step < 25; step++)
            {
                player.SimulateBodyDashStep(0.02f);
                chain.SimulateBodyConvergenceForTesting(0.02f);
            }

            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(Vector2.Distance(startPosition, player.Position), Is.EqualTo(4.5f).Within(0.05f));
            Assert.That(chain.ActiveCount, Is.EqualTo(4));
            for (var index = 0; index < roles.Length; index++)
            {
                Assert.That(chain.GetActiveSegment(index).Role, Is.EqualTo(roles[index]));
            }

            Assert.That(GameObject.Find("OSExplosionTelegraphView"), Is.Null);
            Assert.That(GameObject.Find("Canvas")?.transform.Find("FoundationLabel"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator GameScene_G0AccumulateCutDashRegrowDieAndRestartResetsRunState()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var health = Object.FindAnyObjectByType<OSPlayerHealth>();
            var player = Object.FindAnyObjectByType<OSPlayerController>();
            var controller = Object.FindAnyObjectByType<OSBodyDashController>();

            Assert.That(session, Is.Not.Null);
            Assert.That(growth, Is.Not.Null);
            Assert.That(chain, Is.Not.Null);
            Assert.That(health, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);

            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(chain.ActiveCount, Is.EqualTo(2));

            Assert.That(growth.AddFragments(12).Payload, Is.EqualTo(2));
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Laser).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Control).IsAccepted, Is.True);
            Assert.That(chain.ActiveCount, Is.EqualTo(4));

            Assert.That(chain.TryCutFrom(3, Vector2.zero).Payload, Is.EqualTo(1));
            Assert.That(growth.AddFragments(6).Payload, Is.EqualTo(1));
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(chain.ActiveCount, Is.EqualTo(4));

            Assert.That(controller.RequestBodyDash().Payload, Is.EqualTo(4));
            for (var step = 0; step < 25; step++)
            {
                player.SimulateBodyDashStep(0.02f);
            }

            Assert.That(chain.ActiveCount, Is.EqualTo(4));
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));

            Assert.That(growth.AddFragments(6).Payload, Is.EqualTo(1));
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(chain.ActiveCount, Is.EqualTo(5));

            var lethalDamage = new OSDamageEvent(
                99,
                0,
                9001,
                1,
                OSTargetKind.PlayerHead,
                health.CurrentHealth,
                Vector2.zero);
            Assert.That(health.TryApplyHeadDamage(lethalDamage).IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Dead));
            Assert.That(session.ConfirmResult().IsAccepted, Is.True);
            Assert.That(session.RestartSession().IsAccepted, Is.True);

            Assert.That(session.State, Is.EqualTo(OSSessionState.StartBodySelection));
            Assert.That(session.ActiveSelection?.Kind, Is.EqualTo(OSSelectionKind.StartBody));
            Assert.That(session.PendingSelectionCount, Is.EqualTo(1));
            Assert.That(chain.ActiveCount, Is.Zero);
            Assert.That(growth.FragmentProgress, Is.Zero);
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
            Assert.That(controller.IsDashActive, Is.False);
            Assert.That(chain.IsBodyConvergenceActive, Is.False);
        }
    }
}
