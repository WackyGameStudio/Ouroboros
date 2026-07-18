using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class OSEnemyContactHitbox : MonoBehaviour
    {
        [SerializeField] private OSEnemyController owner;
        private OSCombatTargetIdentity _contactTarget;

        public void Configure(OSEnemyController controller)
        {
            owner = controller;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_contactTarget != null || owner == null)
            {
                return;
            }

            var identity = other.GetComponentInParent<OSCombatTargetIdentity>();
            if (identity == null)
            {
                return;
            }

            _contactTarget = identity;
            owner.BeginContact(identity.RuntimeId, identity.TargetKind, identity.transform);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_contactTarget == null || other.GetComponentInParent<OSCombatTargetIdentity>() != _contactTarget)
            {
                return;
            }

            _contactTarget = null;
            owner?.EndContact();
        }

        private void OnDisable()
        {
            _contactTarget = null;
            owner?.EndContact();
        }
    }
}
