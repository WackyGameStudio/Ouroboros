using System.Collections.Generic;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSBodyDashControllerPlayModeTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (var index = _created.Count - 1; index >= 0; index--)
            {
                if (_created[index] != null)
                {
                    Object.DestroyImmediate(_created[index]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void RequestBodyDash_RequiresNoBodyAndRejectsReentry()
        {
            var rig = CreateRig(0);

            var accepted = rig.Controller.RequestBodyDash();
            var reentry = rig.Controller.RequestBodyDash();

            Assert.That(accepted.IsAccepted, Is.True);
            Assert.That(accepted.Payload, Is.Zero);
            Assert.That(reentry.Code, Is.EqualTo(OSResultCode.RejectedState));
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.BodyDash));
            Assert.That(rig.Player.IsBodyDashActive, Is.True);
        }

        [Test]
        public void Completion_MovesHeadAndPreservesBodyCountAndRoles()
        {
            var rig = CreateRig(4);
            var roles = new OSBodyRoleType[4];
            for (var index = 0; index < roles.Length; index++)
            {
                roles[index] = rig.Chain.GetActiveSegment(index).Role;
            }

            var resolution = default(OSBodyDashResolution);
            rig.Controller.DashCompleted += value => resolution = value;
            Assert.That(rig.Controller.RequestBodyDash().Payload, Is.EqualTo(4));

            for (var step = 0; step < 25; step++)
            {
                rig.Player.SimulateBodyDashStep(0.02f);
                rig.Chain.SimulateBodyConvergenceForTesting(0.02f);
            }

            Assert.That(rig.Player.Position.x, Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(4));
            Assert.That(resolution.TravelledDistance, Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(resolution.ConvergedBodyCount, Is.EqualTo(4));
            for (var index = 0; index < roles.Length; index++)
            {
                Assert.That(rig.Chain.GetActiveSegment(index).Role, Is.EqualTo(roles[index]));
            }
        }

        [Test]
        public void BodyConvergence_PullsSegmentsToHeadThenRecoversPath()
        {
            var rig = CreateRig(4);
            var tail = rig.Chain.GetActiveSegment(3).View.transform;
            var initialDistance = Vector2.Distance(rig.Player.Position, tail.position);
            Assert.That(rig.Controller.RequestBodyDash().IsAccepted, Is.True);

            rig.Player.SimulateBodyDashStep(0.25f);
            rig.Chain.SimulateBodyConvergenceForTesting(0.25f);
            var midpointDistance = Vector2.Distance(rig.Player.Position, tail.position);

            rig.Player.SimulateBodyDashStep(0.25f);
            rig.Chain.SimulateBodyConvergenceForTesting(0.25f);
            var convergedDistance = Vector2.Distance(rig.Player.Position, tail.position);
            rig.Chain.SimulateBodyConvergenceForTesting(0.25f);
            var recoveredDistance = Vector2.Distance(rig.Player.Position, tail.position);

            Assert.That(midpointDistance, Is.LessThan(initialDistance));
            Assert.That(convergedDistance, Is.LessThan(0.01f));
            Assert.That(recoveredDistance, Is.GreaterThan(1.5f));
            Assert.That(rig.Chain.IsBodyConvergenceActive, Is.False);
        }

        [Test]
        public void Dash_StopsAtWorldBlockerWithoutOverlap()
        {
            var rig = CreateRig(2);
            var blocker = new GameObject("DashBlocker");
            blocker.transform.SetParent(rig.Root.transform, false);
            blocker.layer = 8;
            blocker.transform.position = new Vector2(1.5f, 0f);
            blocker.AddComponent<BoxCollider2D>().size = new Vector2(1f, 6f);
            Physics2D.SyncTransforms();

            Assert.That(rig.Controller.RequestBodyDash().IsAccepted, Is.True);
            for (var step = 0; step < 25; step++)
            {
                rig.Player.SimulateBodyDashStep(0.02f);
            }

            Assert.That(rig.Player.Position.x, Is.LessThanOrEqualTo(0.51f));
            Assert.That(Physics2D.OverlapCircle(rig.Player.Position, 0.49f, 1 << 8), Is.Null);
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(2));
        }

        [Test]
        public void Cooldown_BlocksThenAllowsNextDash()
        {
            var rig = CreateRig(2);
            Assert.That(rig.Controller.RequestBodyDash().IsAccepted, Is.True);
            for (var step = 0; step < 25; step++)
            {
                rig.Player.SimulateBodyDashStep(0.02f);
            }

            Assert.That(rig.Controller.RequestBodyDash().Code, Is.EqualTo(OSResultCode.RejectedRequirement));
            rig.Controller.SimulateCooldownForTesting(2f);
            Assert.That(rig.Controller.RequestBodyDash().IsAccepted, Is.True);
        }

        private DashRig CreateRig(int segmentCount)
        {
            var root = Track(new GameObject("BodyDashTestRoot"));
            root.SetActive(false);
            var actions = Track(CreateActions());
            var balance = Track(ScriptableObject.CreateInstance<OSPlayerBalanceData>());

            var routerObject = new GameObject("InputRouter", typeof(OSInputRouter));
            routerObject.transform.SetParent(root.transform, false);
            var router = routerObject.GetComponent<OSInputRouter>();
            router.Configure(actions);
            var sessionObject = new GameObject("Session", typeof(OSGameSessionController));
            sessionObject.transform.SetParent(root.transform, false);
            var session = sessionObject.GetComponent<OSGameSessionController>();
            session.Configure(router, false);

            var headObject = new GameObject("Head", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(OSPlayerController));
            headObject.transform.SetParent(root.transform, false);
            headObject.layer = 2;
            var headBody = headObject.GetComponent<Rigidbody2D>();
            headBody.bodyType = RigidbodyType2D.Kinematic;
            headBody.gravityScale = 0f;
            headBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            headObject.GetComponent<CircleCollider2D>().radius = 0.5f;
            var player = headObject.GetComponent<OSPlayerController>();
            player.Configure(router, session, balance, 1 << 8, new Vector2(-20f, -20f), new Vector2(20f, 20f));

            var prefabObject = Track(new GameObject(
                "BodySegmentTemplate",
                typeof(SpriteRenderer),
                typeof(CircleCollider2D),
                typeof(OSBodySegmentView)));
            prefabObject.SetActive(false);
            var poolRoot = new GameObject("SegmentPool").transform;
            poolRoot.SetParent(root.transform, false);
            var chainObject = new GameObject("BodyChain", typeof(OSBodyChain));
            chainObject.transform.SetParent(root.transform, false);
            var chain = chainObject.GetComponent<OSBodyChain>();
            chain.ConfigureForTesting(
                headObject.transform,
                prefabObject.GetComponent<OSBodySegmentView>(),
                poolRoot,
                64,
                cutGuardDuration: 0.35f);

            var controllerObject = new GameObject("BodyDashController", typeof(OSBodyDashController));
            controllerObject.transform.SetParent(root.transform, false);
            var controller = controllerObject.GetComponent<OSBodyDashController>();
            controller.ConfigureForTesting(session, chain, player);

            root.SetActive(true);
            Assert.That(session.BeginSession().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(chain.SetDebugSegmentCount(segmentCount).IsAccepted, Is.True);
            return new DashRig(root, session, headObject.transform, player, chain, controller);
        }

        private static InputActionAsset CreateActions()
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var player = actions.AddActionMap("Player");
            player.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            player.AddAction("BodyDash", InputActionType.Button, "<Keyboard>/space", interactions: "Press");
            var ui = actions.AddActionMap("UI");
            ui.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter", interactions: "Press");
            ui.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape", interactions: "Press");
            return actions;
        }

        private T Track<T>(T target) where T : Object
        {
            _created.Add(target);
            return target;
        }

        private readonly struct DashRig
        {
            public DashRig(GameObject root, OSGameSessionController session, Transform head,
                OSPlayerController player, OSBodyChain chain, OSBodyDashController controller)
            {
                Root = root;
                Session = session;
                Head = head;
                Player = player;
                Chain = chain;
                Controller = controller;
            }

            public GameObject Root { get; }
            public OSGameSessionController Session { get; }
            public Transform Head { get; }
            public OSPlayerController Player { get; }
            public OSBodyChain Chain { get; }
            public OSBodyDashController Controller { get; }
        }
    }
}
