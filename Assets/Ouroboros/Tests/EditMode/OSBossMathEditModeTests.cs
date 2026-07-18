using NUnit.Framework;
using Ouroboros.Core;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSBossMathEditModeTests
    {
        [Test]
        public void HealthRatio_SelectsThreePhasesAtDefinedThresholds()
        {
            Assert.That(OSBossMath.GetPhase(6000f, 6000f), Is.EqualTo(OSBossPhase.PhaseOne));
            Assert.That(OSBossMath.GetPhase(4201f, 6000f), Is.EqualTo(OSBossPhase.PhaseOne));
            Assert.That(OSBossMath.GetPhase(4200f, 6000f), Is.EqualTo(OSBossPhase.PhaseTwo));
            Assert.That(OSBossMath.GetPhase(2101f, 6000f), Is.EqualTo(OSBossPhase.PhaseTwo));
            Assert.That(OSBossMath.GetPhase(2100f, 6000f), Is.EqualTo(OSBossPhase.PhaseThree));
        }

        [Test]
        public void ShieldDamage_IsSeparatedFromHealthOverflow()
        {
            var shieldOnly = OSBossMath.ResolveShieldDamage(600f, 250f);
            var overflow = OSBossMath.ResolveShieldDamage(shieldOnly.RemainingShield, 500f);

            Assert.That(shieldOnly.ShieldDamage, Is.EqualTo(250f));
            Assert.That(shieldOnly.HealthDamage, Is.Zero);
            Assert.That(shieldOnly.RemainingShield, Is.EqualTo(350f));
            Assert.That(overflow.ShieldDamage, Is.EqualTo(350f));
            Assert.That(overflow.HealthDamage, Is.EqualTo(150f));
            Assert.That(overflow.RemainingShield, Is.Zero);
        }

        [Test]
        public void TimeLimit_ClampsAtZeroAndIgnoresNegativeElapsed()
        {
            Assert.That(OSBossMath.GetRemainingTime(90f, -5f), Is.EqualTo(90f));
            Assert.That(OSBossMath.GetRemainingTime(90f, 35.5f), Is.EqualTo(54.5f));
            Assert.That(OSBossMath.GetRemainingTime(90f, 100f), Is.Zero);
        }

        [Test]
        public void LaterPhase_IncreasesFanCountAndReducesPatternInterval()
        {
            Assert.That(OSBossMath.GetFanProjectileCount(OSBossPhase.PhaseOne), Is.EqualTo(5));
            Assert.That(OSBossMath.GetFanProjectileCount(OSBossPhase.PhaseThree), Is.EqualTo(7));
            Assert.That(OSBossMath.GetPatternInterval(OSBossPhase.PhaseThree),
                Is.LessThan(OSBossMath.GetPatternInterval(OSBossPhase.PhaseOne)));
        }

        [Test]
        public void SessionSummary_PreservesResultAndRunStatistics()
        {
            var maxRoles = new OSRoleCountSnapshot(3, 4, 2, 1);
            var finalRoles = new OSRoleCountSnapshot(1, 2, 0, 1);
            var summary = new OSSessionSummary(
                OSSessionState.Cleared,
                OSSessionResultKind.BossDefeated,
                642f,
                300,
                2,
                80,
                18,
                9,
                24,
                7,
                8,
                64f,
                maxRoles,
                finalRoles,
                8,
                7,
                12012,
                "step14-v1",
                "head_damage LV3");

            Assert.That(summary.ResultKind, Is.EqualTo(OSSessionResultKind.BossDefeated));
            Assert.That(summary.ExplosionConsumedBodyCount, Is.EqualTo(8));
            Assert.That(summary.MaxRoleCounts.Attack, Is.EqualTo(4));
            Assert.That(summary.FinalRoleCounts.Shield, Is.EqualTo(1));
            Assert.That(summary.AppliedUpgradeCount, Is.EqualTo(7));
            Assert.That(summary.UpgradeSummary, Does.Contain("head_damage"));
        }
    }
}
