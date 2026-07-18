using System.Collections.Generic;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSExplosionControllerPlayModeTests
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
        public void RequestExplosion_ThreeSegmentsRejectsWithoutConsumption()
        {
            var rig = CreateRig(3);

            var result = rig.Controller.RequestExplosion();

            Assert.That(result.Code, Is.EqualTo(OSResultCode.RejectedRequirement));
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(3));
            Assert.That(rig.Controller.IsTelegraphActive, Is.False);
        }

        [Test]
        public void Reservation_UsesTailStableIdsRegardlessOfRoleAndRejectsReentry()
        {
            var rig = CreateRig(0);
            rig.Chain.AppendSegment(OSBodyRoleType.Shield);
            rig.Chain.AppendSegment(OSBodyRoleType.Attack);
            rig.Chain.AppendSegment(OSBodyRoleType.Laser);
            rig.Chain.AppendSegment(OSBodyRoleType.Control);
            rig.Chain.AppendSegment(OSBodyRoleType.Attack);
            var snapshot = default(OSExplosionSnapshot);
            rig.Controller.TelegraphStarted += value => snapshot = value;

            var result = rig.Controller.RequestExplosion();
            var reentry = rig.Controller.RequestExplosion();

            Assert.That(result.Payload, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 4, 5 }, snapshot.ReservedSegmentIds);
            Assert.That(reentry.Code, Is.EqualTo(OSResultCode.RejectedState));
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.ExplosionTelegraph));
            Assert.That(rig.Session.IsSimulationRunning, Is.True);
            Assert.That(rig.Session.IsPlayerInputAllowed, Is.True);
        }

        [Test]
        public void OverlappingCircles_DamageRegisteredEnemyExactlyOnceThenConsumeTail()
        {
            var rig = CreateRig(4);
            var snapshot = default(OSExplosionSnapshot);
            rig.Controller.TelegraphStarted += value => snapshot = value;
            Assert.That(rig.Controller.RequestExplosion().IsAccepted, Is.True);
            var enemy = RentEnemy(rig, snapshot.Centers[0], 200f);

            rig.Controller.SimulateTelegraphForTesting(0.25f);

            Assert.That(enemy.CurrentHealth, Is.EqualTo(130f));
            Assert.That(rig.Controller.LastHitCount, Is.EqualTo(1));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(2));
            Assert.That(rig.Health.ExplosionInvulnerabilityRemaining, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Combat));
        }

        [Test]
        public void TelegraphCut_RemovesReservationAndConsumesOnlyRemainingReservedTail()
        {
            var rig = CreateRig(10);
            var resolution = default(OSExplosionResolution);
            rig.Controller.ExplosionResolved += value => resolution = value;
            Assert.That(rig.Controller.RequestExplosion().Payload, Is.EqualTo(3));

            Assert.That(rig.Chain.TryCutFrom(9, Vector2.zero).Payload, Is.EqualTo(1));
            Assert.That(rig.Controller.ReservedCount, Is.EqualTo(2));
            rig.Controller.SimulateTelegraphForTesting(0.25f);

            Assert.That(resolution.ConsumedCount, Is.EqualTo(2));
            Assert.That(resolution.DamagePerEnemy, Is.EqualTo(70f));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(7));
        }

        [Test]
        public void TelegraphAllReservedCut_CancelsWithoutDamageConsumptionOrInvulnerability()
        {
            var rig = CreateRig(4);
            var resolution = default(OSExplosionResolution);
            rig.Controller.ExplosionResolved += value => resolution = value;
            Assert.That(rig.Controller.RequestExplosion().Payload, Is.EqualTo(2));

            Assert.That(rig.Chain.TryCutFrom(0, Vector2.zero).Payload, Is.EqualTo(4));
            Assert.That(rig.Controller.ReservedCount, Is.Zero);
            rig.Controller.SimulateTelegraphForTesting(0.25f);

            Assert.That(resolution.WasCancelled, Is.True);
            Assert.That(resolution.ConsumedCount, Is.Zero);
            Assert.That(rig.Chain.ActiveCount, Is.Zero);
            Assert.That(rig.Health.ExplosionInvulnerabilityRemaining, Is.Zero);
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Combat));
        }

        [Test]
        public void CompletionWithQueuedBodySelection_ConsumesBeforeOpeningSelection()
        {
            var rig = CreateRig(4);
            Assert.That(rig.Controller.RequestExplosion().IsAccepted, Is.True);
            Assert.That(rig.Session.QueueSelection(OSSelectionKind.BodyRole).IsAccepted, Is.True);

            rig.Controller.SimulateTelegraphForTesting(0.25f);

            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(2));
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.BodyRoleSelection));
            Assert.That(rig.Session.ActiveSelection?.Kind, Is.EqualTo(OSSelectionKind.BodyRole));
        }

        [Test]
        public void LethalHeadDamageDuringCompletionTick_CancelsExplosionBeforeConsumption()
        {
            var rig = CreateRig(4);
            var resolution = default(OSExplosionResolution);
            rig.Controller.ExplosionResolved += value => resolution = value;
            Assert.That(rig.Controller.RequestExplosion().IsAccepted, Is.True);

            var lethal = new OSDamageEvent(
                100,
                0,
                500,
                1,
                OSTargetKind.PlayerHead,
                100f,
                Vector2.zero);
            Assert.That(rig.Health.TryApplyHeadDamage(lethal).IsAccepted, Is.True);

            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Dead));
            Assert.That(rig.Controller.IsTelegraphActive, Is.False);
            Assert.That(resolution.WasCancelled, Is.True);
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(4));
            Assert.That(rig.Health.ExplosionInvulnerabilityRemaining, Is.Zero);
        }

        private ExplosionRig CreateRig(int segmentCount)
        {
            var root = Track(new GameObject("Step10ExplosionTestRoot"));
            var sessionObject = new GameObject("Session", typeof(OSGameSessionController));
            sessionObject.transform.SetParent(root.transform, false);
            var session = sessionObject.GetComponent<OSGameSessionController>();
            session.Configure(null, false);

            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
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
                head,
                segmentPrefabObject.GetComponent<OSBodySegmentView>(),
                poolRoot,
                64,
                cutGuardDuration: 0.35f);

            var health = head.gameObject.AddComponent<OSPlayerHealth>();
            health.ConfigureForTesting(session, 100f, 0.6f);

            var registryObject = new GameObject("EnemyRegistry", typeof(OSEnemyRegistry));
            registryObject.transform.SetParent(root.transform, false);
            var registry = registryObject.GetComponent<OSEnemyRegistry>();
            registry.ConfigureForTesting(8);

            var growthObject = new GameObject("BodyGrowth", typeof(OSBodyGrowthController));
            growthObject.transform.SetParent(root.transform, false);
            var growth = growthObject.GetComponent<OSBodyGrowthController>();
            growth.ConfigureForTesting(session, chain);

            var controllerObject = new GameObject("ExplosionController", typeof(OSExplosionController));
            controllerObject.transform.SetParent(root.transform, false);
            var controller = controllerObject.GetComponent<OSExplosionController>();
            controller.ConfigureForTesting(session, chain, registry, health, growth);

            Assert.That(session.BeginSession().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(chain.SetDebugSegmentCount(segmentCount).IsAccepted, Is.True);

            return new ExplosionRig(root, session, head, chain, health, registry, growth, controller);
        }

        private OSEnemyController RentEnemy(ExplosionRig rig, Vector2 position, float health)
        {
            var prefabObject = Track(new GameObject(
                "ExplosionEnemyPrefab",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(OSEnemyController)));
            var body = prefabObject.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var prefab = prefabObject.GetComponent<OSEnemyController>();
            prefab.ConfigureForTesting(rig.Registry, rig.Session, rig.Head, health, 0f, 0f, 1f);
            prefabObject.SetActive(false);

            var poolObject = new GameObject("EnemyPool", typeof(OSPoolRegistry), typeof(OSEnemyPoolContext));
            poolObject.transform.SetParent(rig.Root.transform, false);
            var context = poolObject.GetComponent<OSEnemyPoolContext>();
            context.Configure(rig.Registry, rig.Session, rig.Head);
            var pool = poolObject.GetComponent<OSPoolRegistry>();
            pool.ConfigureForTesting(
                poolObject.transform,
                context,
                new OSPoolPrewarmEntry("enemy_chaser", prefab, 1));
            var result = pool.Rent("enemy_chaser", position, Quaternion.identity);
            Assert.That(result.IsAccepted, Is.True, result.ReasonKey);
            return result.Payload as OSEnemyController;
        }

        private T Track<T>(T target) where T : Object
        {
            _created.Add(target);
            return target;
        }

        private readonly struct ExplosionRig
        {
            public ExplosionRig(
                GameObject root,
                OSGameSessionController session,
                Transform head,
                OSBodyChain chain,
                OSPlayerHealth health,
                OSEnemyRegistry registry,
                OSBodyGrowthController growth,
                OSExplosionController controller)
            {
                Root = root;
                Session = session;
                Head = head;
                Chain = chain;
                Health = health;
                Registry = registry;
                Growth = growth;
                Controller = controller;
            }

            public GameObject Root { get; }
            public OSGameSessionController Session { get; }
            public Transform Head { get; }
            public OSBodyChain Chain { get; }
            public OSPlayerHealth Health { get; }
            public OSEnemyRegistry Registry { get; }
            public OSBodyGrowthController Growth { get; }
            public OSExplosionController Controller { get; }
        }
    }
}
