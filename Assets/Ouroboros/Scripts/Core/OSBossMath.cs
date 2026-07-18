using System;

namespace Ouroboros.Core
{
    public readonly struct OSBossDamageResolution
    {
        public OSBossDamageResolution(
            float incomingDamage,
            float shieldDamage,
            float healthDamage,
            float remainingShield)
        {
            IncomingDamage = incomingDamage;
            ShieldDamage = shieldDamage;
            HealthDamage = healthDamage;
            RemainingShield = remainingShield;
        }

        public float IncomingDamage { get; }
        public float ShieldDamage { get; }
        public float HealthDamage { get; }
        public float RemainingShield { get; }
    }

    public static class OSBossMath
    {
        public static OSBossPhase GetPhase(float currentHealth, float maxHealth)
        {
            if (!float.IsFinite(currentHealth) || !float.IsFinite(maxHealth) || maxHealth <= 0f)
            {
                return OSBossPhase.PhaseOne;
            }

            var ratio = Math.Clamp(currentHealth / maxHealth, 0f, 1f);
            if (ratio > 0.70f)
            {
                return OSBossPhase.PhaseOne;
            }

            return ratio > 0.35f ? OSBossPhase.PhaseTwo : OSBossPhase.PhaseThree;
        }

        public static float GetPatternInterval(OSBossPhase phase)
        {
            return phase switch
            {
                OSBossPhase.PhaseOne => 4f,
                OSBossPhase.PhaseTwo => 3.4f,
                OSBossPhase.PhaseThree => 2.8f,
                _ => 4f
            };
        }

        public static int GetFanProjectileCount(OSBossPhase phase)
        {
            return phase == OSBossPhase.PhaseThree ? 7 : 5;
        }

        public static float GetRemainingTime(float limitSeconds, float elapsedSeconds)
        {
            if (!float.IsFinite(limitSeconds) || limitSeconds <= 0f ||
                !float.IsFinite(elapsedSeconds))
            {
                return 0f;
            }

            return Math.Max(0f, limitSeconds - Math.Max(0f, elapsedSeconds));
        }

        public static OSBossDamageResolution ResolveShieldDamage(
            float shieldHealth,
            float incomingDamage)
        {
            if (!float.IsFinite(shieldHealth) || shieldHealth < 0f ||
                !float.IsFinite(incomingDamage) || incomingDamage <= 0f)
            {
                return new OSBossDamageResolution(0f, 0f, 0f, Math.Max(0f, shieldHealth));
            }

            var absorbed = Math.Min(shieldHealth, incomingDamage);
            return new OSBossDamageResolution(
                incomingDamage,
                absorbed,
                incomingDamage - absorbed,
                shieldHealth - absorbed);
        }
    }
}
