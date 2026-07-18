using System.Collections;
using NUnit.Framework;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep15ArenaCollisionPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            var blocker = GameObject.Find("Step15_1_TestBlocker");
            if (blocker != null)
            {
                Object.DestroyImmediate(blocker);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ExpandedArena_HasSevenMeaningfulObstaclesAndNoLegacyDisplayObjects()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var player = Object.FindAnyObjectByType<OSPlayerController>();
            var follower = Object.FindAnyObjectByType<OSCameraFollower>();
            var world = GameObject.Find("World")?.transform;
            var obstacles = world?.Find("Obstacles");
            var names = new[]
            {
                "Obstacle_Wide",
                "Obstacle_Tall",
                "Obstacle_Block",
                "Obstacle_West",
                "Obstacle_East",
                "Obstacle_South",
                "Obstacle_North"
            };

            Assert.That(player, Is.Not.Null);
            Assert.That(follower, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            Assert.That(obstacles, Is.Not.Null);
            Assert.That(player.WorldMin, Is.EqualTo(new Vector2(-24f, -15f)));
            Assert.That(player.WorldMax, Is.EqualTo(new Vector2(24f, 15f)));
            Assert.That(follower.WorldMin, Is.EqualTo(player.WorldMin));
            Assert.That(follower.WorldMax, Is.EqualTo(player.WorldMax));
            Assert.That(world.Find("Enemy_Chaser"), Is.Null);
            Assert.That(world.Find("Pickup"), Is.Null);

            var blockerLayer = LayerMask.NameToLayer("WorldBlocker");
            for (var index = 0; index < names.Length; index++)
            {
                var obstacle = obstacles.Find(names[index]);
                Assert.That(obstacle, Is.Not.Null, names[index]);
                Assert.That(obstacle.gameObject.layer, Is.EqualTo(blockerLayer), names[index]);
                Assert.That(obstacle.GetComponent<BoxCollider2D>(), Is.Not.Null, names[index]);
            }

            var bounds = obstacles.Find("WorldBounds");
            Assert.That(bounds, Is.Not.Null);
            Assert.That(bounds.Find("Boundary_Left").position.x, Is.EqualTo(-24.25f).Within(0.001f));
            Assert.That(bounds.Find("Boundary_Right").position.x, Is.EqualTo(24.25f).Within(0.001f));
            Assert.That(bounds.Find("Boundary_Top").position.y, Is.EqualTo(15.25f).Within(0.001f));
            Assert.That(bounds.Find("Boundary_Bottom").position.y, Is.EqualTo(-15.25f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator CollisionMatrix_BlocksEnemyBodiesAndBothProjectileFactions()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var worldBlocker = LayerMask.NameToLayer("WorldBlocker");
            var enemyBody = LayerMask.NameToLayer("EnemyBody");
            var enemyHurtbox = LayerMask.NameToLayer("EnemyHurtbox");
            var playerHeadHurtbox = LayerMask.NameToLayer("PlayerHeadHurtbox");
            var playerBodyHurtbox = LayerMask.NameToLayer("PlayerBodyHurtbox");
            var playerProjectile = LayerMask.NameToLayer("PlayerProjectile");
            var enemyProjectile = LayerMask.NameToLayer("EnemyProjectile");

            Assert.That(enemyProjectile, Is.GreaterThanOrEqualTo(8));
            Assert.That(Physics2D.GetIgnoreLayerCollision(enemyBody, worldBlocker), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(playerProjectile, worldBlocker), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(playerProjectile, enemyHurtbox), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(enemyProjectile, worldBlocker), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(enemyProjectile, playerHeadHurtbox), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(enemyProjectile, playerBodyHurtbox), Is.False);
        }

        [UnityTest]
        public IEnumerator EnemyCast_StopsBeforeWorldBlockerEvenAcrossLargeSimulationStep()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var registry = Object.FindAnyObjectByType<OSEnemyRegistry>();
            var pools = Object.FindAnyObjectByType<OSPoolRegistry>();
            var head = Object.FindAnyObjectByType<OSPlayerController>().transform;
            CompleteStartSelections(session);
            Time.timeScale = 0f;
            head.position = new Vector3(4f, 0f, 0f);

            var blocker = CreateBlocker();
            var rent = pools.Rent("enemy_chaser", new Vector3(-2f, 0f, 0f), Quaternion.identity);
            Assert.That(rent.IsAccepted, Is.True);
            var enemy = (OSEnemyController)rent.Payload;
            enemy.ConfigureForTesting(registry, session, head);

            enemy.SimulateStep(2f);
            Physics2D.SyncTransforms();

            Assert.That(enemy.Position.x, Is.LessThan(-0.82f));
            Assert.That(enemy.GetComponent<CircleCollider2D>().Distance(blocker).isOverlapped, Is.False);
        }

        [UnityTest]
        public IEnumerator ProjectileCasts_ReturnHeadControlAndEnemyShotsAtFirstWorldBlocker()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var pools = Object.FindAnyObjectByType<OSPoolRegistry>();
            CompleteStartSelections(session);
            Time.timeScale = 0f;
            CreateBlocker();

            var headRent = pools.Rent("head_projectile", new Vector3(-2f, 0f, 0f), Quaternion.identity);
            Assert.That(headRent.IsAccepted, Is.True);
            var headProjectile = (OSProjectile)headRent.Payload;
            var headAttack = pools.NextAttackEventId();
            Assert.That(headProjectile.Launch(
                headAttack.Payload,
                1,
                Vector2.right,
                10f,
                20f,
                0,
                null).IsAccepted, Is.True);
            headProjectile.SimulateStep(1f);
            Assert.That(pools.GetActiveCount("head_projectile"), Is.Zero);

            var controlRent = pools.Rent(
                "body_control_projectile",
                new Vector3(-2f, 1.2f, 0f),
                Quaternion.identity);
            Assert.That(controlRent.IsAccepted, Is.True);
            var controlProjectile = (OSControlProjectile)controlRent.Payload;
            var controlAttack = pools.NextAttackEventId();
            Assert.That(controlProjectile.Launch(
                controlAttack.Payload,
                1,
                Vector2.right,
                0f,
                20f,
                1f,
                0.5f,
                null).IsAccepted, Is.True);
            controlProjectile.SimulateStep(1f);
            Assert.That(pools.GetActiveCount("body_control_projectile"), Is.Zero);

            var enemyRent = pools.Rent(
                "enemy_projectile",
                new Vector3(2f, -1.2f, 0f),
                Quaternion.identity);
            Assert.That(enemyRent.IsAccepted, Is.True);
            var enemyProjectile = (OSEnemyProjectile)enemyRent.Payload;
            var enemyAttack = pools.NextAttackEventId();
            Assert.That(enemyProjectile.Launch(
                enemyAttack.Payload,
                1,
                Vector2.left,
                8f,
                20f).IsAccepted, Is.True);
            enemyProjectile.SimulateStep(1f);
            Assert.That(pools.GetActiveCount("enemy_projectile"), Is.Zero);
        }

        private static BoxCollider2D CreateBlocker()
        {
            var blocker = new GameObject("Step15_1_TestBlocker", typeof(BoxCollider2D));
            blocker.layer = LayerMask.NameToLayer("WorldBlocker");
            var collider = blocker.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 6f);
            collider.isTrigger = false;
            Physics2D.SyncTransforms();
            return collider;
        }

        private static void CompleteStartSelections(OSGameSessionController session)
        {
            Assert.That(session, Is.Not.Null);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.IsSimulationRunning, Is.True);
        }
    }
}
