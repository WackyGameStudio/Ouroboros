using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ouroboros.Core
{
    [Serializable]
    public sealed class OSRoleVisualDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private OSBodyRoleType roleType;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private string patternKey;

        public string Id => id;
        public OSBodyRoleType RoleType => roleType;
        public Sprite Sprite => sprite;
        public Color Color => color;
        public string PatternKey => patternKey;
    }

    [CreateAssetMenu(fileName = "OSFeedbackCatalog", menuName = "Ouroboros/Data/Feedback Catalog")]
    public sealed class OSFeedbackCatalog : ScriptableObject, IOSValidatableData
    {
        [SerializeField] private string dataVersion = "step02-v1";
        [SerializeField] private List<OSRoleVisualDefinition> roleVisuals = new();
        [SerializeField] private List<string> attackVfxKeys = new();
        [SerializeField] private List<string> telegraphKeys = new();
        [SerializeField] private List<string> audioKeys = new();

        [NonSerialized] private OSDataValidationReport _lastValidationReport;

        public string DataVersion => dataVersion;
        public IReadOnlyList<OSRoleVisualDefinition> RoleVisuals => roleVisuals;
        public IReadOnlyList<string> AttackVfxKeys => attackVfxKeys;
        public IReadOnlyList<string> TelegraphKeys => telegraphKeys;
        public IReadOnlyList<string> AudioKeys => audioKeys;
        public string LastValidationMessage => _lastValidationReport?.Message ?? string.Empty;

        public OSDataValidationReport Validate()
        {
            var errors = new List<string>();
            CollectValidationErrors(errors, nameof(OSFeedbackCatalog));
            return new OSDataValidationReport(errors);
        }

        public void CollectValidationErrors(List<string> errors, string path)
        {
            OSValidationUtility.RequireVersion(dataVersion, path, errors);
            ValidateRoleVisuals(errors, path);
            ValidateKeyList(attackVfxKeys, $"{path}.attackVfxKeys", errors);
            ValidateKeyList(telegraphKeys, $"{path}.telegraphKeys", errors);
            ValidateKeyList(audioKeys, $"{path}.audioKeys", errors);
        }

        private void ValidateRoleVisuals(List<string> errors, string path)
        {
            if (roleVisuals == null)
            {
                errors.Add($"{path}.roleVisuals: list is missing.");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var roles = new HashSet<OSBodyRoleType>();
            for (var i = 0; i < roleVisuals.Count; i++)
            {
                var visual = roleVisuals[i];
                var itemPath = $"{path}.roleVisuals[{i}]";
                if (visual == null)
                {
                    errors.Add($"{itemPath}: definition is null.");
                    continue;
                }

                OSValidationUtility.RequireUniqueId(visual.Id, itemPath, ids, errors);
                if (!roles.Add(visual.RoleType))
                {
                    errors.Add($"{itemPath}: duplicate role type '{visual.RoleType}'.");
                }

                if (visual.Sprite == null)
                {
                    errors.Add($"{itemPath}.sprite: required sprite is missing.");
                }

                OSValidationUtility.RequireId(visual.PatternKey, $"{itemPath}.patternKey", errors);
            }

            foreach (OSBodyRoleType role in Enum.GetValues(typeof(OSBodyRoleType)))
            {
                if (!roles.Contains(role))
                {
                    errors.Add($"{path}.roleVisuals: required role '{role}' is missing.");
                }
            }
        }

        private static void ValidateKeyList(List<string> values, string path, List<string> errors)
        {
            if (values == null || values.Count == 0)
            {
                errors.Add($"{path}: at least one key is required.");
                return;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < values.Count; i++)
            {
                OSValidationUtility.RequireUniqueId(values[i], $"{path}[{i}]", keys, errors);
            }
        }

        private void OnValidate()
        {
            _lastValidationReport = Validate();
        }
    }
}
