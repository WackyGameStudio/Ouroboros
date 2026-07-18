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
        public IEnumerator GameScene_ExplosionTelegraphConsumesTailGrantsInvulnerabilityAndUpdatesView()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var health = Object.FindAnyObjectByType<OSPlayerHealth>();
            var controller = Object.FindAnyObjectByType<OSExplosionController>();
            var presenter = Object.FindAnyObjectByType<OSExplosionPresenter>(FindObjectsInactive.Include);

            Assert.That(session, Is.Not.Null);
            Assert.That(growth, Is.Not.Null);
            Assert.That(chain, Is.Not.Null);
            Assert.That(health, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);

            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(chain.SetDebugSegmentCount(4).IsAccepted, Is.True);

            Assert.That(controller.RequestExplosion().Payload, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(OSSessionState.ExplosionTelegraph));
            Assert.That(presenter.VisibleCircleCount, Is.EqualTo(2));
            Assert.That(controller.ExpectedRemainingBodyCount, Is.EqualTo(2));

            controller.SimulateTelegraphForTesting(0.25f);

            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(chain.ActiveCount, Is.EqualTo(2));
            Assert.That(health.ExplosionInvulnerabilityRemaining, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(presenter.VisibleCircleCount, Is.Zero);
            Assert.That(GameObject.Find("Canvas")?.transform.Find("FoundationLabel"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator GameScene_G0AccumulateCutDetonateRegrowDieAndRestartResetsRunState()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var health = Object.FindAnyObjectByType<OSPlayerHealth>();
            var controller = Object.FindAnyObjectByType<OSExplosionController>();

            Assert.That(session, Is.Not.Null);
            Assert.That(growth, Is.Not.Null);
            Assert.That(chain, Is.Not.Null);
            Assert.That(health, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);

            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(chain.ActiveCount, Is.EqualTo(2));

            Assert.That(growth.AddFragments(24).Payload, Is.EqualTo(2));
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Laser).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Control).IsAccepted, Is.True);
            Assert.That(chain.ActiveCount, Is.EqualTo(4));

            Assert.That(chain.TryCutFrom(3, Vector2.zero).Payload, Is.EqualTo(1));
            Assert.That(chain.ActiveCount, Is.EqualTo(3));
            Assert.That(growth.AddFragments(12).Payload, Is.EqualTo(1));
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(chain.ActiveCount, Is.EqualTo(4));

            Assert.That(controller.RequestExplosion().Payload, Is.EqualTo(2));
            controller.SimulateTelegraphForTesting(0.25f);
            Assert.That(chain.ActiveCount, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));

            Assert.That(growth.AddFragments(12).Payload, Is.EqualTo(1));
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(chain.ActiveCount, Is.EqualTo(3));

            health.SimulateTimeForTesting(0.4f);
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
            Assert.That(controller.IsTelegraphActive, Is.False);
            Assert.That(controller.ReservedCount, Is.Zero);
        }
    }
}
