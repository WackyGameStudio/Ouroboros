using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using Ouroboros.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSSessionFlowPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameSceneRunsSelectionPriorityPauseDeathAndRestartLifecycle()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindFirstObjectByType<OSGameSessionController>();
            var router = Object.FindFirstObjectByType<OSInputRouter>();
            var presenter = Object.FindFirstObjectByType<OSSessionStatePresenter>();
            var canvas = GameObject.Find("Canvas");
            var bodyPanel = canvas?.transform.Find("BodyRoleSelectionPanel")?.gameObject;
            var levelPanel = canvas?.transform.Find("LevelUpPanel")?.gameObject;

            Assert.That(session, Is.Not.Null);
            Assert.That(router, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(bodyPanel, Is.Not.Null);
            Assert.That(levelPanel, Is.Not.Null);
            Assert.That(session.State, Is.EqualTo(OSSessionState.StartBodySelection));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(router.CurrentMode, Is.EqualTo(OSInputMode.UI));
            Assert.That(router.IsMapStateValid, Is.True);
            Assert.That(bodyPanel.activeSelf, Is.True);
            Assert.That(levelPanel.activeSelf, Is.False);

            var pausedSessionTime = session.SessionElapsedTime;
            var uiTime = session.UiElapsedTime;
            yield return null;
            yield return null;
            Assert.That(session.SessionElapsedTime, Is.EqualTo(pausedSessionTime).Within(0.0001f));
            Assert.That(session.UiElapsedTime, Is.GreaterThan(uiTime));

            session.CompleteActiveSelection();
            session.CompleteActiveSelection();
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(router.CurrentMode, Is.EqualTo(OSInputMode.Player));

            session.QueueSelection(OSSelectionKind.LevelUp);
            session.QueueSelection(OSSelectionKind.BodyRole);
            session.QueueSelection(OSSelectionKind.LevelUp);
            session.QueueSelection(OSSelectionKind.BodyRole);
            session.ProcessPendingSelection();

            var processedKinds = new List<OSSelectionKind>();
            while (session.ActiveSelection.HasValue)
            {
                processedKinds.Add(session.ActiveSelection.Value.Kind);
                Assert.That(bodyPanel.activeSelf && levelPanel.activeSelf, Is.False);
                session.CompleteActiveSelection();
            }

            Assert.That(
                processedKinds,
                Is.EqualTo(new[]
                {
                    OSSelectionKind.BodyRole,
                    OSSelectionKind.BodyRole,
                    OSSelectionKind.LevelUp,
                    OSSelectionKind.LevelUp
                }));
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));

            var explosionCount = 0;
            session.ExplosionRequested += () => explosionCount++;
            Assert.That(session.RequestDeath().IsAccepted, Is.True);
            Assert.That(session.TryRequestExplosion().Code, Is.EqualTo(OSResultCode.RejectedState));
            Assert.That(explosionCount, Is.Zero);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(router.CurrentMode, Is.EqualTo(OSInputMode.UI));

            Assert.That(session.ConfirmResult().IsAccepted, Is.True);
            Assert.That(session.RestartSession().IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.StartBodySelection));
            Assert.That(session.SessionElapsedTime, Is.Zero);
            session.CompleteActiveSelection();
            session.CompleteActiveSelection();

            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(router.CurrentMode, Is.EqualTo(OSInputMode.Player));
            Assert.That(router.IsMapStateValid, Is.True);
        }
    }
}
