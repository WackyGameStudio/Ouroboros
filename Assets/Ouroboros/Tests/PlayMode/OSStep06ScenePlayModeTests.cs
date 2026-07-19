using System.Collections;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep06ScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator GameScene_PrewarmAndStep13WaveOverrideRespectCollisionContract()
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
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var debugSpawner = Object.FindAnyObjectByType<OSEnemyDebugSpawner>(FindObjectsInactive.Include);
            var enemyBodyLayer = LayerMask.NameToLayer("EnemyBody");
            var playerHeadLayer = LayerMask.NameToLayer("PlayerHeadSolid");
            var playerBodyLayer = LayerMask.NameToLayer("PlayerBodyHurtbox");
            var worldBlockerLayer = LayerMask.NameToLayer("WorldBlocker");

            Assert.That(pool, Is.Not.Null);
            Assert.That(registry, Is.Not.Null);
            Assert.That(pool.GetCapacity("enemy_chaser"), Is.EqualTo(200));
            Assert.That(pool.GetActiveCount("enemy_chaser"), Is.Zero);
            Assert.That(registry.Count, Is.Zero);
            Assert.That(debugSpawner, Is.Not.Null);
            Assert.That(debugSpawner.enabled, Is.False);

            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            yield return new WaitForSeconds(2.7f);

            var firstEnemy = registry.GetAt(0);
            Assert.That(firstEnemy, Is.Not.Null);
            Assert.That(firstEnemy.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Kinematic));
            Assert.That(Physics2D.GetIgnoreLayerCollision(enemyBodyLayer, enemyBodyLayer), Is.True);
            Assert.That(Physics2D.GetIgnoreLayerCollision(enemyBodyLayer, playerHeadLayer), Is.True);
            Assert.That(Physics2D.GetIgnoreLayerCollision(enemyBodyLayer, playerBodyLayer), Is.True);
            Assert.That(Physics2D.GetIgnoreLayerCollision(enemyBodyLayer, worldBlockerLayer), Is.False);

            var weapon = Object.FindAnyObjectByType<OSHeadWeapon>();
            Assert.That(weapon, Is.Not.Null);
            weapon.enabled = false;
            var countBeforeDeath = registry.Count;
            Assert.That(firstEnemy.TryApplyDamage(999f).IsAccepted, Is.True);
            Assert.That(registry.Count, Is.EqualTo(countBeforeDeath - 1));

            yield return new WaitForSeconds(2.7f);

            Assert.That(registry.Count, Is.GreaterThanOrEqualTo(countBeforeDeath));
            Assert.That(registry.Count, Is.LessThanOrEqualTo(25));
        }
    }
}
