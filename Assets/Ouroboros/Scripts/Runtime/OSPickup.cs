using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    /// <summary>
    /// One pooled pickup with amount preservation, magnet movement, and explicit collector validation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
    public sealed class OSPickup : OSPoolableBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private Sprite pickupSprite;
        [SerializeField] private Sprite[] severedBodyRoleSprites = new Sprite[4];
        [SerializeField, Min(0.01f)] private float magnetSpeed = 8f;

        private OSPickupSpawner _spawner;
        private OSGameSessionController _session;
        private Transform _target;
        private float _magnetRadius;

        public OSPickupType PickupType { get; private set; }
        public OSBodyRoleType BodyRole { get; private set; }
        public int Amount { get; private set; }
        public int RegistryIndex { get; internal set; } = -1;
        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;
        public Sprite VisualSprite => bodyRenderer != null ? bodyRenderer.sprite : null;

        private void Awake()
        {
            ResolveComponents();
        }

        private void FixedUpdate()
        {
            SimulateStep(Time.fixedDeltaTime);
        }

        public OSRuleResult<int> AddAmount(int amount)
        {
            if (!IsRented || amount <= 0 || Amount > int.MaxValue - amount)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "pickup.amount.invalid",
                    Amount);
            }

            Amount += amount;
            return OSRuleResult<int>.Accepted(Amount, "pickup.amount.merged");
        }

        internal OSRuleResult<int> RemoveAmount(int amount)
        {
            if (!IsRented || amount <= 0 || amount > Amount)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedRequirement,
                    "pickup.amount.remove_invalid",
                    Amount);
            }

            Amount -= amount;
            return OSRuleResult<int>.Accepted(Amount, "pickup.amount.removed");
        }

        public OSRuleResult<int> TryCollect(OSPickupCollector collector)
        {
            if (!IsRented || collector == null || _spawner == null ||
                _session != null && !_session.IsSimulationRunning)
            {
                return OSRuleResult<int>.Rejected(
                    OSResultCode.RejectedState,
                    "pickup.collect.invalid_state",
                    Amount);
            }

            return _spawner.Collect(this, collector);
        }

        internal void ConfigurePickup(
            OSPickupSpawner spawner,
            OSGameSessionController session,
            Transform target,
            OSPickupType pickupType,
            int amount,
            float magnetRadius)
        {
            _spawner = spawner;
            _session = session;
            _target = target;
            PickupType = pickupType;
            BodyRole = default;
            Amount = Mathf.Max(1, amount);
            _magnetRadius = Mathf.Max(0f, magnetRadius);
            ApplyPickupVisual(pickupType, default);
        }

        internal void ConfigureSeveredBodyPickup(
            OSPickupSpawner spawner,
            OSGameSessionController session,
            Transform target,
            OSBodyRoleType bodyRole,
            int amount,
            float magnetRadius)
        {
            _spawner = spawner;
            _session = session;
            _target = target;
            PickupType = OSPickupType.SeveredBody;
            BodyRole = bodyRole;
            Amount = Mathf.Max(1, amount);
            _magnetRadius = Mathf.Max(0f, magnetRadius);
            ApplyPickupVisual(PickupType, bodyRole);
        }

        internal void SetMagnetRadius(float radius)
        {
            _magnetRadius = Mathf.Max(0f, radius);
        }

        internal void SimulateStep(float deltaTime)
        {
            if (!IsRented || _target == null || !float.IsFinite(deltaTime) || deltaTime <= 0f ||
                _session != null && !_session.IsSimulationRunning)
            {
                return;
            }

            var offset = (Vector2)_target.position - Position;
            if (offset.sqrMagnitude > _magnetRadius * _magnetRadius || offset.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            body.MovePosition(Vector2.MoveTowards(Position, _target.position, magnetSpeed * deltaTime));
        }

        protected override void OnRented()
        {
            ResolveComponents();
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            RegistryIndex = -1;
            Amount = 0;
        }

        protected override void OnReturning()
        {
            _spawner?.Unregister(this);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            _spawner = null;
            _session = null;
            _target = null;
            PickupType = default;
            BodyRole = default;
            Amount = 0;
            _magnetRadius = 0f;
            RegistryIndex = -1;
            if (bodyRenderer != null)
            {
                bodyRenderer.sprite = pickupSprite;
                bodyRenderer.color = Color.white;
            }
        }

        private void ResolveComponents()
        {
            body ??= GetComponent<Rigidbody2D>();
            bodyRenderer ??= GetComponent<SpriteRenderer>();
            if (pickupSprite == null && bodyRenderer != null)
            {
                pickupSprite = bodyRenderer.sprite;
            }

            var collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        private void ApplyPickupVisual(OSPickupType pickupType, OSBodyRoleType bodyRole)
        {
            if (bodyRenderer == null)
            {
                return;
            }

            if (pickupType == OSPickupType.SeveredBody)
            {
                var roleIndex = (int)bodyRole;
                bodyRenderer.sprite = severedBodyRoleSprites != null &&
                                      (uint)roleIndex < (uint)severedBodyRoleSprites.Length &&
                                      severedBodyRoleSprites[roleIndex] != null
                    ? severedBodyRoleSprites[roleIndex]
                    : pickupSprite;
                bodyRenderer.color = bodyRenderer.sprite == pickupSprite
                    ? BodyRoleColor(bodyRole)
                    : Color.white;
                return;
            }

            bodyRenderer.sprite = pickupSprite;
            bodyRenderer.color = pickupType switch
            {
                OSPickupType.Experience => new Color32(255, 220, 76, 255),
                OSPickupType.Heal => new Color32(76, 255, 176, 255),
                _ => new Color32(109, 255, 211, 255)
            };
        }

        private static Color32 BodyRoleColor(OSBodyRoleType role)
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

        private void OnValidate()
        {
            magnetSpeed = Mathf.Max(0.01f, magnetSpeed);
        }
    }
}
