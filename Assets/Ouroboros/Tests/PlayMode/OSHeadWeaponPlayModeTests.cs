using System;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSHeadWeaponPlayModeTests
    {
        private sealed class TestRig
        {
            public GameObject Root;
            public GameObject EnemyPrefabObject;
            public GameObject ProjectilePrefabObject;
            public GameObject WeaponObject;
            public OSPoolRegistry Pool;
            public OSEnemyRegistry Registry;
            public OSHeadWeapon Weapon;
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

            DestroyImmediate(_rig.Root);
            DestroyImmediate(_rig.EnemyPrefabObject);
            DestroyImmediate(_rig.ProjectilePrefabObject);
            DestroyImmediate(_rig.WeaponObject);
            _rig = null;
        }

        [Test]
        public void NearestTarget_SelectsClosestAcrossOneHundredEightyWithoutManagedAllocation()
        {
            _rig = CreateRig(enemyCapacity: 180, projectileCapacity: 1);
            OSEnemyController expected = null;
            for (var index = 0; index < 180; index++)
            {
                var enemy = RentEnemy(_rig, new Vector2(2f + index * 0.01f, 0f));
                expected ??= enemy;
            }

            Assert.That(_rig.Registry.FindNearestTarget(Vector2.zero, 6f), Is.SameAs(expected));
            _rig.Registry.FindNearestTarget(Vector2.zero, 6f);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var scan = 0; scan < 500; scan++)
            {
                _rig.Registry.FindNearestTarget(Vector2.zero, 6f, expected.RuntimeId);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            TestContext.WriteLine($"Step07 nearest scan: 180 candidates x 500, {allocated} managed bytes.");
            Assert.That(allocated, Is.LessThanOrEqualTo(256));
        }

        [Test]
        public void EqualDistance_PreservesCurrentTarget()
        {
            _rig = CreateRig(enemyCapacity: 2, projectileCapacity: 1);
            RentEnemy(_rig, Vector2.left * 2f);
            var current = RentEnemy(_rig, Vector2.right * 2f);

            var selected = _rig.Registry.FindNearestTarget(Vector2.zero, 6f, current.RuntimeId);

            Assert.That(selected, Is.SameAs(current));
        }

        [Test]
        public void EqualDistanceWithoutCurrentTarget_SelectsSmallestRuntimeId()
        {
            _rig = CreateRig(enemyCapacity: 2, projectileCapacity: 1);
            var first = RentEnemy(_rig, Vector2.left * 2f);
            var second = RentEnemy(_rig, Vector2.right * 2f);

            var selected = _rig.Registry.FindNearestTarget(Vector2.zero, 6f);

            Assert.That(first.RuntimeId, Is.LessThan(second.RuntimeId));
            Assert.That(selected, Is.SameAs(first));
        }

        [Test]
        public void OutOfRange_DoesNotFireOrConsumeCooldown()
        {
            _rig = CreateRig(enemyCapacity: 1, projectileCapacity: 1);
            RentEnemy(_rig, Vector2.right * 6.1f);

            _rig.Weapon.SimulateStep(3f);

            Assert.That(_rig.Weapon.ShotsFired, Is.Zero);
            Assert.That(_rig.Weapon.Cooldown, Is.Zero);
            Assert.That(_rig.Pool.GetActiveCount("head_projectile"), Is.Zero);
        }

        [Test]
        public void NoTargetForThreeSeconds_TargetEntryFiresImmediately()
        {
            _rig = CreateRig(enemyCapacity: 1, projectileCapacity: 1);
            _rig.Weapon.SimulateStep(3f);
            var target = RentEnemy(_rig, Vector2.right * 4f);

            _rig.Weapon.SimulateStep(0f);

            Assert.That(_rig.Weapon.ShotsFired, Is.EqualTo(1));
            Assert.That(_rig.Weapon.CurrentTargetRuntimeId, Is.EqualTo(target.RuntimeId));
            Assert.That(_rig.Weapon.Cooldown, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void FullProjectilePool_DoesNotConsumeCooldownOrCreateDamage()
        {
            _rig = CreateRig(enemyCapacity: 1, projectileCapacity: 1);
            var target = RentEnemy(_rig, Vector2.right * 3f);
            var heldProjectile = RentProjectile(_rig);

            _rig.Weapon.SimulateStep(3f);

            Assert.That(_rig.Weapon.ShotsFired, Is.Zero);
            Assert.That(_rig.Weapon.Cooldown, Is.Zero);
            Assert.That(target.CurrentHealth, Is.EqualTo(18f));
            Assert.That(heldProjectile.ReturnToPool(), Is.True);
        }

        [Test]
        public void SameProjectile_MultipleCollidersDamageEnemyOnlyOnce()
        {
            _rig = CreateRig(enemyCapacity: 1, projectileCapacity: 1, projectilePierce: 1);
            var target = RentEnemy(_rig, Vector2.right * 2f);
            var projectile = RentProjectile(_rig);
            var attackId = _rig.Pool.NextAttackEventId();
            Assert.That(attackId.IsAccepted, Is.True);
            Assert.That(projectile.Launch(
                attackId.Payload,
                1,
                Vector2.right,
                10f,
                6f,
                1,
                _rig.Weapon).IsAccepted, Is.True);

            Assert.That(projectile.TryHitEnemy(target), Is.True);
            Assert.That(projectile.TryHitEnemy(target), Is.False);

            Assert.That(target.CurrentHealth, Is.EqualTo(8f));
            Assert.That(projectile.HitCount, Is.EqualTo(1));
            Assert.That(_rig.Weapon.HitsConfirmed, Is.EqualTo(1));
            Assert.That(projectile.ReturnToPool(), Is.True);
        }

        [Test]
        public void TwoProjectiles_ApplyDamageAndConfirmEnemyDeathOnce()
        {
            _rig = CreateRig(enemyCapacity: 1, projectileCapacity: 2);
            var target = RentEnemy(_rig, Vector2.right * 2f);

            for (var shot = 0; shot < 2; shot++)
            {
                var projectile = RentProjectile(_rig);
                var attackId = _rig.Pool.NextAttackEventId();
                Assert.That(projectile.Launch(
                    attackId.Payload,
                    1,
                    Vector2.right,
                    10f,
                    6f,
                    0,
                    _rig.Weapon).IsAccepted, Is.True);
                Assert.That(projectile.TryHitEnemy(target), Is.True);
            }

            Assert.That(target.IsRented, Is.False);
            Assert.That(_rig.Registry.Count, Is.Zero);
            Assert.That(_rig.Weapon.HitsConfirmed, Is.EqualTo(2));
            Assert.That(_rig.Weapon.DefeatsConfirmed, Is.EqualTo(1));
            Assert.That(_rig.Pool.GetActiveCount("head_projectile"), Is.Zero);
        }

        private static TestRig CreateRig(
            int enemyCapacity,
            int projectileCapacity,
            int projectilePierce = 0)
        {
            var rig = new TestRig
            {
                Root = new GameObject("Step07TestRoot"),
                EnemyPrefabObject = new GameObject(
                    "Step07EnemyPrefab",
                    typeof(SpriteRenderer),
                    typeof(Rigidbody2D),
                    typeof(CircleCollider2D),
                    typeof(OSEnemyController)),
                ProjectilePrefabObject = new GameObject(
                    "Step07ProjectilePrefab",
                    typeof(SpriteRenderer),
                    typeof(Rigidbody2D),
                    typeof(CircleCollider2D),
                    typeof(OSProjectile)),
                WeaponObject = new GameObject("Step07Weapon", typeof(OSHeadWeapon))
            };

            var registryObject = new GameObject("EnemyRegistry", typeof(OSEnemyRegistry));
            registryObject.transform.SetParent(rig.Root.transform, false);
            rig.Registry = registryObject.GetComponent<OSEnemyRegistry>();
            rig.Registry.ConfigureForTesting(enemyCapacity);

            var enemyBody = rig.EnemyPrefabObject.GetComponent<Rigidbody2D>();
            enemyBody.bodyType = RigidbodyType2D.Kinematic;
            enemyBody.gravityScale = 0f;
            var enemyPrefab = rig.EnemyPrefabObject.GetComponent<OSEnemyController>();
            enemyPrefab.ConfigureForTesting(rig.Registry, null, rig.WeaponObject.transform);
            rig.EnemyPrefabObject.SetActive(false);

            var projectileBody = rig.ProjectilePrefabObject.GetComponent<Rigidbody2D>();
            projectileBody.bodyType = RigidbodyType2D.Kinematic;
            projectileBody.gravityScale = 0f;
            rig.ProjectilePrefabObject.GetComponent<CircleCollider2D>().isTrigger = true;
            var projectilePrefab = rig.ProjectilePrefabObject.GetComponent<OSProjectile>();
            projectilePrefab.ConfigureRuntime(null);
            rig.ProjectilePrefabObject.SetActive(false);

            var poolObject = new GameObject("PoolRegistry", typeof(OSPoolRegistry), typeof(OSEnemyPoolContext));
            poolObject.transform.SetParent(rig.Root.transform, false);
            rig.Pool = poolObject.GetComponent<OSPoolRegistry>();
            var context = poolObject.GetComponent<OSEnemyPoolContext>();
            context.Configure(rig.Registry, null, rig.WeaponObject.transform);
            rig.Pool.ConfigureForTesting(
                poolObject.transform,
                context,
                new OSPoolPrewarmEntry("enemy_chaser", enemyPrefab, enemyCapacity),
                new OSPoolPrewarmEntry("head_projectile", projectilePrefab, projectileCapacity));

            rig.Weapon = rig.WeaponObject.GetComponent<OSHeadWeapon>();
            rig.Weapon.ConfigureForTesting(
                rig.Pool,
                rig.Registry,
                null,
                rig.WeaponObject.transform,
                projectilePierce: projectilePierce);
            return rig;
        }

        private static OSEnemyController RentEnemy(TestRig rig, Vector2 position)
        {
            var result = rig.Pool.Rent("enemy_chaser", position, Quaternion.identity);
            Assert.That(result.IsAccepted, Is.True, result.ReasonKey);
            return result.Payload as OSEnemyController;
        }

        private static OSProjectile RentProjectile(TestRig rig)
        {
            var result = rig.Pool.Rent("head_projectile", Vector3.zero, Quaternion.identity);
            Assert.That(result.IsAccepted, Is.True, result.ReasonKey);
            return result.Payload as OSProjectile;
        }

        private static void DestroyImmediate(UnityEngine.Object target)
        {
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
