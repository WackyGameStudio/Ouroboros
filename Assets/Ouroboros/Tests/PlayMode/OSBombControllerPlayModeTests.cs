using System.Collections.Generic;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSBombControllerPlayModeTests
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
        public void Request_RequiresTenBodiesAndConsumesFlooredTenPercent()
        {
            var insufficient = CreateRig(9);
            var rejected = insufficient.Bomb.RequestBomb();

            Assert.That(rejected.Code, Is.EqualTo(OSResultCode.RejectedRequirement));
            Assert.That(insufficient.Chain.ActiveCount, Is.EqualTo(9));
            Assert.That(insufficient.Bomb.CooldownRemaining, Is.Zero);
            Assert.That(insufficient.Health.IsAbilityInvulnerable, Is.False);

            var exact = CreateRig(10);
            var accepted = exact.Bomb.RequestBomb();

            Assert.That(accepted.IsAccepted, Is.True);
            Assert.That(accepted.Payload, Is.EqualTo(1));
            Assert.That(exact.Chain.ActiveCount, Is.EqualTo(9));
            Assert.That(exact.Session.State, Is.EqualTo(OSSessionState.Bomb));
            Assert.That(exact.Health.IsAbilityInvulnerable, Is.True);
        }

        [Test]
        public void TurnDirection_IsOppositeTheMajoritySideOfTheBody()
        {
            var rig = CreateRig(10);
            for (var index = 0; index < rig.Chain.ActiveCount; index++)
            {
                rig.Chain.GetActiveSegment(index).View.transform.position =
                    new Vector2(-0.5f - index, index < 7 ? 1f : -1f);
            }

            var snapshot = default(OSBombSnapshot);
            rig.Bomb.BombStarted += value => snapshot = value;

            Assert.That(rig.Bomb.RequestBomb().IsAccepted, Is.True);
            Assert.That(snapshot.TurnSide, Is.EqualTo(OSBombTurnSide.Right));
        }

        [Test]
        public void FullCycle_DrawsCircleThenGathersAlongRecordedPath()
        {
            var rig = CreateRig(10);
            var snapshot = default(OSBombSnapshot);
            rig.Bomb.BombStarted += value => snapshot = value;

            Assert.That(rig.Bomb.RequestBomb().IsAccepted, Is.True);
            SimulateDraw(rig);

            Assert.That(rig.Bomb.Phase, Is.EqualTo(OSBombPhase.Gathering));
            Assert.That(Vector2.Distance(rig.Player.Position, snapshot.Start), Is.LessThan(0.002f));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(9));

            rig.Chain.SimulateBombPathConvergenceForTesting(0.25f);
            rig.Bomb.SimulateForTesting(0.25f);
            var middle = rig.Chain.GetActiveSegment(4).View.transform.position;
            Assert.That(
                Vector2.Distance(middle, snapshot.Center),
                Is.EqualTo(snapshot.Radius).Within(0.08f),
                "The body must converge by advancing along the recorded circle, not by a straight chord.");

            rig.Chain.SimulateBombPathConvergenceForTesting(0.25f);
            rig.Bomb.SimulateForTesting(0.25f);

            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(rig.Bomb.Phase, Is.EqualTo(OSBombPhase.Inactive));
            Assert.That(rig.Health.IsAbilityInvulnerable, Is.False);
            Assert.That(rig.Chain.IsNaturalUnfoldActive, Is.True);
            for (var index = 0; index < rig.Chain.ActiveCount; index++)
            {
                Assert.That(
                    Vector2.Distance(
                        rig.Chain.GetActiveSegment(index).View.transform.position,
                        rig.Player.Position),
                    Is.LessThan(0.01f));
            }
        }

        [Test]
        public void AbilityInvulnerability_RejectsHeadDamageAndBodyCut()
        {
            var rig = CreateRig(10);
            var resolverObject = new GameObject("CombatResolver", typeof(OSPlayerCombatResolver));
            resolverObject.transform.SetParent(rig.Root.transform, false);
            var resolver = resolverObject.GetComponent<OSPlayerCombatResolver>();
            resolver.ConfigureForTesting(rig.Session, rig.Health, rig.Chain);

            Assert.That(rig.Bomb.RequestBomb().IsAccepted, Is.True);
            var headHealth = rig.Health.CurrentHealth;
            var bodyCount = rig.Chain.ActiveCount;
            var targetStableId = rig.Chain.GetActiveSegment(3).StableId;
            Assert.That(resolver.EnqueueDamage(new OSDamageEvent(
                1,
                resolver.CombatTick,
                100,
                1,
                OSTargetKind.PlayerHead,
                25f,
                rig.Player.Position)).IsAccepted, Is.True);
            Assert.That(resolver.EnqueueDamage(new OSDamageEvent(
                2,
                resolver.CombatTick,
                101,
                targetStableId,
                OSTargetKind.PlayerBody,
                1f,
                rig.Chain.GetActiveSegment(3).View.transform.position)).IsAccepted, Is.True);

            resolver.ProcessPendingForTesting();

            Assert.That(rig.Health.CurrentHealth, Is.EqualTo(headHealth));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(bodyCount));
        }

        [Test]
        public void DisablingDuringBomb_CancelsOrbitAndInvulnerabilityWithoutLeavingBombState()
        {
            var rig = CreateRig(10);
            Assert.That(rig.Bomb.RequestBomb().IsAccepted, Is.True);

            rig.Bomb.enabled = false;

            Assert.That(rig.Player.IsBombOrbitActive, Is.False);
            Assert.That(rig.Health.IsAbilityInvulnerable, Is.False);
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Combat));
        }

        [Test]
        public void BlockedCircle_RejectsBeforeCostCooldownOrInvulnerability()
        {
            var rig = CreateRig(10);
            var blocker = new GameObject("BombBlocker", typeof(BoxCollider2D));
            blocker.transform.SetParent(rig.Root.transform, false);
            blocker.layer = 8;
            blocker.transform.position = new Vector2(1.4f, 0f);
            blocker.GetComponent<BoxCollider2D>().size = new Vector2(0.3f, 1.2f);
            Physics2D.SyncTransforms();

            var result = rig.Bomb.RequestBomb();

            Assert.That(result.Code, Is.EqualTo(OSResultCode.RejectedRange));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(10));
            Assert.That(rig.Bomb.CooldownRemaining, Is.Zero);
            Assert.That(rig.Health.IsAbilityInvulnerable, Is.False);
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Combat));
        }

        [Test]
        public void Upgrades_IncreaseFixedDamageAndReduceTenSecondCooldown()
        {
            var rig = CreateRig(10);
            rig.Bomb.ApplyUpgradeModifiers(new OSUpgradeModifiers(
                1f,
                0f,
                0,
                1f,
                0f,
                1f,
                1f,
                1f,
                0f,
                1f,
                1f,
                1f,
                1f,
                1f,
                false,
                1.6f,
                -3f));

            var snapshot = default(OSBombSnapshot);
            rig.Bomb.BombStarted += value => snapshot = value;
            Assert.That(rig.Bomb.RequestBomb().IsAccepted, Is.True);

            Assert.That(snapshot.Damage, Is.EqualTo(160f).Within(0.001f));
            Assert.That(snapshot.Cooldown, Is.EqualTo(7f).Within(0.001f));
            Assert.That(rig.Bomb.CooldownRemaining, Is.EqualTo(7f).Within(0.001f));
        }

        private static void SimulateDraw(BombRig rig)
        {
            for (var step = 0; step < 50; step++)
            {
                rig.Player.SimulateBombOrbitStep(0.02f);
                rig.Chain.SimulatePathStep(rig.Player.Position);
                rig.Bomb.SimulateForTesting(0.02f);
            }
        }

        private BombRig CreateRig(int segmentCount)
        {
            var root = Track(new GameObject("BombTestRoot"));
            root.SetActive(false);
            var actions = Track(CreateActions());
            var playerBalance = Track(ScriptableObject.CreateInstance<OSPlayerBalanceData>());

            var routerObject = new GameObject("InputRouter", typeof(OSInputRouter));
            routerObject.transform.SetParent(root.transform, false);
            var router = routerObject.GetComponent<OSInputRouter>();
            router.Configure(actions);

            var sessionObject = new GameObject("Session", typeof(OSGameSessionController));
            sessionObject.transform.SetParent(root.transform, false);
            var session = sessionObject.GetComponent<OSGameSessionController>();
            session.Configure(router, false);

            var headObject = new GameObject(
                "Head",
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(OSPlayerController));
            headObject.transform.SetParent(root.transform, false);
            headObject.layer = 2;
            var headBody = headObject.GetComponent<Rigidbody2D>();
            headBody.bodyType = RigidbodyType2D.Kinematic;
            headBody.gravityScale = 0f;
            headBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            headObject.GetComponent<CircleCollider2D>().radius = 0.5f;
            var player = headObject.GetComponent<OSPlayerController>();
            player.Configure(
                router,
                session,
                playerBalance,
                1 << 8,
                new Vector2(-20f, -20f),
                new Vector2(20f, 20f));

            var segmentPrefabObject = Track(new GameObject(
                "BodySegmentTemplate",
                typeof(SpriteRenderer),
                typeof(CircleCollider2D),
                typeof(OSBodySegmentView)));
            segmentPrefabObject.SetActive(false);
            var poolRoot = new GameObject("SegmentPool").transform;
            poolRoot.SetParent(root.transform, false);
            var chainObject = new GameObject("BodyChain", typeof(OSBodyChain));
            chainObject.transform.SetParent(root.transform, false);
            var chain = chainObject.GetComponent<OSBodyChain>();
            chain.ConfigureForTesting(
                headObject.transform,
                segmentPrefabObject.GetComponent<OSBodySegmentView>(),
                poolRoot,
                64,
                cutGuardDuration: 0.35f);

            var healthObject = new GameObject("Health", typeof(OSPlayerHealth));
            healthObject.transform.SetParent(root.transform, false);
            var health = healthObject.GetComponent<OSPlayerHealth>();
            health.ConfigureForTesting(session);

            var bombObject = new GameObject("Bomb", typeof(OSBombController));
            bombObject.transform.SetParent(root.transform, false);
            var bomb = bombObject.GetComponent<OSBombController>();
            bomb.ConfigureForTesting(session, player, health, chain);

            root.SetActive(true);
            Assert.That(session.BeginSession().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(chain.SetDebugSegmentCount(segmentCount).IsAccepted, Is.True);
            return new BombRig(root, session, player, health, chain, bomb);
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

        private T Track<T>(T target) where T : Object
        {
            _created.Add(target);
            return target;
        }

        private readonly struct BombRig
        {
            public BombRig(
                GameObject root,
                OSGameSessionController session,
                OSPlayerController player,
                OSPlayerHealth health,
                OSBodyChain chain,
                OSBombController bomb)
            {
                Root = root;
                Session = session;
                Player = player;
                Health = health;
                Chain = chain;
                Bomb = bomb;
            }

            public GameObject Root { get; }
            public OSGameSessionController Session { get; }
            public OSPlayerController Player { get; }
            public OSPlayerHealth Health { get; }
            public OSBodyChain Chain { get; }
            public OSBombController Bomb { get; }
        }
    }
}
