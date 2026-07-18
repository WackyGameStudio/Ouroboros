using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    public sealed class OSBodySegmentView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer roleIconRenderer;
        [SerializeField] private Collider2D bodyHurtbox;
        [SerializeField] private OSCombatTargetIdentity targetIdentity;
        [SerializeField] private Sprite[] roleSprites = new Sprite[4];

        public int StableId { get; private set; }
        public int ChainIndex { get; private set; } = -1;
        public OSBodyRoleType Role { get; private set; }
        public Collider2D BodyHurtbox => bodyHurtbox;

        public void Configure(OSBodyRoleType role, int stableId, int chainIndex)
        {
            ResolveComponents();
            Role = role;
            StableId = stableId;
            ChainIndex = chainIndex;

            var roleIndex = (int)role;
            if (bodyRenderer != null && roleSprites != null &&
                (uint)roleIndex < (uint)roleSprites.Length && roleSprites[roleIndex] != null)
            {
                bodyRenderer.sprite = roleSprites[roleIndex];
            }

            if (roleIconRenderer != null)
            {
                roleIconRenderer.color = RoleColor(role);
            }

            if (bodyHurtbox != null)
            {
                bodyHurtbox.isTrigger = true;
                bodyHurtbox.enabled = true;
            }

            targetIdentity?.Configure(stableId, OSTargetKind.PlayerBody);

            gameObject.SetActive(true);
        }

        public void SetPose(Vector2 position, Vector2 forward)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (forward.sqrMagnitude > 0.000001f)
            {
                transform.rotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);
            }
        }

        public void Deactivate()
        {
            if (bodyHurtbox != null)
            {
                bodyHurtbox.enabled = false;
            }

            StableId = 0;
            ChainIndex = -1;
            Role = default;
            targetIdentity?.Configure(1, OSTargetKind.PlayerBody);
            gameObject.SetActive(false);
        }

        private void ResolveComponents()
        {
            bodyRenderer ??= GetComponentInChildren<SpriteRenderer>(true);
            bodyHurtbox ??= GetComponentInChildren<Collider2D>(true);
            targetIdentity ??= GetComponentInChildren<OSCombatTargetIdentity>(true);
        }

        private static Color32 RoleColor(OSBodyRoleType role)
        {
            return role switch
            {
                OSBodyRoleType.Shield => new Color32(92, 207, 255, 255),
                OSBodyRoleType.Attack => new Color32(255, 103, 92, 255),
                OSBodyRoleType.Laser => new Color32(202, 112, 255, 255),
                OSBodyRoleType.Control => new Color32(95, 231, 165, 255),
                _ => new Color32(255, 255, 255, 255)
            };
        }
    }
}
