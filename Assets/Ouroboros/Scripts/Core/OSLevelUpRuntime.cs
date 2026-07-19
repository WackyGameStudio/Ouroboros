using System;
using System.Collections.Generic;

namespace Ouroboros.Core
{
    public sealed class OSRunRandom
    {
        private readonly Random _random;

        public OSRunRandom(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        public int Seed { get; }

        public int Next(int maximumExclusive)
        {
            return maximumExclusive > 0 ? _random.Next(maximumExclusive) : 0;
        }

        public double NextDouble()
        {
            return _random.NextDouble();
        }
    }

    public sealed class OSExperienceProgress
    {
        public const int InitialRequirement = 15;
        public const float RequirementGrowth = 1.18f;
        private const int MaximumLevelUpsPerGrant = 256;

        public int Level { get; private set; } = 1;
        public float CurrentExperience { get; private set; }
        public int RequiredExperience { get; private set; } = InitialRequirement;

        public OSRuleResult<int> AddExperience(float amount)
        {
            if (!float.IsFinite(amount) || amount <= 0f)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "experience.invalid_amount");
            }

            CurrentExperience += amount;
            var levelUps = 0;
            while (CurrentExperience + 0.0001f >= RequiredExperience &&
                   levelUps < MaximumLevelUpsPerGrant)
            {
                CurrentExperience -= RequiredExperience;
                Level++;
                RequiredExperience = CalculateNextRequirement(RequiredExperience);
                levelUps++;
            }

            return OSRuleResult<int>.Accepted(levelUps, "experience.applied");
        }

        public void Reset()
        {
            Level = 1;
            CurrentExperience = 0f;
            RequiredExperience = InitialRequirement;
        }

