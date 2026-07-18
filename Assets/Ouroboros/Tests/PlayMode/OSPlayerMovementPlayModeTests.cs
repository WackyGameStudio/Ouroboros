using System.Collections;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSPlayerMovementPlayModeTests : InputTestFixture
    {
        private GameObject _host;
        private GameObject _playerObject;
        private GameObject _blockerRoot;
        private InputActionAsset _actions;
        private OSPlayerBalanceData _balance;
        private Keyboard _keyboard;
        private OSInputRouter _router;
        private OSGameSessionController _session;
        private OSPlayerController _player;
        private Rigidbody2D _body;

        public override void Setup()
        {
            base.Setup();
            Time.timeScale = 1f;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _actions = CreateActions();
            _balance = ScriptableObject.CreateInstance<OSPlayerBalanceData>();

            _host = new GameObject("Step04MovementTestHost");
            _host.SetActive(false);
            _router = _host.AddComponent<OSInputRouter>();
            _router.Configure(_actions);
            _session = _host.AddComponent<OSGameSessionController>();
            _session.Configure(_router, false);

            _playerObject = new GameObject("Step04TestPlayer");
            _playerObject.layer = 2;
            _playerObject.SetActive(false);
            _body = _playerObject.AddComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.gravityScale = 0f;
            _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            var collider = _playerObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            _player = _playerObject.AddComponent<OSPlayerController>();
            _player.Configure(
                _router,
                _session,
                _balance,
                1 << 0,
                new Vector2(-20f, -20f),
                new Vector2(20f, 20f));

            _blockerRoot = new GameObject("Step04TestBlockers");
            _host.SetActive(true);
            _playerObject.SetActive(true);
            BeginCombat();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(_blockerRoot);
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_balance);
            Object.DestroyImmediate(_actions);
            base.TearDown();
        }

        [Test]
        public void WasdAndArrowBindingsProduceTheSameMoveVector()
        {
            Press(_keyboard.wKey);
            Press(_keyboard.dKey);
            var wasd = _router.MoveValue;
            Release(_keyboard.wKey);
            Release(_keyboard.dKey);

            Press(_keyboard.upArrowKey);
            Press(_keyboard.rightArrowKey);
            var arrows = _router.MoveValue;
            Release(_keyboard.upArrowKey);
            Release(_keyboard.rightArrowKey);

            Assert.That(wasd, Is.EqualTo(arrows).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(wasd.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator ZeroInputStopsAndPreservesLastDirection()
        {
            Press(_keyboard.dKey);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Release(_keyboard.dKey);

            var stoppedPosition = _body.position;
            var lastDirection = _player.LastDirection;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(_body.position, Is.EqualTo(stoppedPosition).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(lastDirection, Is.EqualTo(Vector2.right).Using(Vector2ComparerWithEqualsOperator.Instance));
            Assert.That(_player.LastDirection, Is.EqualTo(lastDirection).Using(Vector2ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CastMovementSlidesAlongObstacleTangentWithoutPenetration()
        {
            CreateBlocker("VerticalWall", new Vector2(1.5f, 0f), new Vector2(1f, 12f));
            Physics2D.SyncTransforms();

            for (var step = 0; step < 50; step++)
            {
                _player.SimulateMovementStep(Vector2.one, 0.02f);
            }

            Assert.That(_body.position.x, Is.LessThanOrEqualTo(0.51f));
            Assert.That(_body.position.y, Is.GreaterThan(2f));
            Assert.That(
                Physics2D.OverlapCircle(_body.position, 0.49f, 1 << 0),
                Is.Null,
                "The solid head collider must not overlap a WorldBlocker.");
        }

        [Test]
        public void CornerContactRemainsStableDuringTwoMinuteEquivalentSimulation()
        {
            CreateBlocker("VerticalCorner", new Vector2(1.5f, 0f), new Vector2(1f, 6f));
            CreateBlocker("HorizontalCorner", new Vector2(0f, 1.5f), new Vector2(6f, 1f));
            Physics2D.SyncTransforms();

            for (var step = 0; step < 6000; step++)
            {
                _player.SimulateMovementStep(Vector2.one, 0.02f);
            }

            var settled = _body.position;
            for (var step = 0; step < 30; step++)
            {
                _player.SimulateMovementStep(Vector2.one, 0.02f);
            }

            Assert.That(Vector2.Distance(settled, _body.position), Is.LessThan(0.001f));
            Assert.That(settled.x, Is.InRange(0.45f, 0.51f));
            Assert.That(settled.y, Is.InRange(0.45f, 0.51f));
            Assert.That(Physics2D.OverlapCircle(_body.position, 0.49f, 1 << 0), Is.Null);
        }

        [Test]
        public void SelectionStateRejectsMovementAndKeepsPosition()
        {
            _player.SimulateMovementStep(Vector2.right, 0.02f);
            var combatPosition = _body.position;
            Assert.That(_session.QueueSelection(OSSelectionKind.BodyRole).IsAccepted, Is.True);
            Assert.That(_session.ProcessPendingSelection().IsAccepted, Is.True);

            for (var step = 0; step < 30; step++)
            {
                _player.SimulateMovementStep(Vector2.right, 0.02f);
            }

            Assert.That(_session.State, Is.EqualTo(OSSessionState.BodyRoleSelection));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(_body.position, Is.EqualTo(combatPosition).Using(Vector2ComparerWithEqualsOperator.Instance));
        }

        private void BeginCombat()
        {
            Assert.That(_session.BeginSession().IsAccepted, Is.True);
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_session.State, Is.EqualTo(OSSessionState.Combat));
        }

        private void CreateBlocker(string name, Vector2 position, Vector2 size)
        {
            var blocker = new GameObject(name);
            blocker.transform.SetParent(_blockerRoot.transform, false);
            blocker.transform.position = position;
            var collider = blocker.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        private static InputActionAsset CreateActions()
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var player = actions.AddActionMap("Player");
            var move = player.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            player.AddAction("Explosion", InputActionType.Button, "<Keyboard>/space", interactions: "Press");

            var ui = actions.AddActionMap("UI");
            ui.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter", interactions: "Press");
            ui.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape", interactions: "Press");
            return actions;
        }
    }
}
