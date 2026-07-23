using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DefaultExecutionOrder(7900)]
    [DisallowMultipleComponent]
    public sealed class OSPlayerHealth : MonoBehaviour
    {
        private const float DefaultMaxHealth = 100f;
        private const float DefaultHitInvulnerability = 0.6f;

        [SerializeField] private OSPlayerBalanceData playerBalance;
        [SerializeField] private OSGameSessionController sessionController;

        private float _baseMaxHealth = DefaultMaxHealth;
        private float _maxHealth = DefaultMaxHealth;
        private float _hitInvulnerabilityDuration = DefaultHitInvulnerability;
        private float _healMultiplier = 1f;
        private bool _subscribed;

        public event Action<float, float> HealthChanged;
        public event Action<OSDamageEvent, float> HeadDamaged;
        public event Action<float> Healed;
        public event Action Died;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => _maxHealth;
        public float HealMultiplier => _healMultiplier;
        public float HitInvulnerabilityRemaining { get; private set; }
        public bool IsAbilityInvulnerable { get; private set; }
        public bool IsInvulnerable => HitInvulnerabilityRemaining > 0f || IsAbilityInvulnerable;
        public float LastAppliedDamage { get; private set; }

        private void Awake()
        {
            ResolveBalance();
            ResetForNewSession();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void FixedUpdate()
        {
            if (sessionController != null && !sessionController.IsSimulationRunning)
            {
                return;
            }

            SimulateTime(Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public OSRuleResult<float> TryApplyHeadDamage(OSDamageEvent damageEvent)
        {
            if (damageEvent.TargetKind != OSTargetKind.PlayerHead ||
                !float.IsFinite(damageEvent.Damage) || damageEvent.Damage <= 0f)
            {
                return OSRuleResult<float>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "player_health.damage.invalid",
                    CurrentHealth);
            }

            if (sessionController != null && !sessionController.IsSimulationRunning || CurrentHealth <= 0f)
            {
                return OSRuleResult<float>.Rejected(
                    OSResultCode.RejectedState,
                    "player_health.damage.invalid_state",
                    CurrentHealth);
            }

            if (IsInvulnerable)
            {
                return OSRuleResult<float>.Rejected(
                    OSResultCode.RejectedInvulnerable,
                    "player_health.damage.invulnerable",
                    CurrentHealth);
            }

            var previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damageEvent.Damage);
            LastAppliedDamage = previousHealth - CurrentHealth;
            HitInvulnerabilityRemaining = _hitInvulnerabilityDuration;
            HealthChanged?.Invoke(CurrentHealth, _maxHealth);
            HeadDamaged?.Invoke(damageEvent, CurrentHealth);

            if (CurrentHealth <= 0f)
            {
                Died?.Invoke();
                sessionController?.RequestDeath();
            }

            return OSRuleResult<float>.Accepted(CurrentHealth, "player_health.damage.accepted");
        }

        public OSRuleResult<float> TryHeal(float amount)
        {
            if (!float.IsFinite(amount) || amount <= 0f || CurrentHealth <= 0f)
            {
                return OSRuleResult<float>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "player_health.heal.invalid",
                    CurrentHealth);
            }

            var previous = CurrentHealth;
            CurrentHealth = Mathf.Min(_maxHealth, CurrentHealth + (amount * _healMultiplier));
            var applied = CurrentHealth - previous;
            if (applied <= 0f)
            {
                return OSRuleResult<float>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "player_health.heal.full",
                    CurrentHealth);
            }

            HealthChanged?.Invoke(CurrentHealth, _maxHealth);
            Healed?.Invoke(applied);
            return OSRuleResult<float>.Accepted(CurrentHealth, "player_health.heal.accepted");
        }

        public void ApplyUpgradeModifiers(OSUpgradeModifiers modifiers)
        {
            var previousMax = _maxHealth;
            _maxHealth = Mathf.Max(1f, _baseMaxHealth * modifiers.MaxHealthMultiplier);
            _healMultiplier = Mathf.Max(0.01f, modifiers.HealMultiplier);
            CurrentHealth = _maxHealth > previousMax
                ? Mathf.Min(_maxHealth, CurrentHealth + (_maxHealth - previousMax))
                : Mathf.Min(CurrentHealth, _maxHealth);
            HealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        public void SetAbilityInvulnerable(bool value)
        {
            IsAbilityInvulnerable = value;
        }

        internal void ConfigureForTesting(
            OSGameSessionController session,
            float maxHealth = DefaultMaxHealth,
            float hitInvulnerability = DefaultHitInvulnerability)
        {
            Unsubscribe();
            playerBalance = null;
            sessionController = session;
            _baseMaxHealth = Mathf.Max(1f, maxHealth);
            _maxHealth = _baseMaxHealth;
            _healMultiplier = 1f;
            _hitInvulnerabilityDuration = Mathf.Max(0f, hitInvulnerability);
            ResetForNewSession();
            Subscribe();
        }

        internal void SimulateTimeForTesting(float deltaTime)
        {
            SimulateTime(deltaTime);
        }

        private void ResolveBalance()
        {
            _baseMaxHealth = playerBalance != null
                ? Mathf.Max(1, playerBalance.MaxHealth)
                : DefaultMaxHealth;
            _maxHealth = Mathf.Max(1f, _baseMaxHealth);
            _hitInvulnerabilityDuration = playerBalance != null
                ? Mathf.Max(0f, playerBalance.HitInvulnerability)
                : DefaultHitInvulnerability;
        }

        private void ResetForNewSession()
        {
            ResolveBalance();
            CurrentHealth = _maxHealth;
            HitInvulnerabilityRemaining = 0f;
            IsAbilityInvulnerable = false;
            LastAppliedDamage = 0f;
            HealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        private void SimulateTime(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            HitInvulnerabilityRemaining = Mathf.Max(0f, HitInvulnerabilityRemaining - deltaTime);
        }

        private void Subscribe()
        {
            if (_subscribed || sessionController == null || !isActiveAndEnabled)
            {
                return;
            }

            sessionController.StateChanged += HandleStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged -= HandleStateChanged;
            }

            _subscribed = false;
        }

        private void HandleStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (previous == OSSessionState.Boot && current == OSSessionState.StartBodySelection)
            {
                ResetForNewSession();
            }
        }
    }
}
