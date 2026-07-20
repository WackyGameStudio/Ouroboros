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
    public sealed class OSStep14ScenePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameScene_BossPoolHudSummaryAndResultControlsAreConnected()
        {
            yield return LoadGame();

            var pools = Object.FindAnyObjectByType<OSPoolRegistry>();
            var encounter = Object.FindAnyObjectByType<OSBossEncounterController>();
            var summary = Object.FindAnyObjectByType<OSRunSummaryController>();
            var bossPresenter = Object.FindAnyObjectByType<OSBossPresenter>(FindObjectsInactive.Include);
            var resultPanel = Object.FindAnyObjectByType<OSResultPanel>(FindObjectsInactive.Include);
            var canvas = GameObject.Find("Canvas")?.transform;

            Assert.That(pools.GetCapacity("boss_swarm_core"), Is.EqualTo(1));
            Assert.That(encounter, Is.Not.Null);
            Assert.That(encounter.TimeLimitSeconds, Is.EqualTo(150f));
            Assert.That(summary, Is.Not.Null);
            Assert.That(bossPresenter, Is.Not.Null);
            Assert.That(resultPanel, Is.Not.Null);
            Assert.That(canvas?.Find("BossStatusLabel"), Is.Not.Null);
            Assert.That(canvas?.Find("BossPatternLabel"), Is.Not.Null);
            Assert.That(canvas?.Find("ResultPanel/RestartButton"), Is.Not.Null);
            Assert.That(canvas?.Find("ResultPanel/MainMenuButton"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Timeline_WarnsAtNineMinutesAndSpawnsBossOnceAtTenMinutes()
        {
            yield return LoadGameAndEnterCombat();
            var director = Object.FindAnyObjectByType<OSWaveDirector>();
            var encounter = Object.FindAnyObjectByType<OSBossEncounterController>();

            director.AdvanceForTesting(540f, false);
            Assert.That(director.BossWarningCount, Is.EqualTo(1));
            Assert.That(encounter.BossSpawnCount, Is.Zero);

            director.AdvanceForTesting(60f, false);
            Assert.That(encounter.BossSpawnCount, Is.EqualTo(1));
            Assert.That(encounter.IsBossActive, Is.True);
            Assert.That(encounter.BossHealth, Is.EqualTo(6000f));
            Assert.That(director.DeferredBossEventCount, Is.Zero);

            director.AdvanceForTesting(1f, false);
            Assert.That(encounter.BossSpawnCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BossSelectionPause_FreezesPatternAndOneHundredFiftySecondLimit()
        {
            yield return LoadGameAndEnterCombat();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var encounter = Object.FindAnyObjectByType<OSBossEncounterController>();
            Assert.That(encounter.RequestBossSpawn().IsAccepted, Is.True);
            encounter.BossController.BeginPatternForTesting(OSBossPattern.FanProjectiles);
            encounter.AdvanceForTesting(0.1f);
            var elapsed = encounter.EncounterElapsedSeconds;
            var telegraph = encounter.TelegraphRemaining;

            Assert.That(session.QueueSelection(OSSelectionKind.LevelUp).IsAccepted, Is.True);
            Assert.That(session.ProcessPendingSelection().IsAccepted, Is.True);
            yield return new WaitForSecondsRealtime(0.08f);

            Assert.That(encounter.EncounterElapsedSeconds, Is.EqualTo(elapsed).Within(0.001f));
            Assert.That(encounter.TelegraphRemaining, Is.EqualTo(telegraph).Within(0.001f));
            Assert.That(encounter.ActivePattern, Is.EqualTo(OSBossPattern.FanProjectiles));
        }

        [UnityTest]
        public IEnumerator BossShieldAndControl_SeparateHealthAndDoNotCancelCasting()
        {
            yield return LoadGameAndEnterCombat();
            var encounter = Object.FindAnyObjectByType<OSBossEncounterController>();
            Assert.That(encounter.RequestBossSpawn().IsAccepted, Is.True);
            var enemy = encounter.BossEnemy;
            var boss = encounter.BossController;

            boss.ActivateShieldForTesting();
            Assert.That(enemy.TryApplyDamage(250f).IsAccepted, Is.True);
            Assert.That(boss.ShieldHealth, Is.EqualTo(350f));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(6000f));
            Assert.That(enemy.TryApplyDamage(500f).IsAccepted, Is.True);
            Assert.That(boss.ShieldHealth, Is.Zero);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(5850f));

            Assert.That(enemy.ApplyControl(0.5f).IsAccepted, Is.True);
            boss.BeginPatternForTesting(OSBossPattern.FanProjectiles);
            boss.SimulateStep(0.61f);
            Assert.That(boss.ActivePattern, Is.EqualTo(OSBossPattern.None));
            Assert.That(boss.FanCastCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BossSummon_RespectsNormalEnemyHardCapWhileBossUsesSeparateSlot()
        {
            yield return LoadGameAndEnterCombat();
            var registry = Object.FindAnyObjectByType<OSEnemyRegistry>();
            var pools = Object.FindAnyObjectByType<OSPoolRegistry>();
            var encounter = Object.FindAnyObjectByType<OSBossEncounterController>();

            for (var index = registry.NormalEnemyCount; index < 180; index++)
            {
                var rent = pools.Rent("enemy_chaser", new Vector3(10f, 8f), Quaternion.identity);
                Assert.That(rent.IsAccepted, Is.True, $"normal enemy {index}");
            }

            Assert.That(encounter.RequestBossSpawn().IsAccepted, Is.True);
            Assert.That(registry.NormalEnemyCount, Is.EqualTo(180));
            Assert.That(registry.Count, Is.EqualTo(181));
            encounter.BossController.BeginPatternForTesting(OSBossPattern.SwarmSummon);
            encounter.BossController.ResolvePatternForTesting();
            Assert.That(registry.NormalEnemyCount, Is.EqualTo(180));
            Assert.That(registry.Count, Is.EqualTo(181));
        }

        [UnityTest]
        public IEnumerator SimultaneousPlayerAndBossDeath_PlayerDamageWinsTheTerminalState()
        {
            yield return LoadGameAndEnterCombat();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var encounter = Object.FindAnyObjectByType<OSBossEncounterController>();
            var resolver = Object.FindAnyObjectByType<OSPlayerCombatResolver>();
            var headTarget = FindHeadTarget();
            Assert.That(encounter.RequestBossSpawn().IsAccepted, Is.True);
            var boss = encounter.BossEnemy;

            Assert.That(resolver.EnqueueDamage(new OSDamageEvent(
                999001,
                resolver.CombatTick,
                999,
                headTarget.RuntimeId,
                OSTargetKind.PlayerHead,
                9999f,
                headTarget.transform.position)).IsAccepted, Is.True);
            Assert.That(boss.TryApplyDamage(99999f).IsAccepted, Is.True);
            resolver.ProcessPendingForTesting();
            encounter.AdvanceForTesting(0.01f);

            Assert.That(session.State, Is.EqualTo(OSSessionState.Dead));
            Assert.That(session.ResultKind, Is.EqualTo(OSSessionResultKind.PlayerDefeated));
            Assert.That(encounter.BossDefeatCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BossDeathAndTimeout_BuildDistinctResultsAndDisableCombatInput()
        {
            yield return LoadGameAndEnterCombatWithRoles();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var encounter = Object.FindAnyObjectByType<OSBossEncounterController>();
            var summary = Object.FindAnyObjectByType<OSRunSummaryController>();
            var router = Object.FindAnyObjectByType<OSInputRouter>();
            var resultLabel = GameObject.Find("Canvas")?.transform.Find("ResultPanel/Label")
                ?.GetComponent<TMP_Text>();

            Assert.That(encounter.RequestBossSpawn().IsAccepted, Is.True);
            var boss = encounter.BossEnemy;
            Assert.That(boss.TryApplyDamage(99999f).IsAccepted, Is.True);
            encounter.AdvanceForTesting(0.01f);
            yield return null;

            Assert.That(session.State, Is.EqualTo(OSSessionState.Cleared));
            Assert.That(session.ResultKind, Is.EqualTo(OSSessionResultKind.BossDefeated));
            Assert.That(encounter.BossDefeatCount, Is.EqualTo(1));
            Assert.That(summary.HasSummary, Is.True);
            Assert.That(summary.Summary.AcquiredBodyCount, Is.EqualTo(2));
            Assert.That(summary.Summary.TotalKills, Is.GreaterThanOrEqualTo(1));
            Assert.That(router.CurrentMode, Is.EqualTo(OSInputMode.UI));
            Assert.That(resultLabel.text, Does.Contain("RUN CLEARED"));

            Assert.That(session.ConfirmResult().IsAccepted, Is.True);
            Assert.That(session.RestartSession().IsAccepted, Is.True);
            CompleteStartSelections(session);
            Assert.That(encounter.RequestBossSpawn().IsAccepted, Is.True);
            encounter.AdvanceForTesting(149.9f);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(encounter.BossTimeoutCount, Is.Zero);
            encounter.AdvanceForTesting(0.2f);
            yield return null;

            Assert.That(session.State, Is.EqualTo(OSSessionState.Dead));
            Assert.That(session.ResultKind, Is.EqualTo(OSSessionResultKind.BossTimeout));
            Assert.That(summary.Summary.ResultKind, Is.EqualTo(OSSessionResultKind.BossTimeout));
            Assert.That(resultLabel.text, Does.Contain("150s TIMEOUT"));
        }

        [UnityTest]
        public IEnumerator RestartTenTimes_LeavesNoBossPoolEventOrClockLeak()
        {
            yield return LoadGameAndEnterCombat();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var encounter = Object.FindAnyObjectByType<OSBossEncounterController>();
            var director = Object.FindAnyObjectByType<OSWaveDirector>();
            var pools = Object.FindAnyObjectByType<OSPoolRegistry>();
            var summary = Object.FindAnyObjectByType<OSRunSummaryController>();

            for (var iteration = 0; iteration < 10; iteration++)
            {
                Assert.That(encounter.RequestBossSpawn().IsAccepted, Is.True, $"spawn {iteration}");
                Assert.That(session.RequestDeath().IsAccepted, Is.True, $"death {iteration}");
                Assert.That(session.ConfirmResult().IsAccepted, Is.True, $"result {iteration}");
                Assert.That(session.RestartSession().IsAccepted, Is.True, $"restart {iteration}");
                CompleteStartSelections(session);
                Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
                Assert.That(encounter.IsBossActive, Is.False);
                Assert.That(encounter.BossSpawnCount, Is.Zero);
                Assert.That(director.ElapsedSeconds, Is.Zero);
                Assert.That(pools.GetActiveCount("boss_swarm_core"), Is.Zero);
                Assert.That(summary.HasSummary, Is.False);
            }
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;
        }

        private static IEnumerator LoadGameAndEnterCombat()
        {
            yield return LoadGame();
            CompleteStartSelections(Object.FindAnyObjectByType<OSGameSessionController>());
        }

        private static IEnumerator LoadGameAndEnterCombatWithRoles()
        {
            yield return LoadGame();
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
        }

        private static void CompleteStartSelections(OSGameSessionController session)
        {
            Assert.That(session.State, Is.EqualTo(OSSessionState.StartBodySelection));
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
        }

        private static OSCombatTargetIdentity FindHeadTarget()
        {
            var targets = Object.FindObjectsByType<OSCombatTargetIdentity>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (var index = 0; index < targets.Length; index++)
            {
                if (targets[index].TargetKind == OSTargetKind.PlayerHead)
                {
                    return targets[index];
                }
            }

            Assert.Fail("Player head target identity is missing.");
            return null;
        }
    }
}