        public static int CalculateNextRequirement(int currentRequirement)
        {
            return Math.Max(1, (int)Math.Ceiling(Math.Max(1, currentRequirement) * RequirementGrowth));
        }
    }

    public readonly struct OSUpgradeModifiers
    {
        public OSUpgradeModifiers(
            float headDamageMultiplier,
            float headRateBonus,
            int headPierceBonus,
            float fragmentRequirementMultiplier,
            float bodyDamageRateBonus,
            float roleCooldownMultiplier,
            float dashDistanceMultiplier,
            float dashCooldownMultiplier,
            float dashRecoveryDurationDelta,
            float maxHealthMultiplier,
            float moveSpeedMultiplier,
            float healMultiplier,
            float magnetMultiplier,
            float experienceMultiplier,
            bool elitePriority)
        {
            HeadDamageMultiplier = headDamageMultiplier;
            HeadRateBonus = headRateBonus;
            HeadPierceBonus = headPierceBonus;
            FragmentRequirementMultiplier = fragmentRequirementMultiplier;
            BodyDamageRateBonus = bodyDamageRateBonus;
            RoleCooldownMultiplier = roleCooldownMultiplier;
            DashDistanceMultiplier = dashDistanceMultiplier;
            DashCooldownMultiplier = dashCooldownMultiplier;
            DashRecoveryDurationDelta = dashRecoveryDurationDelta;
            MaxHealthMultiplier = maxHealthMultiplier;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            HealMultiplier = healMultiplier;
            MagnetMultiplier = magnetMultiplier;
            ExperienceMultiplier = experienceMultiplier;
            ElitePriority = elitePriority;
        }

        public static OSUpgradeModifiers Default => new(
            1f,
            0f,
            0,
            1f,
            0f,
            1f,
            1f,
            1f,
            0f,
            1f,
            1f,
            1f,
            1f,
            1f,
            false);

        public float HeadDamageMultiplier { get; }
        public float HeadRateBonus { get; }
        public int HeadPierceBonus { get; }
        public float FragmentRequirementMultiplier { get; }
        public float BodyDamageRateBonus { get; }
        public float RoleCooldownMultiplier { get; }
        public float DashDistanceMultiplier { get; }
        public float DashCooldownMultiplier { get; }
        public float DashRecoveryDurationDelta { get; }
        public float MaxHealthMultiplier { get; }
        public float MoveSpeedMultiplier { get; }
        public float HealMultiplier { get; }
        public float MagnetMultiplier { get; }
        public float ExperienceMultiplier { get; }
        public bool ElitePriority { get; }
    }

    public readonly struct OSUpgradeCandidate
    {
        public OSUpgradeCandidate(OSUpgradeRuntimeDefinition definition, int currentLevel)
        {
            Definition = definition;
            CurrentLevel = currentLevel;
        }

        public OSUpgradeRuntimeDefinition Definition { get; }
        public string Id => Definition.Id;
        public OSUpgradeCategory Category => Definition.Category;
        public int CurrentLevel { get; }
        public int NextLevel => CurrentLevel + 1;
        public int MaxLevel => Definition.MaxLevel;
    }

    public sealed class OSUpgradeRunState
    {
        public const int CandidateCount = 3;
        private readonly OSUpgradeRuntimeDefinition[] _definitions;
        private readonly int[] _levels;
        private readonly int[] _eligibleIndices;
        private readonly float[] _eligibleWeights;

        public OSUpgradeRunState(IReadOnlyList<OSUpgradeDefinition> source)
        {
            if (source == null)
            {
                _definitions = Array.Empty<OSUpgradeRuntimeDefinition>();
            }
            else
            {
                _definitions = new OSUpgradeRuntimeDefinition[source.Count];
                for (var index = 0; index < source.Count; index++)
                {
                    if (source[index] != null)
                    {
                        _definitions[index] = new OSUpgradeRuntimeDefinition(source[index]);
                    }
                }
            }

            _levels = new int[_definitions.Length];
            _eligibleIndices = new int[_definitions.Length];
            _eligibleWeights = new float[_definitions.Length];
            Modifiers = OSUpgradeModifiers.Default;
        }

        public OSUpgradeRunState(IReadOnlyList<OSUpgradeRuntimeDefinition> source)
        {
            _definitions = source != null
                ? CopyDefinitions(source)
                : Array.Empty<OSUpgradeRuntimeDefinition>();
            _levels = new int[_definitions.Length];
            _eligibleIndices = new int[_definitions.Length];
            _eligibleWeights = new float[_definitions.Length];
            Modifiers = OSUpgradeModifiers.Default;
        }

        public int Count => _definitions.Length;
        public int AppliedUpgradeCount { get; private set; }
        public OSUpgradeModifiers Modifiers { get; private set; }

        public int GetLevel(string id)
        {
            var index = FindIndex(id);
            return index >= 0 ? _levels[index] : -1;
        }

        public OSUpgradeRuntimeDefinition GetDefinition(string id)
        {
            var index = FindIndex(id);
            return index >= 0 ? _definitions[index] : default;
        }

        public OSRuleResult<int> BuildCandidates(
            int selectionOrdinal,
            OSRunRandom random,
            OSUpgradeCandidate[] destination)
        {
            if (selectionOrdinal <= 0 || random == null || destination == null ||
                destination.Length < CandidateCount)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "upgrade.candidates.invalid_input");
            }

            Array.Clear(destination, 0, destination.Length);
            var selectedIndices = new int[CandidateCount];
            Array.Fill(selectedIndices, -1);

            if (selectionOrdinal <= 3)
            {
                var categories = new[]
                {
                    OSUpgradeCategory.Firepower,
                    OSUpgradeCategory.Body,
                    OSUpgradeCategory.Survival
                };

                for (var slot = 0; slot < CandidateCount; slot++)
                {
                    selectedIndices[slot] = SelectWeighted(
                        random,
                        categories[slot],
                        true,
                        selectedIndices,
                        slot);
                    if (selectedIndices[slot] < 0)
                    {
                        return OSRuleResult<int>.Rejected(
                            OSResultCode.ConfigurationError,
                            "upgrade.candidates.early_category_missing");
                    }
                }

                for (var index = CandidateCount - 1; index > 0; index--)
                {
                    var swap = random.Next(index + 1);
                    (selectedIndices[index], selectedIndices[swap]) =
                        (selectedIndices[swap], selectedIndices[index]);
                }
            }
            else
            {
                for (var slot = 0; slot < CandidateCount; slot++)
                {
                    selectedIndices[slot] = SelectWeighted(
                        random,
                        default,
                        false,
                        selectedIndices,
                        slot);
                    if (selectedIndices[slot] < 0)
                    {
                        return OSRuleResult<int>.Rejected(
                            OSResultCode.ConfigurationError,
                            "upgrade.candidates.fewer_than_three");
                    }
                }
            }

            for (var slot = 0; slot < CandidateCount; slot++)
            {
                var definitionIndex = selectedIndices[slot];
                destination[slot] = new OSUpgradeCandidate(
                    _definitions[definitionIndex],
                    _levels[definitionIndex]);
            }

            return OSRuleResult<int>.Accepted(CandidateCount, "upgrade.candidates.created");
        }

        public OSRuleResult<int> Apply(string id)
        {
            var index = FindIndex(id);
            if (index < 0)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "upgrade.apply.unknown_id");
            }

            if (_levels[index] >= _definitions[index].MaxLevel)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedCapacity,
                    "upgrade.apply.max_level",
                    _levels[index]);
            }

            _levels[index]++;
            AppliedUpgradeCount++;
            RecalculateModifiers();
            return OSRuleResult<int>.Accepted(_levels[index], "upgrade.apply.accepted");
        }

        public void Reset()
        {
            Array.Clear(_levels, 0, _levels.Length);
            AppliedUpgradeCount = 0;
            Modifiers = OSUpgradeModifiers.Default;
        }

        private int SelectWeighted(
            OSRunRandom random,
            OSUpgradeCategory category,
            bool filterCategory,
            int[] excluded,
            int excludedCount)
        {
            var eligibleCount = 0;
            var totalWeight = 0f;
            for (var index = 0; index < _definitions.Length; index++)
            {
                var definition = _definitions[index];
                if (string.IsNullOrWhiteSpace(definition.Id) ||
                    _levels[index] >= definition.MaxLevel ||
                    definition.CandidateWeight <= 0f ||
                    filterCategory && definition.Category != category ||
                    Contains(excluded, excludedCount, index))
                {
                    continue;
                }

                var weight = definition.CandidateWeight / (1f + (_levels[index] * 0.25f));
                _eligibleIndices[eligibleCount] = index;
                _eligibleWeights[eligibleCount] = weight;
                totalWeight += weight;
                eligibleCount++;
            }

            if (eligibleCount <= 0 || totalWeight <= 0f)
            {
                return -1;
            }

            var roll = random.NextDouble() * totalWeight;
            for (var index = 0; index < eligibleCount; index++)
            {
                roll -= _eligibleWeights[index];
                if (roll <= 0d)
                {
                    return _eligibleIndices[index];
                }
            }

            return _eligibleIndices[eligibleCount - 1];
        }

        private void RecalculateModifiers()
        {
            var headDamage = 1f;
            var headRate = 0f;
            var headPierce = 0;
            var fragmentRequirement = 1f;
            var bodyDamageRate = 0f;
            var roleCooldown = 1f;
            var dashDistance = 1f;
            var dashCooldown = 1f;
            var dashRecoveryDuration = 0f;
            var maxHealth = 1f;
            var moveSpeed = 1f;
            var heal = 1f;
            var magnet = 1f;
            var experience = 1f;
            var elitePriority = false;

            for (var index = 0; index < _definitions.Length; index++)
            {
                var level = _levels[index];
                if (level <= 0)
                {
                    continue;
                }

                var value = _definitions[index].PerLevelValue * level;
                switch (_definitions[index].Operation)
                {
                    case OSUpgradeOperation.AddHeadDamageMultiplier:
                        headDamage += value;
                        break;
                    case OSUpgradeOperation.AddHeadRateMultiplier:
                        headRate += value;
                        break;
                    case OSUpgradeOperation.AddHeadPierce:
                        headPierce += (int)Math.Round(value);
                        break;
                    case OSUpgradeOperation.AddFragmentRequirementMultiplier:
                        fragmentRequirement += value;
                        break;
                    case OSUpgradeOperation.AddBodyDamageRate:
                        bodyDamageRate += value;
                        break;
                    case OSUpgradeOperation.AddRoleCooldownMultiplier:
                        roleCooldown += value;
                        break;
                    case OSUpgradeOperation.AddDashDistanceMultiplier:
                        dashDistance += value;
                        break;
                    case OSUpgradeOperation.AddDashCooldownMultiplier:
                        dashCooldown += value;
                        break;
                    case OSUpgradeOperation.AddDashRecoveryDuration:
                        dashRecoveryDuration += value;
                        break;
                    case OSUpgradeOperation.AddMaxHealth:
                        maxHealth += value;
                        break;
                    case OSUpgradeOperation.AddMoveSpeedMultiplier:
                        moveSpeed += value;
                        break;
                    case OSUpgradeOperation.AddHealMultiplier:
                        heal += value;
                        break;
                    case OSUpgradeOperation.AddMagnetMultiplier:
                        magnet += value;
                        break;
                    case OSUpgradeOperation.AddExperienceMultiplier:
                        experience += value;
                        break;
                    case OSUpgradeOperation.EnableElitePriority:
                        elitePriority = true;
                        break;
                }
            }

            Modifiers = new OSUpgradeModifiers(
                Math.Max(0.01f, headDamage),
                Math.Max(0f, headRate),
                Math.Max(0, headPierce),
                Math.Max(0.01f, fragmentRequirement),
                Math.Max(0f, bodyDamageRate),
                Math.Clamp(roleCooldown, 0.5f, 1f),
                Math.Max(0.01f, dashDistance),
                Math.Clamp(dashCooldown, 0.5f, 1f),
                dashRecoveryDuration,
                Math.Max(0.01f, maxHealth),
                Math.Max(0.01f, moveSpeed),
                Math.Max(0.01f, heal),
                Math.Max(0.01f, magnet),
                Math.Max(0.01f, experience),
                elitePriority);
        }

        private int FindIndex(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return -1;
            }

            for (var index = 0; index < _definitions.Length; index++)
            {
                if (string.Equals(_definitions[index].Id, id, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool Contains(int[] values, int count, int target)
        {
            for (var index = 0; index < count; index++)
            {
                if (values[index] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static OSUpgradeRuntimeDefinition[] CopyDefinitions(
            IReadOnlyList<OSUpgradeRuntimeDefinition> source)
        {
            var copy = new OSUpgradeRuntimeDefinition[source.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                copy[index] = source[index];
            }

            return copy;
        }
    }

    public static class OSUpgradeMath
    {
        public static float CalculateHeadFireInterval(float baseInterval, float rateBonus)
        {
            if (!float.IsFinite(baseInterval) || baseInterval <= 0f)
            {
                return 0.15f;
            }

            return Math.Max(0.15f, baseInterval / (1f + Math.Max(0f, rateBonus)));
        }

        public static int CalculateFragmentRequirement(float baseRequirement, float multiplier)
        {
            if (!float.IsFinite(baseRequirement) || !float.IsFinite(multiplier))
            {
                return 4;
            }

            return Math.Max(4, (int)Math.Ceiling(Math.Max(1f, baseRequirement) * Math.Max(0.01f, multiplier)));
        }

        public static float CalculateMoveSpeed(float baseSpeed, float multiplier)
        {
            if (!float.IsFinite(baseSpeed) || !float.IsFinite(multiplier))
            {
                return 0f;
            }

            return Math.Clamp(baseSpeed * multiplier, 0f, 7.5f);
        }
    }
}
