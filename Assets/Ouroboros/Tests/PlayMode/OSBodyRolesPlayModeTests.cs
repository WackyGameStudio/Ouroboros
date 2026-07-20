using System.Collections.Generic;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSBodyRolesPlayModeTests
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
        public void AttackSegments_FireIndependentlyAtSameNearestEnemy()
        {
            var rig = CreateRig(headProjectileCapacity: 4);
            rig.Chain.AppendSegment(OSBodyRoleType.Attack);
            rig.Chain.AppendSegment(OSBodyRoleType.Attack);
            var enemy = RentEnemy(rig, new Vector2(1f, 0f));
            var attack = AddAttackRole(rig);
            var targetIds = new List<int>();
            attack.Fired += feedback => targetIds.Add(feedback.TargetRuntimeId);

            attack.SimulateStep(0.01f);

            Assert.That(attack.ActiveSegmentCount, Is.EqualTo(2));
            Assert.That(attack.ShotsFired, Is.EqualTo(2));
            Assert.That(rig.Pool.GetActiveCount("head_projectile"), Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { enemy.RuntimeId, enemy.RuntimeId }, targetIds);
        }

        [Test]
        public void AttackPoolSaturation_DoesNotConsumeFailedSegmentCooldown()
        {
            var rig = CreateRig(headProjectileCapacity: 1);
            rig.Chain.AppendSegment(OSBodyRoleType.Attack);
            rig.Chain.AppendSegment(OSBodyRoleType.Attack);
            RentEnemy(rig, new Vector2(1f, 0f));
            var attack = AddAttackRole(rig);

            attack.SimulateStep(0.01f);

            var firstId = rig.Chain.GetActiveSegment(0).StableId;
            var secondId = rig.Chain.GetActiveSegment(1).StableId;
            Assert.That(attack.ShotsFired, Is.EqualTo(1));
            Assert.That(
                new[]
                {
                    attack.GetCooldownForTesting(firstId),
                    attack.GetCooldownForTesting(secondId)
                },
                Has.Exactly(1).EqualTo(0f));
        }

        [Test]
        public void RemovedAttackSegment_StopsNewFireButExistingProjectileLives()
        {
            var rig = CreateRig(headProjectileCapacity: 2);
            rig.Chain.AppendSegment(OSBodyRoleType.Shield);
            rig.Chain.AppendSegment(OSBodyRoleType.Attack);
            RentEnemy(rig, new Vector2(1f, 0f));
            var attack = AddAttackRole(rig);
            attack.SimulateStep(0.01f);
            Assert.That(rig.Pool.GetActiveCount("head_projectile"), Is.EqualTo(1));

            rig.Chain.RemoveTailSegments(1);
            attack.SimulateStep(2f);

            Assert.That(attack.ActiveSegmentCount, Is.Zero);
            Assert.That(attack.ShotsFired, Is.EqualTo(1));
            Assert.That(rig.Pool.GetActiveCount("head_projectile"), Is.EqualTo(1));
        }

        [Test]
        public void AttackCooldown_DoesNotAdvanceDuringSelectionOrDeath()
        {
            var rig = CreateRig(headProjectileCapacity: 2);
            rig.Chain.AppendSegment(OSBodyRoleType.Attack);
            RentEnemy(rig, new Vector2(1f, 0f));
            var attack = AddAttackRole(rig);
            attack.SimulateStep(0.01f);
            var stableId = rig.Chain.GetActiveSegment(0).StableId;
            Assert.That(attack.GetCooldownForTesting(stableId), Is.EqualTo(1f).Within(0.001f));

            rig.Session.QueueSelection(OSSelectionKind.BodyRole);
            rig.Session.ProcessPendingSelection();
            attack.SimulateStep(0.75f);
            Assert.That(attack.GetCooldownForTesting(stableId), Is.EqualTo(1f).Within(0.001f));

            rig.Session.CompleteActiveSelection();
            rig.Session.RequestDeath();
            attack.SimulateStep(0.75f);
            Assert.That(attack.GetCooldownForTesting(stableId), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void LaserTelegraphViewAndDamage_UseSameSnapshotAndPierceMultipleEnemies()
        {
            var rig = CreateRig();
            rig.Chain.AppendSegment(OSBodyRoleType.Laser);
            var source = rig.Chain.GetActiveSegment(0);
            var origin = (Vector2)source.View.transform.position;
            var first = RentEnemy(rig, origin + Vector2.right * 2f, 40f);
            var second = RentEnemy(rig, origin + Vector2.right * 4f, 40f);
            var view = CreateLineView(rig.Root.transform, "LaserView");
            var laser = AddLaserRole(rig, new[] { view });

            laser.SimulateStep(0f);
            Assert.That(laser.TryGetSnapshotForTesting(source.StableId, out var snapshot), Is.True);
            Assert.That(view.enabled, Is.True);
            Assert.That((Vector2)view.GetPosition(0), Is.EqualTo(snapshot.Origin));
            Assert.That((Vector2)view.GetPosition(1), Is.EqualTo(snapshot.End));
            Assert.That(view.widthMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(view.startWidth, Is.EqualTo(snapshot.Width).Within(0.0001f));
            Assert.That(view.endWidth, Is.EqualTo(snapshot.Width).Within(0.0001f));

            laser.SimulateStep(0.2f);
            Assert.That(first.CurrentHealth, Is.EqualTo(28f));
            Assert.That(second.CurrentHealth, Is.EqualTo(28f));
            Assert.That(laser.HitsConfirmed, Is.EqualTo(2));
            Assert.That(view.enabled, Is.False);
        }

        [Test]
        public void LaserDamage_HitsEveryEnemyWhoseHurtboxOverlapsBeam()
        {
            var rig = CreateRig();
            rig.Chain.AppendSegment(OSBodyRoleType.Laser);
            var origin = (Vector2)rig.Chain.GetActiveSegment(0).View.transform.position;
            var aimedTarget = RentEnemy(rig, origin + Vector2.right * 2f, 40f);
            var edgeOverlap = RentEnemy(rig, origin + new Vector2(4f, 0.4f), 40f);
            var outside = RentEnemy(rig, origin + new Vector2(4f, 0.5f), 40f);
            var laser = AddLaserRole(rig);

            Physics2D.SyncTransforms();
            laser.SimulateStep(0f);
            laser.SimulateStep(0.2f);

            Assert.That(aimedTarget.CurrentHealth, Is.EqualTo(28f));
            Assert.That(edgeOverlap.CurrentHealth, Is.EqualTo(28f));
            Assert.That(outside.CurrentHealth, Is.EqualTo(40f));
            Assert.That(laser.HitsConfirmed, Is.EqualTo(2));
        }

        [Test]
        public void LaserWithMultipleTargetColliders_DamagesRegistryEnemyOnce()
        {
            var rig = CreateRig();
            rig.Chain.AppendSegment(OSBodyRoleType.Laser);
            var origin = (Vector2)rig.Chain.GetActiveSegment(0).View.transform.position;
            var enemy = RentEnemy(rig, origin + Vector2.right * 2f, 40f);
            enemy.gameObject.AddComponent<BoxCollider2D>().isTrigger = true;
            var child = new GameObject("ExtraHurtbox", typeof(CircleCollider2D));
            child.transform.SetParent(enemy.transform, false);
            child.GetComponent<CircleCollider2D>().isTrigger = true;
            var laser = AddLaserRole(rig);

            laser.SimulateStep(0f);
            laser.SimulateStep(0.2f);

            Assert.That(enemy.CurrentHealth, Is.EqualTo(28f));
            Assert.That(laser.HitsConfirmed, Is.EqualTo(1));
        }

        [Test]
        public void DifferentLasers_EachDamageSameEnemy()
        {
            var rig = CreateRig();
            rig.Chain.AppendSegment(OSBodyRoleType.Laser);
            rig.Chain.AppendSegment(OSBodyRoleType.Laser);
            var enemy = RentEnemy(rig, new Vector2(2f, 0f), 50f);
            var laser = AddLaserRole(rig);

            laser.SimulateStep(0f);
            laser.SimulateStep(0.2f);

            Assert.That(enemy.CurrentHealth, Is.EqualTo(26f));
            Assert.That(laser.BeamsFired, Is.EqualTo(2));
        }

        [Test]
        public void RemovedLaserSegment_CancelsTelegraphWithoutDamage()
        {
            var rig = CreateRig();
            rig.Chain.AppendSegment(OSBodyRoleType.Shield);
            rig.Chain.AppendSegment(OSBodyRoleType.Laser);
            var origin = (Vector2)rig.Chain.GetActiveSegment(1).View.transform.position;
            var enemy = RentEnemy(rig, origin + Vector2.right * 2f, 40f);
            var laser = AddLaserRole(rig);
            var cancelled = 0;
            laser.LaserResolved += result => cancelled += result.Cancelled ? 1 : 0;
            laser.SimulateStep(0f);

            rig.Chain.RemoveTailSegments(1);
            laser.SimulateStep(0.2f);

            Assert.That(cancelled, Is.EqualTo(1));
            Assert.That(laser.ActiveTelegraphCount, Is.Zero);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(40f));
        }

        [Test]
        public void ControlProjectile_DealsZeroDamageAndAppliesNormalDuration()
        {
            var rig = CreateRig(controlProjectileCapacity: 2);
            rig.Chain.AppendSegment(OSBodyRoleType.Control);
            var enemy = RentEnemy(rig, new Vector2(1f, 0f), 40f);
            var control = AddControlRole(rig);
            control.SimulateStep(0.01f);
            var projectile = FindRented<OSControlProjectile>(rig.Root.transform);

            Assert.That(projectile, Is.Not.Null);
            Assert.That(projectile.Damage, Is.Zero);
            Assert.That(projectile.TryHitEnemy(enemy), Is.True);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(40f));
            Assert.That(enemy.MovementControlRemaining, Is.EqualTo(1f).Within(0.001f));
            Assert.That(control.ControlsApplied, Is.EqualTo(1));
        }

        [Test]
        public void ControlReapplication_UsesMaximumAndSelectionFreezesRemainingTime()
        {
            var rig = CreateRig();
            var enemy = RentEnemy(rig, Vector2.right, 40f);
            enemy.ApplyControl(1f);
            enemy.SimulateStep(0.4f);
            enemy.ApplyControl(0.5f);
            Assert.That(enemy.MovementControlRemaining, Is.EqualTo(0.6f).Within(0.001f));

            rig.Session.QueueSelection(OSSelectionKind.BodyRole);
            rig.Session.ProcessPendingSelection();
            enemy.SimulateStep(0.5f);
            Assert.That(enemy.MovementControlRemaining, Is.EqualTo(0.6f).Within(0.001f));
        }

        [Test]
        public void BossControl_UsesHalfDurationAndDoesNotCancelAttackCasting()
        {
            var rig = CreateRig();
            var enemy = RentEnemy(
                rig,
                Vector2.right,
                100f,
                OSEnemyArchetype.BossSwarmCore,
                controlAttack: false);

            enemy.ApplyControl(OSBodyRoleMath.SelectControlDuration(enemy.Archetype, 1f, 0.5f));

            Assert.That(enemy.MovementControlRemaining, Is.EqualTo(0.5f));
            Assert.That(enemy.AttackControlRemaining, Is.Zero);
        }

        [Test]
        public void ControlProjectile_RejectsNonZeroDamagePayload()
        {
            var rig = CreateRig(controlProjectileCapacity: 1);
            var rent = rig.Pool.Rent("body_control_projectile", Vector3.zero, Quaternion.identity);
            Assert.That(rent.Payload, Is.TypeOf<OSControlProjectile>());
            var projectile = (OSControlProjectile)rent.Payload;

            var result = projectile.Launch(1, 1, Vector2.right, 0.01f, 6f, 1f, 0.5f, null);

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Code, Is.EqualTo(OSResultCode.RejectedRequirement));
        }

        [Test]
        public void ShieldResolver_BlocksHeadAndBodyDamageWithoutChangingHealthOrChain()
        {
            var headRig = CreateRig();
            headRig.Chain.AppendSegment(OSBodyRoleType.Shield);
            var headShield = AddShieldRole(headRig);
            var headHealth = AddHealth(headRig);
            var headResolver = AddResolver(headRig, headHealth, headShield);
            var shieldPosition = (Vector2)headRig.Chain.GetActiveSegment(0).View.transform.position;
            headResolver.EnqueueDamage(HeadDamage(1, shieldPosition));
            headResolver.ProcessPendingForTesting();
            Assert.That(headHealth.CurrentHealth, Is.EqualTo(100f));
            Assert.That(headShield.ChargedCount, Is.Zero);

            var bodyRig = CreateRig();
            bodyRig.Chain.AppendSegment(OSBodyRoleType.Shield);
            bodyRig.Chain.AppendSegment(OSBodyRoleType.Attack);
            var bodyShield = AddShieldRole(bodyRig);
            var bodyHealth = AddHealth(bodyRig);
            var bodyResolver = AddResolver(bodyRig, bodyHealth, bodyShield);
            var target = bodyRig.Chain.GetActiveSegment(1);
            bodyResolver.EnqueueDamage(BodyDamage(2, target));
            bodyResolver.ProcessPendingForTesting();
            Assert.That(bodyRig.Chain.ActiveCount, Is.EqualTo(2));
            Assert.That(bodyShield.ChargedCount, Is.Zero);
        }

        [Test]
        public void ShieldResolver_UsesColliderContactPointAtRangeBoundary()
        {
            var rig = CreateRig();
            rig.Chain.AppendSegment(OSBodyRoleType.Shield);
            rig.Chain.AppendSegment(OSBodyRoleType.Attack);
            var shield = AddShieldRole(rig);
            var health = AddHealth(rig);
            var resolver = AddResolver(rig, health, shield);
            var shieldSegment = rig.Chain.GetActiveSegment(0);
            var target = rig.Chain.GetActiveSegment(1);
            var shieldPosition = (Vector2)shieldSegment.View.transform.position;
            target.View.transform.position = shieldPosition + Vector2.right * 1.8f;
            var targetCollider = target.View.GetComponentInChildren<Collider2D>(true);
            Assert.That(targetCollider, Is.Not.Null);
            Physics2D.SyncTransforms();

            var enemy = RentEnemy(rig, shieldPosition + Vector2.right * 1.3f);
            enemy.ConfigureForTesting(
                rig.Enemies,
                rig.Session,
                rig.Head,
                damage: 8f,
                speed: 0f);
            enemy.ContactAttackRequested += damageEvent => resolver.EnqueueDamage(damageEvent);
            enemy.BeginContact(
                target.StableId,
                OSTargetKind.PlayerBody,
                target.View.transform,
                targetCollider);

            var contactPoint = targetCollider.ClosestPoint(enemy.Position);
            Assert.That(Vector2.Distance(shieldPosition, target.View.transform.position),
                Is.GreaterThan(shield.Radius));
            Assert.That(Vector2.Distance(shieldPosition, contactPoint),
                Is.LessThanOrEqualTo(shield.Radius));

            enemy.SimulateStep(0.01f);
            resolver.ProcessPendingForTesting();

            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(2));
            Assert.That(shield.ChargedCount, Is.Zero);
        }

        [Test]
        public void OverlappingShieldTie_ConsumesHeadmostOneOnly()
        {
            var rig = CreateRig();
            rig.Chain.AppendSegment(OSBodyRoleType.Shield);
            rig.Chain.AppendSegment(OSBodyRoleType.Shield);
            var shield = AddShieldRole(rig);
            var first = rig.Chain.GetActiveSegment(0);
            var second = rig.Chain.GetActiveSegment(1);
            var midpoint = ((Vector2)first.View.transform.position +
                            (Vector2)second.View.transform.position) * 0.5f;

            var result = shield.TryBlockDamage(HeadDamage(3, midpoint));

            Assert.That(result.Payload, Is.EqualTo(first.StableId));
            Assert.That(shield.GetChargesForTesting(first.StableId), Is.Zero);
            Assert.That(shield.GetChargesForTesting(second.StableId), Is.EqualTo(1));
        }

        [Test]
        public void InvulnerableOrCutGuardRejectedDamage_DoesNotConsumeShield()
        {
            var rig = CreateRig();
            rig.Chain.AppendSegment(OSBodyRoleType.Shield);
            rig.Chain.AppendSegment(OSBodyRoleType.Attack);
            rig.Chain.AppendSegment(OSBodyRoleType.Control);
            var shield = AddShieldRole(rig);
            var health = AddHealth(rig);
            var resolver = AddResolver(rig, health, shield);
            var shieldId = rig.Chain.GetActiveSegment(0).StableId;

            Assert.That(
                health.TryApplyHeadDamage(HeadDamage(40, rig.Chain.GetActiveSegment(0).View.transform.position))
                    .IsAccepted,
                Is.True);
            resolver.EnqueueDamage(HeadDamage(4, rig.Chain.GetActiveSegment(0).View.transform.position));
            resolver.ProcessPendingForTesting();
            Assert.That(shield.GetChargesForTesting(shieldId), Is.EqualTo(1));

            rig.Chain.TryCutFrom(2, Vector2.zero);
            rig.Chain.AppendSegment(OSBodyRoleType.Attack);
            var target = rig.Chain.GetActiveSegment(2);
            resolver.EnqueueDamage(BodyDamage(5, target));
            resolver.ProcessPendingForTesting();
            Assert.That(shield.GetChargesForTesting(shieldId), Is.EqualTo(1));
            Assert.That(rig.Chain.ActiveCount, Is.EqualTo(3));
        }

        [Test]
        public void ShieldRecharge_PausesDuringSelectionAndRemovalDiscardsTimer()
        {
            var rig = CreateRig();
            rig.Chain.AppendSegment(OSBodyRoleType.Shield);
            var shield = AddShieldRole(rig);
            var segment = rig.Chain.GetActiveSegment(0);
            shield.TryBlockDamage(HeadDamage(6, segment.View.transform.position));
            Assert.That(shield.GetRechargeForTesting(segment.StableId), Is.EqualTo(6f));

            rig.Session.QueueSelection(OSSelectionKind.BodyRole);
            rig.Session.ProcessPendingSelection();
            shield.SimulateStep(6f);
            Assert.That(shield.GetChargesForTesting(segment.StableId), Is.Zero);
            Assert.That(shield.GetRechargeForTesting(segment.StableId), Is.EqualTo(6f));

            rig.Session.CompleteActiveSelection();
            shield.SimulateStep(6f);
            Assert.That(shield.GetChargesForTesting(segment.StableId), Is.EqualTo(1));

            shield.TryBlockDamage(HeadDamage(7, segment.View.transform.position));
            rig.Chain.RemoveTailSegments(1);
            shield.SimulateStep(10f);
            Assert.That(shield.ActiveSegmentCount, Is.Zero);
            Assert.That(shield.ChargedCount, Is.Zero);
        }

        private RoleRig CreateRig(int headProjectileCapacity = 4, int controlProjectileCapacity = 4)
        {
            var root = Track(new GameObject("Step11BodyRolesTestRoot"));
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
            segmentPrefabObject.transform.SetParent(root.transform, false);
            segmentPrefabObject.SetActive(false);
            var segmentPool = new GameObject("SegmentPool").transform;
            segmentPool.SetParent(root.transform, false);
            var chainObject = new GameObject("BodyChain", typeof(OSBodyChain));
            chainObject.transform.SetParent(root.transform, false);
            var chain = chainObject.GetComponent<OSBodyChain>();
            chain.ConfigureForTesting(
                head,
                segmentPrefabObject.GetComponent<OSBodySegmentView>(),
                segmentPool,
                64,
                cutGuardDuration: 0.35f);

            var registryObject = new GameObject("RoleRegistry", typeof(OSBodyRoleRegistry));
            registryObject.transform.SetParent(root.transform, false);
            var roleRegistry = registryObject.GetComponent<OSBodyRoleRegistry>();
            roleRegistry.ConfigureForTesting(chain);

            var enemyRegistryObject = new GameObject("EnemyRegistry", typeof(OSEnemyRegistry));
            enemyRegistryObject.transform.SetParent(root.transform, false);
            var enemyRegistry = enemyRegistryObject.GetComponent<OSEnemyRegistry>();
            enemyRegistry.ConfigureForTesting(16);

            var contextObject = new GameObject("PoolContext", typeof(OSEnemyPoolContext));
            contextObject.transform.SetParent(root.transform, false);
            var context = contextObject.GetComponent<OSEnemyPoolContext>();
            context.Configure(enemyRegistry, session, head);
            var poolObject = new GameObject("Pool", typeof(OSPoolRegistry));
            poolObject.transform.SetParent(root.transform, false);
            var poolRoot = new GameObject("PoolRoot").transform;
            poolRoot.SetParent(root.transform, false);

            var headProjectilePrefab = CreateHeadProjectilePrefab(root.transform);
            var controlProjectilePrefab = CreateControlProjectilePrefab(root.transform);
            var enemyPrefab = CreateEnemyPrefab(root.transform, enemyRegistry, session, head);
            var pool = poolObject.GetComponent<OSPoolRegistry>();
            pool.ConfigureForTesting(
                poolRoot,
                context,
                new OSPoolPrewarmEntry("head_projectile", headProjectilePrefab, headProjectileCapacity),
                new OSPoolPrewarmEntry(
                    "body_control_projectile",
                    controlProjectilePrefab,
                    controlProjectileCapacity),
                new OSPoolPrewarmEntry("enemy", enemyPrefab, 8));

            Assert.That(session.BeginSession().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            return new RoleRig(root, session, head, chain, roleRegistry, enemyRegistry, pool);
        }

        private OSAttackBodyRole AddAttackRole(RoleRig rig)
        {
            var role = rig.Root.AddComponent<OSAttackBodyRole>();
            role.ConfigureForTesting(rig.Roles, rig.Enemies, rig.Pool, rig.Session);
            return role;
        }

        private OSLaserBodyRole AddLaserRole(RoleRig rig, LineRenderer[] views = null)
        {
            var role = rig.Root.AddComponent<OSLaserBodyRole>();
            role.ConfigureForTesting(rig.Roles, rig.Enemies, rig.Session, views);
            return role;
        }

        private OSControlBodyRole AddControlRole(RoleRig rig)
        {
            var role = rig.Root.AddComponent<OSControlBodyRole>();
            role.ConfigureForTesting(rig.Roles, rig.Enemies, rig.Pool, rig.Session);
            return role;
        }

        private OSShieldBodyRole AddShieldRole(RoleRig rig)
        {
            var role = rig.Root.AddComponent<OSShieldBodyRole>();
            role.ConfigureForTesting(rig.Roles, rig.Session);
            return role;
        }

        private OSPlayerHealth AddHealth(RoleRig rig)
        {
            var health = rig.Head.gameObject.AddComponent<OSPlayerHealth>();
            health.ConfigureForTesting(rig.Session, 100f, 0.6f);
            return health;
        }

        private OSPlayerCombatResolver AddResolver(
            RoleRig rig,
            OSPlayerHealth health,
            OSShieldBodyRole shield)
        {
            var resolver = rig.Root.AddComponent<OSPlayerCombatResolver>();
            resolver.ConfigureForTesting(rig.Session, health, rig.Chain, shield);
            return resolver;
        }

        private OSEnemyController RentEnemy(
            RoleRig rig,
            Vector2 position,
            float health = 18f,
            OSEnemyArchetype archetype = OSEnemyArchetype.Chaser,
            bool controlAttack = false)
        {
            var rent = rig.Pool.Rent("enemy", position, Quaternion.identity);
            Assert.That(rent.IsAccepted, Is.True, rent.ReasonKey);
            var enemy = rent.Payload as OSEnemyController;
            Assert.That(enemy, Is.Not.Null);
            enemy.ConfigureForTesting(
                rig.Enemies,
                rig.Session,
                rig.Head,
                health,
                0f,
                0f,
                1f,
                archetype,
                controlMovement: true,
                controlAttack: controlAttack);
            return enemy;
        }

        private static OSProjectile CreateHeadProjectilePrefab(Transform parent)
        {
            var target = new GameObject(
                "HeadProjectilePrefab",
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(OSProjectile));
            target.transform.SetParent(parent, false);
            target.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            target.GetComponent<CircleCollider2D>().isTrigger = true;
            target.SetActive(false);
            return target.GetComponent<OSProjectile>();
        }

        private static OSControlProjectile CreateControlProjectilePrefab(Transform parent)
        {
            var target = new GameObject(
                "ControlProjectilePrefab",
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(OSControlProjectile));
            target.transform.SetParent(parent, false);
            target.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            target.GetComponent<CircleCollider2D>().isTrigger = true;
            target.SetActive(false);
            return target.GetComponent<OSControlProjectile>();
        }

        private static OSEnemyController CreateEnemyPrefab(
            Transform parent,
            OSEnemyRegistry registry,
            OSGameSessionController session,
            Transform head)
        {
            var target = new GameObject(
                "EnemyPrefab",
                typeof(SpriteRenderer),
                typeof(Rigidbody2D),
                typeof(CircleCollider2D),
                typeof(OSEnemyController));
            target.transform.SetParent(parent, false);
            target.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var collider = target.GetComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.25f;
            var enemy = target.GetComponent<OSEnemyController>();
            enemy.ConfigureForTesting(registry, session, head, 18f, 0f, 0f, 1f);
            target.SetActive(false);
            return enemy;
        }

        private static LineRenderer CreateLineView(Transform parent, string name)
        {
            var target = new GameObject(name, typeof(LineRenderer));
            target.transform.SetParent(parent, false);
            return target.GetComponent<LineRenderer>();
        }

        private static T FindRented<T>(Transform root) where T : OSPoolableBehaviour
        {
            var instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (var index = 0; index < instances.Length; index++)
            {
                if (instances[index].IsRented && instances[index].transform.IsChildOf(root))
                {
                    return instances[index];
                }
            }

            return null;
        }

        private static OSDamageEvent HeadDamage(int attackId, Vector2 hitPosition)
        {
            return new OSDamageEvent(
                attackId,
                0,
                500 + attackId,
                1,
                OSTargetKind.PlayerHead,
                8f,
                hitPosition);
        }

        private static OSDamageEvent BodyDamage(int attackId, OSBodySegmentRuntime target)
        {
            return new OSDamageEvent(
                attackId,
                0,
                500 + attackId,
                target.StableId,
                OSTargetKind.PlayerBody,
                8f,
                target.View.transform.position);
        }

        private T Track<T>(T target) where T : Object
        {
            _created.Add(target);
            return target;
        }

        private readonly struct RoleRig
        {
            public RoleRig(
                GameObject root,
                OSGameSessionController session,
                Transform head,
                OSBodyChain chain,
                OSBodyRoleRegistry roles,
                OSEnemyRegistry enemies,
                OSPoolRegistry pool)
            {
                Root = root;
                Session = session;
                Head = head;
                Chain = chain;
                Roles = roles;
                Enemies = enemies;
                Pool = pool;
            }

            public GameObject Root { get; }
            public OSGameSessionController Session { get; }
            public Transform Head { get; }
            public OSBodyChain Chain { get; }
            public OSBodyRoleRegistry Roles { get; }
            public OSEnemyRegistry Enemies { get; }
            public OSPoolRegistry Pool { get; }
        }
    }
}
