using System.Collections.Generic;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSPlayerDamagePlayModeTests
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
        public void HeadDamage_ReducesHealthAndInvulnerabilityDoesNotRefreshOnRejectedHit()
        {
            var rig = CreateRig(4);
            rig.Resolver.EnqueueDamage(HeadDamage(1, 8f));
            rig.Resolver.ProcessPendingForTesting();

            Assert.That(rig.Health.CurrentHealth, Is.EqualTo(92f));
            Assert.That(rig.Health.HitInvulnerabilityRemaining, Is.EqualTo(0.6f).Within(0.001f));

            rig.Resolver.EnqueueDamage(HeadDamage(2, 20f));
            rig.Resolver.ProcessPendingForTesting();
            Assert.That(rig.Health.CurrentHealth, Is.EqualTo(92f));
            Assert.That(rig.Health.HitInvulnerabilityRemaining, Is.EqualTo(0.6f).Within(0.001f));

            rig.Health.SimulateTimeForTesting(0.6f);
            Assert.That(rig.Health.TryHeal(20f).Payload, Is.EqualTo(100f));
        }

        [Test]
        public void TryCutFrom_FirstMiddleAndTail_RemoveExpectedStableIdsInHeadToTailOrder()
        {
            var rig = CreateRig(4);
            var cutEvents = new List<OSBodyRemovalEvent>();
            rig.Chain.SegmentsCut += cutEvents.Add;

            Assert.That(rig.Chain.TryCutFrom(0, Vector2.zero).Payload, Is.EqualTo(4));
            Assert.That(rig.Chain.ActiveCount, Is.Zero);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, cutEvents[0].RemovedStableIds);

            rig.Chain.SetDebugSegmentCount(4);
            Assert.That(rig.Chain.TryCutFrom(2, Vector2.zero).Payload, Is.EqualTo(2));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 3, 4 }, cutEvents[1].RemovedStableIds);

            rig.Chain.SetDebugSegmentCount(4);
            Assert.That(rig.Chain.TryCutFrom(3, Vector2.zero).Payload, Is.EqualTo(1));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { 4 }, cutEvents[2].RemovedStableIds);
        }

        [Test]
        public void BodyHit_DoesNotChangeHeadHealthAndClosestToHeadCandidateWins()
        {
            var rig = CreateRig(5);
            var tailStableId = rig.Chain.GetActiveSegment(4).StableId;
            var nearHeadStableId = rig.Chain.GetActiveSegment(1).StableId;
            rig.Resolver.EnqueueDamage(BodyDamage(10, tailStableId));
            rig.Resolver.EnqueueDamage(BodyDamage(11, nearHeadStableId));
            rig.Resolver.ProcessPendingForTesting();

            Assert.That(rig.Health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void CutGuard_RejectsAdditionalBodyCutWithoutChangingRemainingChain()
        {
            var rig = CreateRig(4);
            var middle = rig.Chain.GetActiveSegment(2).StableId;
            rig.Resolver.EnqueueDamage(BodyDamage(1, middle));
            rig.Resolver.ProcessPendingForTesting();
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(2));
            Assert.That(rig.Chain.CutGuardRemaining, Is.EqualTo(0.35f).Within(0.001f));

            var headmost = rig.Chain.GetActiveSegment(0).StableId;
            rig.Resolver.EnqueueDamage(BodyDamage(2, headmost));
            rig.Resolver.ProcessPendingForTesting();
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(2));

            rig.Chain.SimulateCutGuardForTesting(0.35f);
            rig.Resolver.EnqueueDamage(BodyDamage(3, headmost));
            rig.Resolver.ProcessPendingForTesting();
            Assert.That(rig.Chain.ActiveCount, Is.Zero);
        }

        [Test]
        public void SameAttack_HeadAndBody_ResolvesOnlyHeadDamage()
        {
            var rig = CreateRig(4);
            var bodyStableId = rig.Chain.GetActiveSegment(1).StableId;
            rig.Resolver.EnqueueDamage(BodyDamage(7, bodyStableId));
            rig.Resolver.EnqueueDamage(HeadDamage(7, 8f));
            rig.Resolver.ProcessPendingForTesting();

            Assert.That(rig.Health.CurrentHealth, Is.EqualTo(92f));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(4));
        }

        [Test]
        public void DifferentAttacks_HeadThenBody_CutsOnlyAfterSurvivingHeadDamage()
        {
            var rig = CreateRig(4);
            var bodyStableId = rig.Chain.GetActiveSegment(2).StableId;
            rig.Resolver.EnqueueDamage(BodyDamage(9, bodyStableId));
            rig.Resolver.EnqueueDamage(HeadDamage(3, 8f));
            rig.Resolver.ProcessPendingForTesting();

            Assert.That(rig.Health.CurrentHealth, Is.EqualTo(92f));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(2));
        }

        [Test]
        public void LethalHeadDamage_StopsBodyCutAndEntersDeadImmediately()
        {
            var rig = CreateRig(4);
            var bodyStableId = rig.Chain.GetActiveSegment(0).StableId;
            rig.Resolver.EnqueueDamage(BodyDamage(2, bodyStableId));
            rig.Resolver.EnqueueDamage(HeadDamage(1, 100f));
            rig.Resolver.ProcessPendingForTesting();

            Assert.That(rig.Health.CurrentHealth, Is.Zero);
            Assert.That(rig.Session.State, Is.EqualTo(OSSessionState.Dead));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(4));
        }

        private DamageRig CreateRig(int segmentCount)
        {
            var root = Track(new GameObject("Step09DamageTestRoot"));
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
            var resolverObject = new GameObject("CombatResolver", typeof(OSPlayerCombatResolver));
            resolverObject.transform.SetParent(root.transform, false);
            var resolver = resolverObject.GetComponent<OSPlayerCombatResolver>();
            resolver.ConfigureForTesting(session, health, chain);

            Assert.That(session.BeginSession().IsAccepted, Is.True);
            session.CompleteActiveSelection();
            session.CompleteActiveSelection();
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(chain.SetDebugSegmentCount(segmentCount).IsAccepted, Is.True);
            return new DamageRig(session, health, resolver, chain);
        }

        private static OSDamageEvent HeadDamage(int attackId, float damage)
        {
            return new OSDamageEvent(
                attackId,
                0,
                500 + attackId,
                1,
                OSTargetKind.PlayerHead,
                damage,
                Vector2.zero);
        }

        private static OSDamageEvent BodyDamage(int attackId, int stableId)
        {
            return new OSDamageEvent(
                attackId,
                0,
                500 + attackId,
                stableId,
                OSTargetKind.PlayerBody,
                8f,
                Vector2.zero);
        }

        private T Track<T>(T target) where T : Object
        {
            _created.Add(target);
            return target;
        }

        private readonly struct DamageRig
        {
            public DamageRig(
                OSGameSessionController session,
                OSPlayerHealth health,
                OSPlayerCombatResolver resolver,
                OSBodyChain chain)
            {
                Session = session;
                Health = health;
                Resolver = resolver;
                Chain = chain;
            }

            public OSGameSessionController Session { get; }
            public OSPlayerHealth Health { get; }
            public OSPlayerCombatResolver Resolver { get; }
            public OSBodyChain Chain { get; }
        }
    }
}
