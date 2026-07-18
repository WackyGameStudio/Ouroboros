using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    /// <summary>
    /// Marks the player-head trigger that is allowed to collect pickups.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class OSPickupCollector : MonoBehaviour
    {
        [SerializeField, Min(1)] private int ownerRuntimeId = 1;

        public int OwnerRuntimeId => ownerRuntimeId;
        public Transform CollectionTarget => transform.parent != null ? transform.parent : transform;

        private void Awake()
        {
            var collider = GetComponent<Collider2D>();
            collider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var pickup = other.GetComponentInParent<OSPickup>();
            pickup?.TryCollect(this);
        }

        private void OnValidate()
        {
            ownerRuntimeId = Mathf.Max(1, ownerRuntimeId);
            var collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }
    }
}
