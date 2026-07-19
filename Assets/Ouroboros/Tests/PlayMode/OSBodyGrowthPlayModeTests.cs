using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSBodyGrowthPlayModeTests
    {
        private GameObject _root;
        private GameObject _segmentPrefabObject;
        private GameObject _pickupPrefabObject;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            DestroyImmediate(_root);
            DestroyImmediate(_segmentPrefabObject);
            DestroyImmediate(_pickupPrefabObject);
        }

        [Test]
        public void MultipleBodyRequests_AreProcessedSeriallyAndAppendExactlyOnce()
        {
            var rig = CreateGrowthRig(64);
            CompleteStartSelections(rig);

            var fragments = rig.Growth.AddFragments(12);
            Assert.That(fragments.IsAccepted, Is.True);
            Assert.That(fragments.Payload, Is.EqualTo(2));
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.BodyRoleSelection));
            Assert.That(rig.Session.PendingBodySelectionCount, Is.EqualTo(2));

            Assert.That(rig.Growth.ConfirmRole(OSBodyRoleType.Laser).IsAccepted, Is.True);
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(3));
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.BodyRoleSelection));

            Assert.That(rig.Growth.ConfirmRole(OSBodyRoleType.Control).IsAccepted, Is.True);
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(4));
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(rig.Chain.GetRoleCount(OSBodyRoleType.Laser), Is.EqualTo(1));
            Assert.That(rig.Chain.GetRoleCount(OSBodyRoleType.Control), Is.EqualTo(1));
        }

        [Test]
        public void GuardedGauge_QueuesOneRequestAfterDebugTailRemoval()
        {
            var rig = CreateGrowthRig(64);
            CompleteStartSelections(rig);
            Assert.That(rig.Chain.SetDebugSegmentCount(64).IsAccepted, Is.True);

            var deferred = rig.Growth.AddFragments(6);
            Assert.That(deferred.IsAccepted, Is.True);
            Assert.That(deferred.Payload, Is.Zero);
            Assert.That(rig.Growth.HasDeferredRequest, Is.True);
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Combat));

            Assert.That(rig.Growth.DebugRemoveTailSegments(1).IsAccepted, Is.True);
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.BodyRoleSelection));
            Assert.That(rig.Growth.FragmentProgress, Is.Zero);
            Assert.That(rig.Growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(64));
        }

        [Test]
        public void PickupSpawner_MergesNearbyAndPreservesAmountWhenPoolIsFull()
        {
            var rig = CreateGrowthRig(64);
            CompleteStartSelections(rig);
            var pickupRig = ConfigurePickupRig(rig, 2);

            var first = pickupRig.Spawner.Spawn(OSPickupType.BodyFragment, 1, Vector2.zero);
            var nearby = pickupRig.Spawner.Spawn(OSPickupType.BodyFragment, 2, Vector2.right * 0.5f);
            Assert.That(first.IsAccepted, Is.True);
            Assert.That(nearby.Payload, Is.SameAs(first.Payload));
            Assert.That(first.Payload.Amount, Is.EqualTo(3));
            Assert.That(pickupRig.Spawner.ActiveCount, Is.EqualTo(1));

            var second = pickupRig.Spawner.Spawn(OSPickupType.BodyFragment, 4, Vector2.right * 5f);
            var saturated = pickupRig.Spawner.Spawn(OSPickupType.BodyFragment, 5, Vector2.right * 10f);
            Assert.That(second.IsAccepted, Is.True);
            Assert.That(saturated.IsAccepted, Is.True);
            Assert.That(saturated.Payload, Is.SameAs(second.Payload));
            Assert.That(second.Payload.Amount, Is.EqualTo(9));
            Assert.That(pickupRig.Spawner.ActiveCount, Is.EqualTo(2));
        }

        [Test]
        public void PickupSpawner_SeparatesDifferentTypesSpawnedAtSamePosition()
        {
            var rig = CreateGrowthRig(64);
            CompleteStartSelections(rig);
            var pickupRig = ConfigurePickupRig(rig, 3);

            var experience = pickupRig.Spawner.Spawn(OSPickupType.Experience, 1, Vector2.zero);
            var fragment = pickupRig.Spawner.Spawn(OSPickupType.BodyFragment, 1, Vector2.zero);
            var heal = pickupRig.Spawner.Spawn(OSPickupType.Heal, 1, Vector2.zero);

            Assert.That(experience.IsAccepted, Is.True);
            Assert.That(fragment.IsAccepted, Is.True);
            Assert.That(heal.IsAccepted, Is.True);
            Assert.That(fragment.Payload, Is.Not.SameAs(experience.Payload));
            Assert.That(heal.Payload, Is.Not.SameAs(experience.Payload));
            Assert.That(heal.Payload, Is.Not.SameAs(fragment.Payload));
            AssertSeparated(experience.Payload, fragment.Payload, pickupRig.Spawner.SpawnSeparation);
            AssertSeparated(experience.Payload, heal.Payload, pickupRig.Spawner.SpawnSeparation);
            AssertSeparated(fragment.Payload, heal.Payload, pickupRig.Spawner.SpawnSeparation);
        }

        [Test]
        public void PickupCollection_IsPausedDuringSelectionAndAppliesInCombat()
        {
            var rig = CreateGrowthRig(64);
            var pickupRig = ConfigurePickupRig(rig, 1);
            var spawned = pickupRig.Spawner.Spawn(OSPickupType.BodyFragment, 3, Vector2.zero);

            var paused = spawned.Payload.TryCollect(pickupRig.Collector);
            Assert.That(paused.IsAccepted, Is.False);
            Assert.That(spawned.Payload.IsRented, Is.True);
            Assert.That(rig.Growth.FragmentProgress, Is.Zero);

            CompleteStartSelections(rig);
            var collected = spawned.Payload.TryCollect(pickupRig.Collector);
            Assert.That(collected.IsAccepted, Is.True);
            Assert.That(spawned.Payload.IsRented, Is.False);
            Assert.That(rig.Growth.FragmentProgress, Is.EqualTo(3));
        }

        [Test]
        public void SeveredBodyPickup_PreservesRoleAndLeavesRemainderWhenOnlyOneSegmentFits()
        {
            var rig = CreateGrowthRig(3);
            CompleteStartSelections(rig);
            var pickupRig = ConfigurePickupRig(rig, 2);
            var spawned = pickupRig.Spawner.SpawnSeveredBody(
                OSBodyRoleType.Laser,
                2,
                Vector2.zero);

            Assert.That(spawned.IsAccepted, Is.True, spawned.ReasonKey);
            Assert.That(spawned.Payload.PickupType, Is.EqualTo(OSPickupType.SeveredBody));
            Assert.That(spawned.Payload.BodyRole, Is.EqualTo(OSBodyRoleType.Laser));

            var partial = spawned.Payload.TryCollect(pickupRig.Collector);
            Assert.That(partial.IsAccepted, Is.True, partial.ReasonKey);
            Assert.That(partial.Payload, Is.EqualTo(1));
            Assert.That(spawned.Payload.IsRented, Is.True);
            Assert.That(spawned.Payload.Amount, Is.EqualTo(1));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(3));
            Assert.That(rig.Chain.GetActiveSegment(2).Role, Is.EqualTo(OSBodyRoleType.Laser));

            Assert.That(rig.Chain.RemoveTailSegments(1).IsAccepted, Is.True);
            var remainder = spawned.Payload.TryCollect(pickupRig.Collector);
            Assert.That(remainder.IsAccepted, Is.True, remainder.ReasonKey);
            Assert.That(spawned.Payload.IsRented, Is.False);
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(3));
            Assert.That(rig.Chain.GetActiveSegment(2).Role, Is.EqualTo(OSBodyRoleType.Laser));
        }

        private GrowthRig CreateGrowthRig(int capacity)
        {
            _root = new GameObject("Step08GrowthTestRoot");
            var head = new GameObject("Head").transform;
            head.SetParent(_root.transform, false);
            var sessionObject = new GameObject("Session", typeof(OSGameSessionController));
            sessionObject.transform.SetParent(_root.transform, false);
            var session = sessionObject.GetComponent<OSGameSessionController>();
            session.Configure(null, false);

            _segmentPrefabObject = new GameObject(
                "BodySegmentTemplate",
                typeof(SpriteRenderer),
                typeof(CircleCollider2D),
                typeof(OSBodySegmentView));
            _segmentPrefabObject.SetActive(false);
            var poolRoot = new GameObject("SegmentPool").transform;
            poolRoot.SetParent(_root.transform, false);
            var chainObject = new GameObject("BodyChain", typeof(OSBodyChain));
            chainObject.transform.SetParent(_root.transform, false);
            var chain = chainObject.GetComponent<OSBodyChain>();
            chain.ConfigureForTesting(
                head,
                _segmentPrefabObject.GetComponent<OSBodySegmentView>(),
                poolRoot,
                capacity);

            var growthObject = new GameObject("BodyGrowth", typeof(OSBodyGrowthController));
            growthObject.transform.SetParent(_root.transform, false);
            var growth = growthObject.GetComponent<OSBodyGrowthController>();
            growth.ConfigureForTesting(session, chain, 6, capacity);
            Assert.That(session.BeginSession().IsAccepted, Is.True);
            return new GrowthRig(session, chain, growth, head);
        }

        private PickupRig ConfigurePickupRig(GrowthRig rig, int capacity)
        {
            _pickupPrefabObject = new GameObject(
                "PickupTemplate",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(OSPickup));
            var pickupBody = _pickupPrefabObject.GetComponent<Rigidbody2D>();
            pickupBody.bodyType = RigidbodyType2D.Kinematic;
            pickupBody.gravityScale = 0f;
            _pickupPrefabObject.GetComponent<CircleCollider2D>().isTrigger = true;
            _pickupPrefabObject.SetActive(false);

            var poolObject = new GameObject("PickupPool", typeof(OSPoolRegistry));
            poolObject.transform.SetParent(_root.transform, false);
            var pool = poolObject.GetComponent<OSPoolRegistry>();
            pool.ConfigureForTesting(
                poolObject.transform,
                null,
                new OSPoolPrewarmEntry(
                    "body_fragment_pickup",
                    _pickupPrefabObject.GetComponent<OSPickup>(),
                    capacity));

            var spawnerObject = new GameObject("PickupSpawner", typeof(OSPickupSpawner));
            spawnerObject.transform.SetParent(_root.transform, false);
            var spawner = spawnerObject.GetComponent<OSPickupSpawner>();
            spawner.ConfigureForTesting(pool, rig.Session, rig.Growth, rig.Head, capacity);

            var collectorObject = new GameObject(
                "PickupCollector",
                typeof(CircleCollider2D),
                typeof(OSPickupCollector));
            collectorObject.transform.SetParent(rig.Head, false);
            return new PickupRig(spawner, collectorObject.GetComponent<OSPickupCollector>());
        }

        private static void CompleteStartSelections(GrowthRig rig)
        {
            Assert.That(rig.Growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(rig.Growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(2));
        }

        private static void AssertSeparated(OSPickup first, OSPickup second, float minimumDistance)
        {
            Assert.That(Vector2.Distance(first.Position, second.Position),
                Is.GreaterThanOrEqualTo(minimumDistance - 0.0001f));
        }

        private static void DestroyImmediate(Object target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }

        private readonly struct GrowthRig
        {
            public GrowthRig(
                OSGameSessionController session,
                OSBodyChain chain,
                OSBodyGrowthController growth,
                Transform head)
            {
                Session = session;
                Chain = chain;
                Growth = growth;
                Head = head;
            }

            public OSGameSessionController Session { get; }
            public OSBodyChain Chain { get; }
            public OSBodyGrowthController Growth { get; }
            public Transform Head { get; }
        }

        private readonly struct PickupRig
        {
            public PickupRig(OSPickupSpawner spawner, OSPickupCollector collector)
            {
                Spawner = spawner;
                Collector = collector;
            }

            public OSPickupSpawner Spawner { get; }
            public OSPickupCollector Collector { get; }
        }
    }
}
