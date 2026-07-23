using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSInputRouterPlayModeTests : InputTestFixture
    {
        private GameObject _host;
        private InputActionAsset _actions;
        private Keyboard _keyboard;
        private OSInputRouter _router;
        private OSGameSessionController _session;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _actions = CreateActions();

            _host = new GameObject("Step03InputTestHost");
            _host.SetActive(false);
            _router = _host.AddComponent<OSInputRouter>();
            _router.Configure(_actions);
            _session = _host.AddComponent<OSGameSessionController>();
            _session.Configure(_router, false);
            _host.SetActive(true);
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }

            if (_actions != null)
            {
                Object.DestroyImmediate(_actions);
            }

            base.TearDown();
        }

        [Test]
        public void RepeatedEnableDisableDoesNotMultiplySubmitCallbacks()
        {
            var callbackCount = 0;
            _router.SubmitRequested += () => callbackCount++;
            _router.SetForState(OSSessionState.StartBodySelection);

            Tap(_keyboard.enterKey);
            Assert.That(callbackCount, Is.EqualTo(1));

            _router.enabled = false;
            _router.enabled = true;
            _router.SetForState(OSSessionState.StartBodySelection);
            Tap(_keyboard.enterKey);

            Assert.That(callbackCount, Is.EqualTo(2));
            Assert.That(_router.IsMapStateValid, Is.True);
        }

        [Test]
        public void HeldSpaceDuringSelectionDoesNotReplayWhenCombatReturns()
        {
            Assert.That(_session.BeginSession().IsAccepted, Is.True);
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_session.State, Is.EqualTo(OSSessionState.Combat));

            var dashCount = 0;
            _session.BodyDashRequested += () => dashCount++;
            Assert.That(_session.QueueSelection(OSSelectionKind.BodyRole).IsAccepted, Is.True);
            Assert.That(_session.ProcessPendingSelection().IsAccepted, Is.True);
            Assert.That(_router.CurrentMode, Is.EqualTo(OSInputMode.UI));

            Press(_keyboard.spaceKey);
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            InputSystem.Update();
            Assert.That(dashCount, Is.Zero);

            Release(_keyboard.spaceKey);
            Press(_keyboard.spaceKey);
            Assert.That(dashCount, Is.EqualTo(1));
            Release(_keyboard.spaceKey);
        }

        [Test]
        public void BKeyRequestsBombOnlyWhilePlayerMapIsActive()
        {
            Assert.That(_session.BeginSession().IsAccepted, Is.True);

            var bombCount = 0;
            _session.BombRequested += () => bombCount++;
            Tap(_keyboard.bKey);
            Assert.That(bombCount, Is.Zero);

            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_session.State, Is.EqualTo(OSSessionState.Combat));

            Tap(_keyboard.bKey);
            Assert.That(bombCount, Is.EqualTo(1));

            Assert.That(_session.QueueSelection(OSSelectionKind.BodyRole).IsAccepted, Is.True);
            Assert.That(_session.ProcessPendingSelection().IsAccepted, Is.True);
            Tap(_keyboard.bKey);
            Assert.That(bombCount, Is.EqualTo(1));
        }

        [Test]
        public void UiSubmitWorksAtTimeScaleZeroAndRestartPreservesRoleSelectionContract()
        {
            Assert.That(_session.BeginSession().IsAccepted, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(_router.CurrentMode, Is.EqualTo(OSInputMode.UI));

            Tap(_keyboard.enterKey);
            Assert.That(_session.State, Is.EqualTo(OSSessionState.StartBodySelection),
                "Generic Submit must not bypass the fixed body-role cards.");
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(_router.CurrentMode, Is.EqualTo(OSInputMode.Player));

            Assert.That(_session.RequestDeath().IsAccepted, Is.True);
            Assert.That(_session.TryRequestBodyDash().Code, Is.EqualTo(OSResultCode.RejectedState));
            Tap(_keyboard.enterKey);
            Assert.That(_session.State, Is.EqualTo(OSSessionState.Result));
            Tap(_keyboard.enterKey);
            Assert.That(_session.State, Is.EqualTo(OSSessionState.StartBodySelection));
            Tap(_keyboard.enterKey);
            Assert.That(_session.State, Is.EqualTo(OSSessionState.StartBodySelection));
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);

            Assert.That(_session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(_router.CurrentMode, Is.EqualTo(OSInputMode.Player));
            Assert.That(_router.IsMapStateValid, Is.True);
        }

        [Test]
        public void SubmitTransitionWaitsUntilAllPerformedCallbacksComplete()
        {
            Assert.That(_session.BeginSession().IsAccepted, Is.True);
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_session.RequestDeath().IsAccepted, Is.True);

            var submit = _actions.FindAction("UI/Submit", true);
            InputDevice observedDevice = null;
            var callbackCount = 0;
            System.Action<InputAction.CallbackContext> laterCallback = context =>
            {
                observedDevice = context.control.device;
                callbackCount++;
            };
            submit.performed += laterCallback;

            try
            {
                Tap(_keyboard.enterKey);
                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(observedDevice, Is.SameAs(_keyboard));
                Assert.That(_session.State, Is.EqualTo(OSSessionState.Result));

                Tap(_keyboard.enterKey);
                Assert.That(callbackCount, Is.EqualTo(2));
                Assert.That(observedDevice, Is.SameAs(_keyboard));
                Assert.That(_session.State, Is.EqualTo(OSSessionState.StartBodySelection));
                Assert.That(_router.IsMapStateValid, Is.True);
            }
            finally
            {
                submit.performed -= laterCallback;
            }
        }

        private void Tap(ButtonControl button)
        {
            Press(button);
            Release(button);
        }

        private static InputActionAsset CreateActions()
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var player = actions.AddActionMap("Player");
            player.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            player.AddAction("BodyDash", InputActionType.Button, "<Keyboard>/space", interactions: "Press");
            player.AddAction("Bomb", InputActionType.Button, "<Keyboard>/b", interactions: "Press");

            var ui = actions.AddActionMap("UI");
            ui.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter", interactions: "Press");
            ui.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape", interactions: "Press");
            return actions;
        }
    }
}
