using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSLevelUpPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExperienceLevelUp_PausesCombatShowsThreeCardsAndCommitsExactlyOnce()
        {
            yield return LoadGameAndEnterCombat();

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var level = Object.FindAnyObjectByType<OSLevelUpController>();
            var headWeapon = Object.FindAnyObjectByType<OSHeadWeapon>();
            var panel = Object.FindAnyObjectByType<Ouroboros.UI.OSLevelUpPanel>(
                FindObjectsInactive.Include);

            var shotsBefore = headWeapon.ShotsFired;
            var result = level.AddExperience(15);
            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.Payload, Is.EqualTo(1));
            Assert.That(level.Level, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(OSSessionState.LevelUpSelection));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(panel.gameObject.activeInHierarchy, Is.True);

            var candidates = Enumerable.Range(0, OSUpgradeRunState.CandidateCount)
                .Select(level.GetCandidate)
                .ToArray();
            Assert.That(candidates.Select(candidate => candidate.Id).Distinct().Count(), Is.EqualTo(3));
            Assert.That(candidates.Select(candidate => candidate.Category), Does.Contain(OSUpgradeCategory.Firepower));
            Assert.That(candidates.Select(candidate => candidate.Category), Does.Contain(OSUpgradeCategory.Body));
            Assert.That(candidates.Select(candidate => candidate.Category), Does.Contain(OSUpgradeCategory.Survival));

            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(headWeapon.ShotsFired, Is.EqualTo(shotsBefore));

            var requestId = level.CandidateRequestId;
            var first = level.ConfirmCandidate(0);
            Assert.That(first.IsAccepted, Is.True);
            Assert.That(level.AppliedUpgradeCount, Is.EqualTo(1));
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            var duplicate = level.ConfirmCandidate(0);
            Assert.That(duplicate.IsAccepted, Is.False);
            Assert.That(level.CandidateRequestId, Is.Not.EqualTo(requestId));
            Assert.That(level.AppliedUpgradeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BodyRequestTakesPriorityAndMultiLevelOverflowIsResolvedSerially()
        {
            yield return LoadGameAndEnterCombat();

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            var level = Object.FindAnyObjectByType<OSLevelUpController>();

            Assert.That(session.QueueSelection(OSSelectionKind.BodyRole).IsAccepted, Is.True);
            var experience = level.AddExperience(40);
            Assert.That(experience.IsAccepted, Is.True);
            Assert.That(experience.Payload, Is.EqualTo(2));
            Assert.That(level.Level, Is.EqualTo(3));
            Assert.That(level.CurrentExperience, Is.EqualTo(7f).Within(0.001f));
            Assert.That(session.State, Is.EqualTo(OSSessionState.BodyRoleSelection));
            Assert.That(session.ActiveSelection?.Kind, Is.EqualTo(OSSelectionKind.BodyRole));

            Assert.That(growth.ConfirmRole(OSBodyRoleType.Laser).IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.LevelUpSelection));
            Assert.That(session.ActiveSelection?.Kind, Is.EqualTo(OSSelectionKind.LevelUp));

            Assert.That(level.ConfirmCandidate(0).IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.LevelUpSelection));
            Assert.That(level.ConfirmCandidate(0).IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(level.AppliedUpgradeCount, Is.EqualTo(2));
            Assert.That(session.PendingSelectionCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RuntimeModifiersApplyToCombatGrowthSurvivalAndCollectionSystems()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var health = Object.FindAnyObjectByType<OSPlayerHealth>();
            var player = Object.FindAnyObjectByType<OSPlayerController>();
            var weapon = Object.FindAnyObjectByType<OSHeadWeapon>();
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            var pickups = Object.FindAnyObjectByType<OSPickupSpawner>();
            var bodyDash = Object.FindAnyObjectByType<OSBodyDashController>();
            var attack = Object.FindAnyObjectByType<OSAttackBodyRole>();
            var laser = Object.FindAnyObjectByType<OSLaserBodyRole>();
            var shield = Object.FindAnyObjectByType<OSShieldBodyRole>();

            var baseHealth = health.MaxHealth;
            var baseMoveSpeed = player.MoveSpeed;
            var baseDamage = weapon.Damage;
            var baseFireInterval = weapon.FireInterval;
            var baseMagnet = pickups.MagnetRadius;
            var baseDashDistance = bodyDash.Distance;
            var baseDashCooldown = bodyDash.Cooldown;
            var baseDashRecovery = bodyDash.RecoveryDuration;
            var baseAttackInterval = attack.Interval;
            var baseLaserInterval = laser.Interval;
            var baseShieldRecharge = shield.RechargeDuration;

            var modifiers = new OSUpgradeModifiers(
                1.15f,
                0.12f,
                1,
                0.83f,
                0.01f,
                0.92f,
                1.15f,
                0.88f,
                -0.05f,
                1.2f,
                1.08f,
                1.25f,
                1.3f,
                1.1f,
                true);

            health.ApplyUpgradeModifiers(modifiers);
            player.ApplyUpgradeModifiers(modifiers);
            weapon.ApplyUpgradeModifiers(modifiers);
            growth.ApplyUpgradeModifiers(modifiers);
            pickups.ApplyUpgradeModifiers(modifiers);
            bodyDash.ApplyUpgradeModifiers(modifiers);
            attack.ApplyUpgradeModifiers(modifiers);
            laser.ApplyUpgradeModifiers(modifiers);
            shield.ApplyUpgradeModifiers(modifiers);

            Assert.That(health.MaxHealth, Is.EqualTo(baseHealth * 1.2f).Within(0.001f));
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth).Within(0.001f));
            Assert.That(health.HealMultiplier, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(player.MoveSpeed, Is.EqualTo(Mathf.Min(7.5f, baseMoveSpeed * 1.08f)).Within(0.001f));
            Assert.That(weapon.Damage, Is.EqualTo(baseDamage * 1.15f).Within(0.001f));
            Assert.That(weapon.FireInterval, Is.LessThan(baseFireInterval));
            Assert.That(weapon.Pierce, Is.EqualTo(1));
            Assert.That(weapon.ElitePriority, Is.True);
            Assert.That(growth.FragmentRequirement, Is.EqualTo(5));
            Assert.That(pickups.MagnetRadius, Is.EqualTo(baseMagnet * 1.3f).Within(0.001f));
            Assert.That(bodyDash.Distance, Is.EqualTo(baseDashDistance * 1.15f).Within(0.001f));
            Assert.That(bodyDash.Cooldown, Is.EqualTo(baseDashCooldown * 0.88f).Within(0.001f));
            Assert.That(bodyDash.RecoveryDuration, Is.EqualTo(baseDashRecovery - 0.05f).Within(0.001f));
            Assert.That(attack.Interval, Is.EqualTo(baseAttackInterval * 0.92f).Within(0.001f));
            Assert.That(laser.Interval, Is.EqualTo(baseLaserInterval * 0.92f).Within(0.001f));
            Assert.That(shield.RechargeDuration, Is.EqualTo(baseShieldRecharge * 0.92f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator ExperiencePickupFeedsLevelFlowAndResultKeepsRunSummary()
        {
            yield return LoadGameAndEnterCombat();

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var level = Object.FindAnyObjectByType<OSLevelUpController>();
            var pickups = Object.FindAnyObjectByType<OSPickupSpawner>();
            var collector = Object.FindAnyObjectByType<OSPickupCollector>();
            var resultLabel = GameObject.Find("Canvas")?.transform.Find("ResultPanel/Label")
                ?.GetComponent<TMP_Text>();

            var spawn = pickups.Spawn(OSPickupType.Experience, 15, collector.CollectionTarget.position);
            Assert.That(spawn.IsAccepted, Is.True);
            Assert.That(spawn.Payload.TryCollect(collector).IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.LevelUpSelection));
            Assert.That(level.ConfirmCandidate(0).IsAccepted, Is.True);
            Assert.That(level.AppliedUpgradeCount, Is.EqualTo(1));

            Assert.That(session.RequestDeath().IsAccepted, Is.True);
            Assert.That(resultLabel, Is.Not.Null);
            Assert.That(resultLabel.text, Does.Contain("UPGRADES 1"));
            Assert.That(resultLabel.text, Does.Contain("BUILD"));
            Assert.That(resultLabel.text, Does.Contain("LV 1"));
            Assert.That(resultLabel.text, Does.Contain($"RUN SEED {level.RunSeed}"));
        }

        private static IEnumerator LoadGameAndEnterCombat()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            Assert.That(growth, Is.Not.Null);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(Object.FindAnyObjectByType<OSGameSessionController>().State,
                Is.EqualTo(OSSessionState.Combat));
        }
    }
}
