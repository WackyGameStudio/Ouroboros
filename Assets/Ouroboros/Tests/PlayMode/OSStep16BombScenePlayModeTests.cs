using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using Ouroboros.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep16BombScenePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameScene_BombDataInputVisualsHudAndUpgradesAreConnected()
        {
            yield return LoadGame();

            var bomb = Object.FindAnyObjectByType<OSBombController>(FindObjectsInactive.Include);
            var view = Object.FindAnyObjectByType<OSBombView>(FindObjectsInactive.Include);
            var router = Object.FindAnyObjectByType<OSInputRouter>(FindObjectsInactive.Include);
            var hud = Object.FindAnyObjectByType<OSCombatHudPresenter>(FindObjectsInactive.Include);
            var catalog = Resources.FindObjectsOfTypeAll<OSUpgradeCatalog>()
                .FirstOrDefault(candidate => candidate.name == "OSUpgradeCatalog");
            var waves = Resources.FindObjectsOfTypeAll<OSWaveScheduleData>()
                .FirstOrDefault(candidate => candidate.name == "OSWaveSchedule");
            var ring = GameObject.Find("BombRing")?.GetComponent<LineRenderer>();
            var fill = GameObject.Find("BombExplosionFill")?.GetComponent<SpriteRenderer>();

            Assert.That(bomb, Is.Not.Null);
            Assert.That(view, Is.Not.Null);
            Assert.That(router, Is.Not.Null);
            Assert.That(router.IsConfigured, Is.True);
            Assert.That(hud, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(waves, Is.Not.Null);
            Assert.That(catalog.Entries.Count, Is.EqualTo(17));
            Assert.That(catalog.Entries.Any(entry =>
                entry.Id == "bomb_damage" &&
                entry.Operation == OSUpgradeOperation.AddBombDamageMultiplier), Is.True);
            Assert.That(catalog.Entries.Any(entry =>
                entry.Id == "bomb_cooldown" &&
                entry.Operation == OSUpgradeOperation.AddBombCooldownDelta), Is.True);
            Assert.That(bomb.MinimumBodyCount, Is.EqualTo(10));
            Assert.That(bomb.ConsumeRate, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(bomb.DrawDuration, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(bomb.GatherDuration, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(bomb.RadiusMultiplier, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(bomb.DamagePerBody, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(bomb.PredictDamage(1f, 10), Is.EqualTo(100f).Within(0.0001f));
            Assert.That(bomb.Cooldown, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(waves.SpawnDensityMultiplier, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(ring, Is.Not.Null);
            Assert.That(ring.sortingOrder, Is.EqualTo(190));
            Assert.That(fill, Is.Not.Null);
            Assert.That(fill.sortingOrder, Is.EqualTo(189));
        }

        [UnityTest]
        public IEnumerator Explosion_DamagesOverlappingHurtboxOnceThenFinishesPathGather()
        {
            yield return LoadGameAndEnterCombat();

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var bomb = Object.FindAnyObjectByType<OSBombController>();
            var pools = Object.FindAnyObjectByType<OSPoolRegistry>();
            var wave = Object.FindAnyObjectByType<OSWaveDirector>();
            var hud = Object.FindAnyObjectByType<OSCombatHudPresenter>();
            wave.enabled = false;
            Object.FindAnyObjectByType<OSHeadWeapon>().enabled = false;
            Object.FindAnyObjectByType<OSAttackBodyRole>().enabled = false;
            Object.FindAnyObjectByType<OSLaserBodyRole>().enabled = false;
            Object.FindAnyObjectByType<OSControlBodyRole>().enabled = false;
            Assert.That(chain.SetDebugSegmentCount(10).IsAccepted, Is.True);

            var snapshot = default(OSBombSnapshot);
            var resolution = default(OSBombResolution);
            var exploded = false;
            bomb.BombStarted += value => snapshot = value;
            bomb.BombExploded += value =>
            {
                resolution = value;
                exploded = true;
            };

            Assert.That(bomb.RequestBomb().IsAccepted, Is.True);
            Assert.That(chain.ActiveCount, Is.EqualTo(9));
            Assert.That(session.State, Is.EqualTo(OSSessionState.Bomb));
            Assert.That(GameObject.Find("BombRing").GetComponent<LineRenderer>().enabled, Is.True);
            hud.ForceRefreshForTesting();
            Assert.That(hud.ActionText, Does.Contain("BOMB DRAWING"));

            var rent = pools.Rent("enemy_chaser", snapshot.Center, Quaternion.identity);
            Assert.That(rent.IsAccepted, Is.True);
            var enemy = rent.Payload as OSEnemyController;
            Assert.That(enemy, Is.Not.Null);
            Assert.That(enemy.ApplyWaveHealthMultiplier(100f).IsAccepted, Is.True);
            var hurtboxLayer = LayerMask.NameToLayer("EnemyHurtbox");
            var hurtbox = enemy.GetComponentsInChildren<Collider2D>(true)
                .First(collider => collider.gameObject.layer == hurtboxLayer);
            var duplicate = hurtbox.gameObject.AddComponent<CircleCollider2D>();
            duplicate.isTrigger = true;
            duplicate.radius = Mathf.Max(0.05f, hurtbox.bounds.extents.x);
            duplicate.offset = hurtbox.offset;
            enemy.transform.position = snapshot.Center +
                                       (Vector2.right *
                                        (snapshot.Radius + duplicate.radius * 0.5f));
            enemy.enabled = false;
            Physics2D.SyncTransforms();
            Assert.That(
                Vector2.Distance(snapshot.Center, hurtbox.bounds.center),
                Is.GreaterThan(snapshot.Radius),
                "The enemy center should be outside while its hurtbox overlaps the explosion.");
            var healthBefore = enemy.CurrentHealth;

            for (var step = 0; step < 65 && !exploded; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(exploded, Is.True);
            Assert.That(resolution.HitCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(healthBefore - 100f).Within(0.001f),
                "Multiple colliders from one Runtime ID must still receive exactly one Bomb hit.");

            for (var step = 0; step < 35 && bomb.IsActive; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(bomb.IsActive, Is.False);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(chain.IsNaturalUnfoldActive, Is.True);
            Assert.That(GameObject.Find("BombRing").GetComponent<LineRenderer>().enabled, Is.False);
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;
        }

        private static IEnumerator LoadGameAndEnterCombat()
        {
            yield return LoadGame();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            yield return null;
        }
    }
}
