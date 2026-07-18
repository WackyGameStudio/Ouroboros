using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros.Core
{
    [CreateAssetMenu(fileName = "OSPlayerBalance", menuName = "Ouroboros/Data/Player Balance")]
    public sealed class OSPlayerBalanceData : ScriptableObject, IOSValidatableData
    {
        [SerializeField] private string dataVersion = "step02-v1";
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float moveSpeed = 5.5f;
        [SerializeField] private float hitInvulnerability = 0.6f;
        [SerializeField] private float headDamage = 10f;
        [SerializeField] private float headFireInterval = 0.5f;
        [SerializeField] private float headRange = 6f;
        [SerializeField] private float magnetRadius = 1.25f;

        [NonSerialized] private OSDataValidationReport _lastValidationReport;

        public string DataVersion => dataVersion;
        public int MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float HitInvulnerability => hitInvulnerability;
        public float HeadDamage => headDamage;
        public float HeadFireInterval => headFireInterval;
        public float HeadRange => headRange;
        public float MagnetRadius => magnetRadius;
        public string LastValidationMessage => _lastValidationReport?.Message ?? string.Empty;

        public OSDataValidationReport Validate()
        {
            var errors = new List<string>();
            CollectValidationErrors(errors, nameof(OSPlayerBalanceData));
            return new OSDataValidationReport(errors);
        }

        public void CollectValidationErrors(List<string> errors, string path)
        {
            OSValidationUtility.RequireVersion(dataVersion, path, errors);
            OSValidationUtility.RequireAtLeastOne(maxHealth, $"{path}.maxHealth", errors);
            OSValidationUtility.RequireFinitePositive(moveSpeed, $"{path}.moveSpeed", errors);
            OSValidationUtility.RequireFiniteNonNegative(
                hitInvulnerability,
                $"{path}.hitInvulnerability",
                errors);
            OSValidationUtility.RequireFinitePositive(headDamage, $"{path}.headDamage", errors);
            OSValidationUtility.RequireFinitePositive(headFireInterval, $"{path}.headFireInterval", errors);
            OSValidationUtility.RequireFinitePositive(headRange, $"{path}.headRange", errors);
            OSValidationUtility.RequireFiniteNonNegative(magnetRadius, $"{path}.magnetRadius", errors);
        }

        private void OnValidate()
        {
            _lastValidationReport = Validate();
        }
    }
}
