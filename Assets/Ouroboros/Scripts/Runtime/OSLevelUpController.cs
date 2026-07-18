using System;
using System.Text;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DefaultExecutionOrder(-7000)]
    [DisallowMultipleComponent]
    public sealed class OSLevelUpController : MonoBehaviour
    {
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSUpgradeCatalog upgradeCatalog;
        [SerializeField] private OSPlayerBalanceData playerBalance;
        [SerializeField] private OSBodyBalanceData bodyBalance;
        [SerializeField] private OSPlayerHealth playerHealth;
        [SerializeField] private OSPlayerController playerController;
        [SerializeField] private OSHeadWeapon headWeapon;
        [SerializeField] private OSBodyGrowthController bodyGrowth;
        [SerializeField] private OSPickupSpawner pickupSpawner;
        [SerializeField] private OSExplosionController explosionController;
        [SerializeField] private OSAttackBodyRole attackBodyRole;
        [SerializeField] private OSLaserBodyRole laserBodyRole;
        [SerializeField] private OSControlBodyRole controlBodyRole;
        [SerializeField] private OSShieldBodyRole shieldBodyRole;
        [SerializeField] private int runSeed = 12012;

        private readonly OSExperienceProgress _experience = new();
        private readonly OSUpgradeCandidate[] _candidates = new OSUpgradeCandidate[OSUpgradeRunState.CandidateCount];
        private OSUpgradeRunState _upgradeState;
        private OSRunRandom _random;
        private int _candidateRequestId;
        private int _committedRequestId;
        private bool _subscribed;

        public event Action<int, float, int> ExperienceChanged;
        public event Action<int, OSUpgradeCandidate[]> CandidatesChanged;
        public event Action<OSUpgradeCandidate, OSUpgradeModifiers> UpgradeApplied;

        public int Level => _experience.Level;
        public float CurrentExperience => _experience.CurrentExperience;
        public int RequiredExperience => _experience.RequiredExperience;
        public int RunSeed => runSeed;
        public int AppliedUpgradeCount => _upgradeState?.AppliedUpgradeCount ?? 0;
        public int CandidateRequestId => _candidateRequestId;
        public string LastAppliedUpgradeId { get; private set; } = string.Empty;
        public OSUpgradeModifiers Modifiers => _upgradeState?.Modifiers ?? OSUpgradeModifiers.Default;

        private void Awake()
        {
            InitializeRun();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public OSUpgradeCandidate GetCandidate(int index)
        {
            return index >= 0 && index < _candidates.Length ? _candidates[index] : default;
        }

        public int GetUpgradeLevel(string id)
        {
            return _upgradeState?.GetLevel(id) ?? -1;
        }

        public string GetAppliedUpgradeSummary()
        {
            if (_upgradeState == null || upgradeCatalog == null || AppliedUpgradeCount <= 0)
            {
                return "NONE";
            }

            var builder = new StringBuilder(160);
            var entries = upgradeCatalog.Entries;
            var listed = 0;
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    continue;
                }

                var level = _upgradeState.GetLevel(entry.Id);
                if (level <= 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    if (listed % 3 == 0)
                    {
                        builder.Append('\n');
                    }
                    else
                    {
                        builder.Append("  |  ");
                    }
                }

                builder.Append(GetDisplayName(entry.Id));
                builder.Append(" LV ");
                builder.Append(level);
                listed++;
            }

            return builder.Length > 0 ? builder.ToString() : "NONE";
        }

        public OSRuleResult<int> AddExperience(int baseAmount)
        {
            if (baseAmount <= 0 || sessionController == null ||
                !sessionController.IsSimulationRunning || _upgradeState == null)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "level.experience.invalid_state");
            }

            var amount = baseAmount * Modifiers.ExperienceMultiplier;
            var result = _experience.AddExperience(amount);
            if (!result.IsAccepted)
            {
                return result;
            }

            var queued = 0;
            for (var index = 0; index < result.Payload; index++)
            {
                var queue = sessionController.QueueSelection(OSSelectionKind.LevelUp);
                if (!queue.IsAccepted)
                {
                    return OSRuleResult<int>.Rejected(queue.Code, queue.ReasonKey, queued);
                }

                queued++;
            }

            ExperienceChanged?.Invoke(Level, CurrentExperience, RequiredExperience);
            if (queued > 0 && sessionController.State == OSSessionState.Combat)
            {
                var process = sessionController.ProcessPendingSelection();
                if (!process.IsAccepted)
                {
                    return OSRuleResult<int>.Rejected(process.Code, process.ReasonKey, queued);
                }
            }

            return OSRuleResult<int>.Accepted(queued, "level.experience.applied");
        }

        public OSRuleResult<int> ConfirmCandidate(int candidateIndex)
        {
            if (_upgradeState == null || sessionController == null ||
                candidateIndex < 0 || candidateIndex >= _candidates.Length ||
                !sessionController.ActiveSelection.HasValue ||
                sessionController.ActiveSelection.Value.Kind != OSSelectionKind.LevelUp ||
                sessionController.ActiveSelection.Value.RequestId != _candidateRequestId)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "upgrade.confirm.stale_request");
            }

            var requestId = sessionController.ActiveSelection.Value.RequestId;
            if (_committedRequestId == requestId)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.Duplicate,
                    "upgrade.confirm.duplicate");
            }

            var candidate = _candidates[candidateIndex];
            if (string.IsNullOrWhiteSpace(candidate.Id))
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.ConfigurationError,
                    "upgrade.confirm.candidate_missing");
            }

            _committedRequestId = requestId;
            var apply = _upgradeState.Apply(candidate.Id);
            if (!apply.IsAccepted)
            {
                _committedRequestId = 0;
                return apply;
            }

            LastAppliedUpgradeId = candidate.Id;
            ApplyCurrentModifiers();
            UpgradeApplied?.Invoke(candidate, Modifiers);

            var completion = sessionController.CompleteActiveSelection();
            if (!completion.IsAccepted)
            {
                return OSRuleResult<int>.Rejected(completion.Code, completion.ReasonKey, apply.Payload);
            }

            return apply;
        }

        public string GetDisplayName(string id)
        {
            return id switch
            {
                "head_damage" => "CORE OVERDRIVE",
                "head_rate" => "NEURAL ACCELERATION",
                "head_pierce" => "PIERCING WARHEAD",
                "body_fragment_efficiency" => "GROWTH EFFICIENCY",
                "body_damage_rate" => "LINK AMPLIFIER",
                "role_overclock" => "ROLE OVERCLOCK",
                "explosion_radius" => "WAVE EXPANSION",
                "explosion_damage" => "CHAIN OVERLOAD",
                "explosion_efficiency" => "TISSUE PRESERVATION",
                "max_health" => "CORE REINFORCEMENT",
                "move_speed" => "FLEX NERVES",
                "heal_amount" => "REGEN TISSUE",
                "magnet_radius" => "COLLECTION FIELD",
                "experience_gain" => "LEARNING SYNC",
                "elite_priority" => "THREAT IDENTIFICATION",
                _ => id?.Replace('_', ' ').ToUpperInvariant() ?? "UNKNOWN"
            };
        }

        public string GetCategoryName(OSUpgradeCategory category)
        {
            return category switch
            {
                OSUpgradeCategory.Firepower => "FIREPOWER",
                OSUpgradeCategory.Body => "BODY",
                OSUpgradeCategory.Explosion => "EXPLOSION",
                OSUpgradeCategory.Survival => "SURVIVAL",
                OSUpgradeCategory.Utility => "UTILITY",
                _ => category.ToString().ToUpperInvariant()
            };
        }

        public string GetComparison(OSUpgradeCandidate candidate)
        {
            var level = candidate.CurrentLevel;
            var next = candidate.NextLevel;
            var value = candidate.Definition.PerLevelValue;
            return candidate.Definition.Operation switch
            {
                OSUpgradeOperation.AddHeadDamageMultiplier =>
                    $"HEAD DMG  {Percent(1f + (value * level))} → {Percent(1f + (value * next))}",
                OSUpgradeOperation.AddHeadRateMultiplier =>
                    $"HEAD RATE  {HeadInterval(value, level):0.00}s → {HeadInterval(value, next):0.00}s",
                OSUpgradeOperation.AddHeadPierce =>
                    $"PIERCE  {Mathf.RoundToInt(value * level)} → {Mathf.RoundToInt(value * next)}",
                OSUpgradeOperation.AddFragmentRequirementMultiplier =>
                    $"FRAGMENTS  {FragmentRequirement(value, level)} → {FragmentRequirement(value, next)}",
                OSUpgradeOperation.AddBodyDamageRate =>
                    $"PER BODY  {BodyDamageRate(value, level):0}% → {BodyDamageRate(value, next):0}%",
                OSUpgradeOperation.AddRoleCooldownMultiplier =>
                    $"ROLE CD  {Percent(1f + (value * level))} → {Percent(1f + (value * next))}",
                OSUpgradeOperation.AddExplosionRadiusMultiplier =>
                    $"BLAST R  {ExplosionRadius(value, level):0.00} → {ExplosionRadius(value, next):0.00}",
                OSUpgradeOperation.AddExplosionDamageMultiplier =>
                    $"BLAST/SEG  {ExplosionDamage(value, level):0} → {ExplosionDamage(value, next):0}",
                OSUpgradeOperation.AddExplosionConsumeRate =>
                    $"CONSUME  {ExplosionConsume(value, level):0}% → {ExplosionConsume(value, next):0}%",
                OSUpgradeOperation.AddMaxHealth =>
                    $"MAX HP  {MaxHealth(value, level):0} → {MaxHealth(value, next):0}",
                OSUpgradeOperation.AddMoveSpeedMultiplier =>
                    $"MOVE  {MoveSpeed(value, level):0.00} → {MoveSpeed(value, next):0.00}",
                OSUpgradeOperation.AddHealMultiplier =>
                    $"HEAL  {Percent(1f + (value * level))} → {Percent(1f + (value * next))}",
                OSUpgradeOperation.AddMagnetMultiplier =>
                    $"MAGNET R  {MagnetRadius(value, level):0.00} → {MagnetRadius(value, next):0.00}",
                OSUpgradeOperation.AddExperienceMultiplier =>
                    $"XP GAIN  {Percent(1f + (value * level))} → {Percent(1f + (value * next))}",
                OSUpgradeOperation.EnableElitePriority => "TARGETING  NEAREST → ELITE FIRST",
                _ => $"LEVEL {level} → {next}"
            };
        }

        private void InitializeRun()
        {
            _upgradeState = new OSUpgradeRunState(upgradeCatalog?.Entries);
            _random = new OSRunRandom(runSeed);
            _experience.Reset();
            Array.Clear(_candidates, 0, _candidates.Length);
            _candidateRequestId = 0;
            _committedRequestId = 0;
            LastAppliedUpgradeId = string.Empty;
            ApplyCurrentModifiers();
        }

        private void ApplyCurrentModifiers()
        {
            var modifiers = Modifiers;
            playerHealth?.ApplyUpgradeModifiers(modifiers);
            playerController?.ApplyUpgradeModifiers(modifiers);
            headWeapon?.ApplyUpgradeModifiers(modifiers);
            bodyGrowth?.ApplyUpgradeModifiers(modifiers);
            pickupSpawner?.ApplyUpgradeModifiers(modifiers);
            explosionController?.ApplyUpgradeModifiers(modifiers);
            attackBodyRole?.ApplyUpgradeModifiers(modifiers);
            laserBodyRole?.ApplyUpgradeModifiers(modifiers);
            controlBodyRole?.ApplyUpgradeModifiers(modifiers);
            shieldBodyRole?.ApplyUpgradeModifiers(modifiers);
        }

        private void BuildCandidates(OSSelectionRequest request)
        {
            _candidateRequestId = 0;
            _committedRequestId = 0;
            Array.Clear(_candidates, 0, _candidates.Length);
            var result = _upgradeState.BuildCandidates(
                _upgradeState.AppliedUpgradeCount + 1,
                _random,
                _candidates);
            if (!result.IsAccepted)
            {
                Debug.LogError($"[OUROBOROS][LEVEL] Candidate generation failed: {result.ReasonKey}", this);
                CandidatesChanged?.Invoke(0, _candidates);
                return;
            }

            _candidateRequestId = request.RequestId;
            CandidatesChanged?.Invoke(_candidateRequestId, _candidates);
        }

        private void Subscribe()
        {
            if (_subscribed || sessionController == null || !isActiveAndEnabled)
            {
                return;
            }

            sessionController.StateChanged += HandleStateChanged;
            sessionController.ActiveSelectionChanged += HandleActiveSelectionChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || sessionController == null)
            {
                _subscribed = false;
                return;
            }

            sessionController.StateChanged -= HandleStateChanged;
            sessionController.ActiveSelectionChanged -= HandleActiveSelectionChanged;
            _subscribed = false;
        }

        private void HandleStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (previous == OSSessionState.Boot && current == OSSessionState.StartBodySelection)
            {
                InitializeRun();
                ExperienceChanged?.Invoke(Level, CurrentExperience, RequiredExperience);
            }
            else if (current is OSSessionState.Dead or OSSessionState.Cleared or OSSessionState.Result)
            {
                _candidateRequestId = 0;
                _committedRequestId = 0;
                Array.Clear(_candidates, 0, _candidates.Length);
                CandidatesChanged?.Invoke(0, _candidates);
            }
        }

        private void HandleActiveSelectionChanged(OSSelectionRequest? request)
        {
            if (!request.HasValue || request.Value.Kind != OSSelectionKind.LevelUp)
            {
                _candidateRequestId = 0;
                _committedRequestId = 0;
                Array.Clear(_candidates, 0, _candidates.Length);
                CandidatesChanged?.Invoke(0, _candidates);
                return;
            }

            BuildCandidates(request.Value);
        }

        private float HeadInterval(float value, int level)
        {
            var baseInterval = playerBalance != null ? playerBalance.HeadFireInterval : 0.5f;
            return OSUpgradeMath.CalculateHeadFireInterval(baseInterval, value * level);
        }

        private int FragmentRequirement(float value, int level)
        {
            var baseRequirement = bodyBalance != null ? bodyBalance.FragmentRequirement : 12;
            return OSUpgradeMath.CalculateFragmentRequirement(baseRequirement, 1f + (value * level));
        }

        private float BodyDamageRate(float value, int level)
        {
            var baseRate = bodyBalance != null ? bodyBalance.BodyDamageRate : 0.04f;
            return Mathf.Min(0.06f, baseRate + (value * level)) * 100f;
        }

        private float ExplosionRadius(float value, int level)
        {
            var radius = bodyBalance != null ? bodyBalance.Explosion.Radius : 1.8f;
            return radius * (1f + (value * level));
        }

        private float ExplosionDamage(float value, int level)
        {
            var damage = bodyBalance != null ? bodyBalance.Explosion.DamagePerSegment : 35f;
            return damage * (1f + (value * level));
        }

        private float ExplosionConsume(float value, int level)
        {
            var rate = bodyBalance != null ? bodyBalance.Explosion.ConsumeRate : 0.3f;
            return OSUpgradeMath.CalculateExplosionConsumeRate(rate, value * level) * 100f;
        }

        private float MaxHealth(float value, int level)
        {
            var health = playerBalance != null ? playerBalance.MaxHealth : 100f;
            return health * (1f + (value * level));
        }

        private float MoveSpeed(float value, int level)
        {
            var speed = playerBalance != null ? playerBalance.MoveSpeed : 5.5f;
            return OSUpgradeMath.CalculateMoveSpeed(speed, 1f + (value * level));
        }

        private float MagnetRadius(float value, int level)
        {
            var radius = playerBalance != null ? playerBalance.MagnetRadius : 1.25f;
            return radius * (1f + (value * level));
        }

        private static string Percent(float multiplier)
        {
            return $"{multiplier * 100f:0}%";
        }
    }
}
