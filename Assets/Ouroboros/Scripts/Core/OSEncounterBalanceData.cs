using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros.Core
{
    [Serializable]
    public sealed class OSDropTable
    {
        [SerializeField] private int experienceAmount = 1;
        [SerializeField] private int fragmentAmount;
        [SerializeField] private float fragmentChance;
        [SerializeField] private int healAmount;
        [SerializeField] private float healChance;

        public int ExperienceAmount => experienceAmount;
        public int FragmentAmount => fragmentAmount;
        public float FragmentChance => fragmentChance;
        public int HealAmount => healAmount;
        public float HealChance => healChance;

        internal void CollectValidationErrors(List<string> errors, string path)
        {
            RequireNonNegative(experienceAmount, $"{path}.experienceAmount", errors);
            RequireNonNegative(fragmentAmount, $"{path}.fragmentAmount", errors);
            RequireChance(fragmentChance, $"{path}.fragmentChance", errors);
            RequireNonNegative(healAmount, $"{path}.healAmount", errors);
            RequireChance(healChance, $"{path}.healChance", errors);
        }

        private static void RequireNonNegative(int value, string path, List<string> errors)
        {
            if (value < 0)
            {
                errors.Add($"{path}: expected a value greater than or equal to 0, actual {value}.");
            }
        }

        private static void RequireChance(float value, string path, List<string> errors)
        {
            OSValidationUtility.RequireFiniteNonNegative(value, path, errors);
            if (OSValidationUtility.IsFinite(value) && value > 1f)
            {
                errors.Add($"{path}: expected a probability no greater than 1, actual {value}.");
            }
        }
    }

    [Serializable]
    public sealed class OSEnemyDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private OSEnemyArchetype archetype;
        [SerializeField] private GameObject prefab;
        [SerializeField] private float maxHealth;
        [SerializeField] private float moveSpeed;
        [SerializeField] private float contactDamage;
        [SerializeField] private float attackInterval;
        [SerializeField] private OSDropTable dropTable = new();
        [SerializeField] private bool controlAffectsMovement = true;
        [SerializeField] private bool controlAffectsAttack;
        [SerializeField] private int poolCapacity = 1;

        public string Id => id;
        public OSEnemyArchetype Archetype => archetype;
        public GameObject Prefab => prefab;
        public float MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float ContactDamage => contactDamage;
        public float AttackInterval => attackInterval;
        public OSDropTable DropTable => dropTable;
        public bool ControlAffectsMovement => controlAffectsMovement;
        public bool ControlAffectsAttack => controlAffectsAttack;
        public int PoolCapacity => poolCapacity;

        internal void CollectValidationErrors(List<string> errors, string path)
        {
            if (prefab == null)
            {
                errors.Add($"{path}.prefab: required prefab is missing.");
            }

            OSValidationUtility.RequireFinitePositive(maxHealth, $"{path}.maxHealth", errors);
            OSValidationUtility.RequireFiniteNonNegative(moveSpeed, $"{path}.moveSpeed", errors);
            OSValidationUtility.RequireFiniteNonNegative(contactDamage, $"{path}.contactDamage", errors);
            OSValidationUtility.RequireFinitePositive(attackInterval, $"{path}.attackInterval", errors);
            OSValidationUtility.RequireAtLeastOne(poolCapacity, $"{path}.poolCapacity", errors);

            if (dropTable == null)
            {
                errors.Add($"{path}.dropTable: drop table is missing.");
            }
            else
            {
                dropTable.CollectValidationErrors(errors, $"{path}.dropTable");
            }
        }
    }

    [CreateAssetMenu(fileName = "OSEncounterBalance", menuName = "Ouroboros/Data/Encounter Balance")]
    public sealed class OSEncounterBalanceData : ScriptableObject, IOSValidatableData
    {
        [SerializeField] private string dataVersion = "step02-v1";
        [SerializeField] private List<OSEnemyDefinition> enemyDefinitions = new();
        [SerializeField] private OSEnemyDefinition eliteDefinition;
        [SerializeField] private OSEnemyDefinition bossDefinition;
        [SerializeField] private int activeEnemyLimit = 180;
        [SerializeField] private int projectileLimit = 120;
        [SerializeField] private int pickupLimit = 256;
        [SerializeField] private int vfxLimit = 160;

        [NonSerialized] private OSDataValidationReport _lastValidationReport;

        public string DataVersion => dataVersion;
        public IReadOnlyList<OSEnemyDefinition> EnemyDefinitions => enemyDefinitions;
        public OSEnemyDefinition EliteDefinition => eliteDefinition;
        public OSEnemyDefinition BossDefinition => bossDefinition;
        public int ActiveEnemyLimit => activeEnemyLimit;
        public int ProjectileLimit => projectileLimit;
        public int PickupLimit => pickupLimit;
        public int VfxLimit => vfxLimit;
        public string LastValidationMessage => _lastValidationReport?.Message ?? string.Empty;

        public OSDataValidationReport Validate()
        {
            var errors = new List<string>();
            CollectValidationErrors(errors, nameof(OSEncounterBalanceData));
            return new OSDataValidationReport(errors);
        }

        public void CollectValidationErrors(List<string> errors, string path)
        {
            OSValidationUtility.RequireVersion(dataVersion, path, errors);
            OSValidationUtility.RequireAtLeastOne(activeEnemyLimit, $"{path}.activeEnemyLimit", errors);
            OSValidationUtility.RequireAtLeastOne(projectileLimit, $"{path}.projectileLimit", errors);
            OSValidationUtility.RequireAtLeastOne(pickupLimit, $"{path}.pickupLimit", errors);
            OSValidationUtility.RequireAtLeastOne(vfxLimit, $"{path}.vfxLimit", errors);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (enemyDefinitions == null)
            {
                errors.Add($"{path}.enemyDefinitions: list is missing.");
            }
            else
            {
                for (var i = 0; i < enemyDefinitions.Count; i++)
                {
                    ValidateEnemy(enemyDefinitions[i], $"{path}.enemyDefinitions[{i}]", ids, errors);
                }
            }

            ValidateEnemy(eliteDefinition, $"{path}.eliteDefinition", ids, errors);
            ValidateEnemy(bossDefinition, $"{path}.bossDefinition", ids, errors);
        }

        internal void CollectEnemyIds(HashSet<string> ids)
        {
            if (enemyDefinitions != null)
            {
                for (var i = 0; i < enemyDefinitions.Count; i++)
                {
                    AddId(enemyDefinitions[i], ids);
                }
            }

            AddId(eliteDefinition, ids);
            AddId(bossDefinition, ids);
        }

        private static void ValidateEnemy(
            OSEnemyDefinition definition,
            string path,
            HashSet<string> ids,
            List<string> errors)
        {
            if (definition == null)
            {
                errors.Add($"{path}: definition is null.");
                return;
            }

            OSValidationUtility.RequireUniqueId(definition.Id, path, ids, errors);
            definition.CollectValidationErrors(errors, path);
        }

        private static void AddId(OSEnemyDefinition definition, HashSet<string> ids)
        {
            if (definition != null && !string.IsNullOrWhiteSpace(definition.Id))
            {
                ids.Add(definition.Id);
            }
        }

        private void OnValidate()
        {
            _lastValidationReport = Validate();
        }
    }
}
