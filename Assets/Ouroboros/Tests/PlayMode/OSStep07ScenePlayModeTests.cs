using System.Collections;
using NUnit.Framework;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep07ScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator GameScene_AutoWeaponKillsChaserAndUsesProjectileCollisionContract()
        {
            var load = SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            var pool = Object.FindAnyObjectByType<OSPoolRegistry>();
            var registry = Object.FindAnyObjectByType<OSEnemyRegistry>();
            var weapon = Object.FindAnyObjectByType<OSHeadWeapon>();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var projectileLayer = LayerMask.NameToLayer("PlayerProjectile");
            var enemyHurtboxLayer = LayerMask.NameToLayer("EnemyHurtbox");
            var worldBlockerLayer = LayerMask.NameToLayer("WorldBlocker");

            Assert.That(pool, Is.Not.Null);
            Assert.That(registry, Is.Not.Null);
            Assert.That(weapon, Is.Not.Null);
            Assert.That(session, Is.Not.Null);
            Assert.That(pool.GetCapacity("head_projectile"), Is.EqualTo(120));
            Assert.That(Physics2D.GetIgnoreLayerCollision(projectileLayer, enemyHurtboxLayer), Is.False);
            Assert.That(Physics2D.GetIgnoreLayerCollision(projectileLayer, worldBlockerLayer), Is.False);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            var head = Object.FindAnyObjectByType<OSPlayerController>().transform;
            var enemy = pool.Rent("enemy_chaser", head.position + Vector3.right * 3f, Quaternion.identity);
            Assert.That(enemy.IsAccepted, Is.True);

            var timeout = 5f;
            while (weapon.DefeatsConfirmed == 0 && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(weapon.ShotsFired, Is.GreaterThanOrEqualTo(1));
            Assert.That(weapon.HitsConfirmed, Is.GreaterThanOrEqualTo(1));
            Assert.That(weapon.DefeatsConfirmed, Is.GreaterThanOrEqualTo(1));
            Assert.That(registry.Count, Is.LessThanOrEqualTo(25));
        }
    }
}
