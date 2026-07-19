using System.Collections;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using Ouroboros.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep13ScenePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameScene_WavePoolsHudAndOutsideCameraSpawnerAreConnected()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var director = Object.FindAnyObjectByType<OSWaveDirector>();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var registry = Object.FindAnyObjectByType<OSEnemyRegistry>();
            var pools = Object.FindAnyObjectByType<OSPoolRegistry>();
            var presenter = Object.FindAnyObjectByType<OSWavePresenter>(FindObjectsInactive.Include);
            var debugSpawner = Object.FindAnyObjectByType<OSEnemyDebugSpawner>(FindObjectsInactive.Include);

            Assert.That(director, Is.Not.Null);
            Assert.That(session, Is.Not.Null);
            Assert.That(registry, Is.Not.Null);
            Assert.That(pools, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(debugSpawner, Is.Not.Null);
            Assert.That(debugSpawner.enabled, Is.False);
            Assert.That(pools.GetCapacity("enemy_chaser"), Is.EqualTo(200));
            Assert.That(pools.GetCapacity("enemy_charger"), Is.EqualTo(32));
            Assert.That(pools.GetCapacity("enemy_shooter"), Is.EqualTo(32));
            Assert.That(pools.GetCapacity("enemy_splitter"), Is.EqualTo(32));
            Assert.That(pools.GetCapacity("enemy_splitter_spawn"), Is.EqualTo(64));
            Assert.That(pools.GetCapacity("enemy_elite_accelerator"), Is.EqualTo(4));
            Assert.That(pools.GetCapacity("enemy_projectile"), Is.EqualTo(64));
            Assert.That(director.ActiveEnemyLimit, Is.EqualTo(180));
            var canvas = GameObject.Find("Canvas")?.transform;
            var waveLabel = canvas?.Find("WaveStatusLabel");
            Assert.That(waveLabel, Is.Not.Null);
            Assert.That(canvas?.Find("WaveEventLabel"), Is.Not.Null);
            Assert.That(((RectTransform)waveLabel).anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));

            CompleteStartSelections(session);
            yield return new WaitForSeconds(2.7f);

            Assert.That(director.ElapsedSeconds, Is.GreaterThan(2.5f));
            Assert.That(registry.Count, Is.GreaterThan(0));
            Assert.That(registry.Count, Is.LessThanOrEqualTo(director.CurrentTargetActiveEnemies));
            Assert.That(director.IsValidSpawnPosition(director.LastSpawnPosition), Is.True);
            var viewport = Camera.main.WorldToViewportPoint(director.LastSpawnPosition);
            Assert.That(viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f,
                Is.True);
        }

        [UnityTest]
        public IEnumerator Timeline_PausesForSelectionTriggersTwoElitesAndWarningThenRestartResets()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var director = Object.FindAnyObjectByType<OSWaveDirector>();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var registry = Object.FindAnyObjectByType<OSEnemyRegistry>();
            CompleteStartSelections(session);

            var queue = session.QueueSelection(OSSelectionKind.LevelUp);
            Assert.That(queue.IsAccepted, Is.True);
            Assert.That(session.ProcessPendingSelection().IsAccepted, Is.True);
            var pausedAt = director.ElapsedSeconds;
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(director.ElapsedSeconds, Is.EqualTo(pausedAt).Within(0.001f));
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);

            director.AdvanceForTesting(180f, false);
            Assert.That(director.EliteSpawnCount, Is.EqualTo(1));
            director.AdvanceForTesting(180f, false);
            Assert.That(director.EliteSpawnCount, Is.EqualTo(2));
            director.AdvanceForTesting(180f, false);
            Assert.That(director.BossWarningCount, Is.EqualTo(1));
            Assert.That(director.DeferredBossEventCount, Is.Zero);

            Assert.That(session.RequestDeath().IsAccepted, Is.True);
            Assert.That(session.ConfirmResult().IsAccepted, Is.True);
            Assert.That(session.RestartSession().IsAccepted, Is.True);
            Assert.That(director.ElapsedSeconds, Is.Zero);
            Assert.That(director.EliteSpawnCount, Is.Zero);
            Assert.That(director.BossWarningCount, Is.Zero);
            Assert.That(registry.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Archetypes_TelegraphShootSplitAndApplyNonStackingEliteAura()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var registry = Object.FindAnyObjectByType<OSEnemyRegistry>();
            var pools = Object.FindAnyObjectByType<OSPoolRegistry>();
            var head = Object.FindAnyObjectByType<OSPlayerController>().transform;
            CompleteStartSelections(session);

            var charger = RentEnemy(pools, "enemy_charger", head.position + Vector3.right * 5f);
            charger.SimulateStep(0.81f);
            Assert.That(charger.BehaviorState, Is.EqualTo(OSEnemyBehaviorState.Telegraph));
            var telegraphTimer = charger.BehaviorTimer;
            Assert.That(charger.ApplyControl(0.5f).IsAccepted, Is.True);
            charger.SimulateStep(0.25f);
            Assert.That(charger.BehaviorState, Is.EqualTo(OSEnemyBehaviorState.Telegraph));
            Assert.That(charger.BehaviorTimer, Is.EqualTo(telegraphTimer).Within(0.001f));
            charger.SimulateStep(0.3f);
            charger.SimulateStep(0.65f);
            Assert.That(charger.BehaviorState, Is.EqualTo(OSEnemyBehaviorState.Charge));

            var projectileBefore = pools.GetActiveCount("enemy_projectile");
            var shooter = RentEnemy(pools, "enemy_shooter", head.position + Vector3.up * 6f);
            shooter.SimulateStep(0.36f);
            Assert.That(shooter.BehaviorState, Is.EqualTo(OSEnemyBehaviorState.Telegraph));
            shooter.SimulateStep(0.45f);
            Assert.That(pools.GetActiveCount("enemy_projectile"), Is.EqualTo(projectileBefore + 1));

            var smallBefore = pools.GetActiveCount("enemy_splitter_spawn");
            var splitter = RentEnemy(pools, "enemy_splitter", head.position + Vector3.left * 7f);
            Assert.That(splitter.TryApplyDamage(99999f).IsAccepted, Is.True);
            Assert.That(pools.GetActiveCount("enemy_splitter_spawn"), Is.EqualTo(smallBefore + 2));

            var elite = RentEnemy(pools, "enemy_elite_accelerator", head.position + Vector3.down * 8f);
            var chaser = RentEnemy(pools, "enemy_chaser", elite.Position + Vector2.right);
            var secondElite = RentEnemy(pools, "enemy_elite_accelerator", elite.Position + Vector2.left);
            var chaserRenderer = chaser.GetComponent<SpriteRenderer>();
            var colorBeforeAura = chaserRenderer.color;
            chaser.SimulateStep(0.01f);
            Assert.That(chaser.IsAuraAccelerated, Is.True);
            Assert.That(chaserRenderer.color, Is.Not.EqualTo(colorBeforeAura));
            Assert.That(chaser.EffectiveMoveSpeed, Is.EqualTo(chaser.MoveSpeed * 1.2f).Within(0.001f));
            Assert.That(chaser.EffectiveAttackInterval,
                Is.EqualTo(chaser.AttackInterval * 0.9f).Within(0.001f));
            Assert.That(secondElite.IsAuraAccelerated, Is.False);
            Assert.That(registry.Count, Is.LessThanOrEqualTo(180));
        }

        [UnityTest]
        public IEnumerator HardCap_PreservesDeferredSpawnTicket()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var director = Object.FindAnyObjectByType<OSWaveDirector>();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var registry = Object.FindAnyObjectByType<OSEnemyRegistry>();
            var pools = Object.FindAnyObjectByType<OSPoolRegistry>();
            CompleteStartSelections(session);

            for (var index = registry.Count; index < 180; index++)
            {
                var rent = pools.Rent("enemy_chaser", new Vector3(10f, 8f, 0f), Quaternion.identity);
                Assert.That(rent.IsAccepted, Is.True, $"rent {index}");
            }

            Assert.That(registry.Count, Is.EqualTo(180));
            director.AdvanceForTesting(3f, true);
            Assert.That(registry.Count, Is.EqualTo(180));
            Assert.That(director.DeferredSpawnTicketCount, Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator AcceleratedG1_ZeroToSevenMinutesKeepsFourArchetypesAndTwoElitesOnline()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var director = Object.FindAnyObjectByType<OSWaveDirector>();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var registry = Object.FindAnyObjectByType<OSEnemyRegistry>();
            CompleteStartSelections(session);

            long activeSamples = 0;
            var peakActive = 0;
            for (var second = 0; second < 420; second++)
            {
                director.AdvanceForTesting(1f, true);
                activeSamples += registry.Count;
                peakActive = Mathf.Max(peakActive, registry.Count);
            }

            var chaser = false;
            var charger = false;
            var shooter = false;
            var splitter = false;
            for (var index = 0; index < registry.Count; index++)
            {
                var enemy = registry.GetAt(index);
                chaser |= enemy.Archetype == OSEnemyArchetype.Chaser;
                charger |= enemy.Archetype == OSEnemyArchetype.Charger;
                shooter |= enemy.Archetype == OSEnemyArchetype.Shooter;
                splitter |= enemy.Archetype == OSEnemyArchetype.Splitter;
            }

            var averageActive = activeSamples / 420f;
            TestContext.WriteLine(
                $"Step13 G1 accelerated 0-7m: average active {averageActive:F1}, peak {peakActive}.");
            Assert.That(director.ElapsedSeconds, Is.EqualTo(420f).Within(0.01f));
            Assert.That(director.EliteSpawnCount, Is.EqualTo(2));
            Assert.That(chaser && charger && shooter && splitter, Is.True);
            Assert.That(averageActive, Is.GreaterThan(20f));
            Assert.That(peakActive, Is.LessThanOrEqualTo(180));
        }

        private static OSEnemyController RentEnemy(OSPoolRegistry pools, string key, Vector3 position)
        {
            var rent = pools.Rent(key, position, Quaternion.identity);
            Assert.That(rent.IsAccepted, Is.True, key);
            Assert.That(rent.Payload, Is.TypeOf<OSEnemyController>());
            return (OSEnemyController)rent.Payload;
        }

        private static void CompleteStartSelections(OSGameSessionController session)
        {
            Assert.That(session.State, Is.EqualTo(OSSessionState.StartBodySelection));
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
        }
    }
}
