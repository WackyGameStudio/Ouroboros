using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSEnemyLifecyclePlayModeTests
    {
        private sealed class TestRig
        {
            public GameObject Root;
            public GameObject PrefabObject;
            public GameObject TargetObject;
            public GameObject SessionObject;
            public OSPoolRegistry Pool;
            public OSEnemyRegistry Registry;
            public OSGameSessionController Session;
        }

        private TestRig _rig;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_rig == null)
            {
                return;
            }

            if (_rig.Root != null)
            {
                UnityEngine.Object.DestroyImmediate(_rig.Root);
            }

            if (_rig.PrefabObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_rig.PrefabObject);
            }

            if (_rig.TargetObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_rig.TargetObject);
            }

            if (_rig.SessionObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_rig.SessionObject);
            }

            _rig = null;
        }

        [Test]
        public void RentReturn_TwoHundredInstances_ResetMutableStateAndAssignStableIds()
        {
            _rig = CreateRig(200);
            var enemies = new OSEnemyController[200];
            var previousRuntimeId = 0;

            for (var index = 0; index < enemies.Length; index++)
            {
                enemies[index] = RentEnemy(_rig, new Vector3(index * 0.01f, 0f, 0f));
                Assert.That(enemies[index].RuntimeId, Is.GreaterThan(previousRuntimeId));
                Assert.That(enemies[index].CurrentHealth, Is.EqualTo(18f));
                previousRuntimeId = enemies[index].RuntimeId;
            }

            Assert.That(_rig.Registry.Count, Is.EqualTo(200));
            Assert.That(_rig.Pool.GetActiveCount("enemy_chaser"), Is.EqualTo(200));

            enemies[0].BeginContact(1, OSTargetKind.PlayerHead, _rig.TargetObject.transform);
            Assert.That(enemies[0].ApplyControl(2f).IsAccepted, Is.True);
            Assert.That(enemies[0].TryApplyDamage(5f).Payload, Is.EqualTo(13f));

            for (var index = 0; index < enemies.Length; index++)
            {
                Assert.That(_rig.Pool.Return(enemies[index]).IsAccepted, Is.True);
            }

            Assert.That(_rig.Registry.Count, Is.Zero);
            Assert.That(_rig.Pool.GetActiveCount("enemy_chaser"), Is.Zero);

            var resetEnemy = RentEnemy(_rig, Vector3.zero);
            Assert.That(resetEnemy.CurrentHealth, Is.EqualTo(resetEnemy.MaxHealth));
            Assert.That(resetEnemy.AttackCooldown, Is.Zero);
            Assert.That(resetEnemy.HasContactTarget, Is.False);
            Assert.That(resetEnemy.IsDeathConfirmed, Is.False);
            Assert.That(resetEnemy.RuntimeId, Is.GreaterThan(previousRuntimeId));
        }

        [Test]
        public void Return_DuplicateIsRejectedWithoutCorruptingAvailability()
        {
            _rig = CreateRig(1);
            var enemy = RentEnemy(_rig, Vector3.zero);

            Assert.That(_rig.Pool.Return(enemy).IsAccepted, Is.True);
            var duplicate = _rig.Pool.Return(enemy);

            Assert.That(duplicate.Code, Is.EqualTo(OSResultCode.Duplicate));
            Assert.That(_rig.Registry.Count, Is.Zero);
            Assert.That(_rig.Pool.GetActiveCount("enemy_chaser"), Is.Zero);
            Assert.That(_rig.Pool.GetCapacity("enemy_chaser"), Is.EqualTo(1));
        }

        [Test]
        public void Registry_RegisterUnregisterMaintainsSwapBackIndicesAndLookup()
        {
            _rig = CreateRig(3);
            var first = RentEnemy(_rig, Vector3.left);
            var second = RentEnemy(_rig, Vector3.zero);
            var third = RentEnemy(_rig, Vector3.right);
            var thirdId = third.RuntimeId;

            Assert.That(_rig.Registry.Count, Is.EqualTo(3));
            Assert.That(_rig.Pool.Return(second).IsAccepted, Is.True);

            Assert.That(_rig.Registry.Count, Is.EqualTo(2));
            Assert.That(_rig.Registry.GetAt(0), Is.SameAs(first));
            Assert.That(_rig.Registry.GetAt(1), Is.SameAs(third));
            Assert.That(_rig.Registry.TryGetByRuntimeId(thirdId, out var found), Is.True);
            Assert.That(found, Is.SameAs(third));
            Assert.That(third.RegistryIndex, Is.EqualTo(1));
        }

        [Test]
        public void LethalDamage_ConfirmsDeathAndReturnsExactlyOnce()
        {
            _rig = CreateRig(1);
            var enemy = RentEnemy(_rig, Vector3.zero);
            var deathCount = 0;
            enemy.Died += _ => deathCount++;

            var lethal = enemy.TryApplyDamage(100f);
            var duplicate = enemy.TryApplyDamage(100f);

            Assert.That(lethal.IsAccepted, Is.True);
            Assert.That(duplicate.Code, Is.EqualTo(OSResultCode.Duplicate));
            Assert.That(deathCount, Is.EqualTo(1));
            Assert.That(enemy.IsRented, Is.False);
            Assert.That(_rig.Registry.Count, Is.Zero);
            Assert.That(_rig.Pool.GetActiveCount("enemy_chaser"), Is.Zero);
        }

        [Test]
        public void ContactAttack_UsesUniqueStableIdsAndHonorsInterval()
        {
            _rig = CreateRig(2);
            var first = RentEnemy(_rig, Vector3.left);
            var second = RentEnemy(_rig, Vector3.right);
            var firstAttackId = 0;
            var secondAttackId = 0;
            var firstAttackCount = 0;
            first.ContactAttackRequested += damageEvent =>
            {
                firstAttackId = damageEvent.AttackEventId;
                firstAttackCount++;
            };
            second.ContactAttackRequested += damageEvent => secondAttackId = damageEvent.AttackEventId;
            first.BeginContact(1, OSTargetKind.PlayerHead, _rig.TargetObject.transform);
            second.BeginContact(1, OSTargetKind.PlayerHead, _rig.TargetObject.transform);

            first.SimulateStep(0.01f);
            second.SimulateStep(0.01f);
            first.SimulateStep(0.25f);

            Assert.That(firstAttackId, Is.GreaterThan(0));
            Assert.That(secondAttackId, Is.GreaterThan(firstAttackId));
            Assert.That(firstAttackCount, Is.EqualTo(1));
            Assert.That(first.AttackCooldown, Is.EqualTo(0.75f).Within(0.001f));
        }

        [Test]
        public void ContactAttack_MultipleTargetsShareOneAttackEventId()
        {
            _rig = CreateRig(1);
            var enemy = RentEnemy(_rig, Vector3.zero);
            var events = new List<OSDamageEvent>();
            enemy.ContactAttackRequested += events.Add;
            enemy.BeginContact(1, OSTargetKind.PlayerHead, _rig.TargetObject.transform);
            enemy.BeginContact(22, OSTargetKind.PlayerBody, _rig.TargetObject.transform);

            enemy.SimulateStep(0.01f);

            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].AttackEventId, Is.EqualTo(events[1].AttackEventId));
            Assert.That(events[0].TargetKind, Is.EqualTo(OSTargetKind.PlayerHead));
            Assert.That(events[1].TargetKind, Is.EqualTo(OSTargetKind.PlayerBody));
            Assert.That(enemy.ContactTargetCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SelectionState_PausesMovementAndContactAttackTimer()
        {
            _rig = CreateRig(1, withSession: true);
            Assert.That(_rig.Session.BeginSession().IsAccepted, Is.True);
            var enemy = RentEnemy(_rig, new Vector3(4f, 0f, 0f));
            var attackCount = 0;
            enemy.ContactAttackRequested += _ => attackCount++;
            enemy.BeginContact(1, OSTargetKind.PlayerHead, _rig.TargetObject.transform);
            var pausedPosition = enemy.Position;

            enemy.SimulateStep(0.5f);

            Assert.That(enemy.Position, Is.EqualTo(pausedPosition));
            Assert.That(enemy.AttackCooldown, Is.Zero);
            Assert.That(attackCount, Is.Zero);

            Assert.That(_rig.Session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(_rig.Session.CompleteActiveSelection().IsAccepted, Is.True);
            enemy.SimulateStep(0.1f);
            yield return new WaitForFixedUpdate();

            Assert.That(enemy.Position.x, Is.LessThan(pausedPosition.x));
            Assert.That(enemy.AttackCooldown, Is.EqualTo(1f).Within(Time.fixedDeltaTime + 0.001f));
            Assert.That(attackCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ActiveOneHundredEighty_SimulationHasBoundedCpuAndNoManagedAllocations()
        {
            _rig = CreateRig(180);
            var enemies = new OSEnemyController[180];
            for (var index = 0; index < enemies.Length; index++)
            {
                var angle = index * Mathf.PI * 2f / enemies.Length;
                enemies[index] = RentEnemy(
                    _rig,
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 8f);
            }

            for (var index = 0; index < enemies.Length; index++)
            {
                enemies[index].SimulateStep(0.02f);
            }

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            for (var tick = 0; tick < 120; tick++)
            {
                for (var index = 0; index < enemies.Length; index++)
                {
                    enemies[index].SimulateStep(0.02f);
                }
            }

            stopwatch.Stop();
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            TestContext.WriteLine(
                $"Step06 180 enemies / 120 ticks: {stopwatch.ElapsedMilliseconds} ms, {allocatedBytes} managed bytes.");

            Assert.That(_rig.Registry.Count, Is.EqualTo(180));
            Assert.That(allocatedBytes, Is.LessThanOrEqualTo(2048));
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000));
            yield return null;
        }

        private static OSEnemyController RentEnemy(TestRig rig, Vector3 position)
        {
            var result = rig.Pool.Rent("enemy_chaser", position, Quaternion.identity);
            Assert.That(result.IsAccepted, Is.True, result.ReasonKey);
            return result.Payload as OSEnemyController;
        }

        private static TestRig CreateRig(int capacity, bool withSession = false)
        {
            var rig = new TestRig
            {
                Root = new GameObject("Step06TestRoot"),
                TargetObject = new GameObject("Step06Target"),
                PrefabObject = new GameObject(
                    "Step06EnemyPrefab",
                    typeof(SpriteRenderer),
                    typeof(Rigidbody2D),
                    typeof(CircleCollider2D),
                    typeof(OSEnemyController))
            };
            rig.TargetObject.transform.position = Vector3.zero;

            if (withSession)
            {
                rig.SessionObject = new GameObject("Step06Session", typeof(OSGameSessionController));
                rig.Session = rig.SessionObject.GetComponent<OSGameSessionController>();
                rig.Session.Configure(null, false);
            }

            var registryObject = new GameObject("EnemyRegistry", typeof(OSEnemyRegistry));
            registryObject.transform.SetParent(rig.Root.transform, false);
            rig.Registry = registryObject.GetComponent<OSEnemyRegistry>();
            rig.Registry.ConfigureForTesting(capacity);

            var prefabBody = rig.PrefabObject.GetComponent<Rigidbody2D>();
            prefabBody.bodyType = RigidbodyType2D.Kinematic;
            prefabBody.gravityScale = 0f;
            var prefabController = rig.PrefabObject.GetComponent<OSEnemyController>();
            prefabController.ConfigureForTesting(
                rig.Registry,
                rig.Session,
                rig.TargetObject.transform);
            rig.PrefabObject.SetActive(false);

            var poolObject = new GameObject("PoolRegistry", typeof(OSPoolRegistry), typeof(OSEnemyPoolContext));
            poolObject.transform.SetParent(rig.Root.transform, false);
            rig.Pool = poolObject.GetComponent<OSPoolRegistry>();
            var context = poolObject.GetComponent<OSEnemyPoolContext>();
            context.Configure(rig.Registry, rig.Session, rig.TargetObject.transform);
            rig.Pool.ConfigureForTesting(
                poolObject.transform,
                context,
                new OSPoolPrewarmEntry("enemy_chaser", prefabController, capacity));
            return rig;
        }
    }
}
