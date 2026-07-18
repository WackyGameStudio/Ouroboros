using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class OSEnemyContactHitbox : MonoBehaviour
    {
        [SerializeField] private OSEnemyController owner;

        public void Configure(OSEnemyController controller)
        {
            owner = controller;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (owner == null)
            {
                return;
            }

            var identity = other.GetComponentInParent<OSCombatTargetIdentity>();
            if (identity == null)
            {
                return;
            }

            owner.BeginContact(identity.RuntimeId, identity.TargetKind, identity.transform);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (owner == null)
            {
                return;
            }

            var identity = other.GetComponentInParent<OSCombatTargetIdentity>();
            if (identity != null)
            {
                owner.EndContact(identity.RuntimeId, identity.TargetKind);
            }
        }

        private void OnDisable()
        {
            owner?.EndContact();
        }
    }
}
