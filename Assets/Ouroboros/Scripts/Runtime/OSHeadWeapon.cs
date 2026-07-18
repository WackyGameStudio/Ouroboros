using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    public readonly struct OSHeadFireFeedback
    {
        public OSHeadFireFeedback(
            int attackEventId,
            int projectileRuntimeId,
            int targetRuntimeId,
            Vector2 origin,
            Vector2 direction)
        {
            AttackEventId = attackEventId;
            ProjectileRuntimeId = projectileRuntimeId;
            TargetRuntimeId = targetRuntimeId;
            Origin = origin;
            Direction = direction;
        }

        public int AttackEventId { get; }
        public int ProjectileRuntimeId { get; }
        public int TargetRuntimeId { get; }
        public Vector2 Origin { get; }
        public Vector2 Direction { get; }
    }

    [DefaultExecutionOrder(-400)]
    [DisallowMultipleComponent]
    public sealed class OSHeadWeapon : MonoBehaviour, IOSProjectileFeedbackSink
    {
        private const float DefaultDamage = 10f;
        private const float DefaultFireInterval = 0.5f;
        private const float DefaultRange = 6f;

        [SerializeField] private OSPlayerBalanceData playerBalance;
        [SerializeField] private OSPoolRegistry poolRegistry;
        [SerializeField] private OSEnemyRegistry enemyRegistry;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private Transform firePoint;
        [SerializeField] private string projectilePoolKey = "head_projectile";
        [SerializeField, Min(1)] private int sourceRuntimeId = 1;
        [SerializeField, Min(0)] private int pierce;

        private float _damage = DefaultDamage;
        private float _fireInterval = DefaultFireInterval;
        private float _range = DefaultRange;
        private float _cooldown;
        private int _currentTargetRuntimeId;
        private bool _subscribed;

        public event Action<OSHeadFireFeedback> Fired;
        public event Action<OSDamageEvent> HitConfirmed;

        public float Cooldown => _cooldown;
        public float Damage => _damage;
        public float FireInterval => _fireInterval;
        public float Range => _range;
        public int Pierce => pierce;
        public int CurrentTargetRuntimeId => _currentTargetRuntimeId;
        public int ShotsFired { get; private set; }
        public int HitsConfirmed { get; private set; }
        public int DefeatsConfirmed { get; private set; }

        private void Awake()
        {
            ResolveDefinition();
            firePoint ??= transform;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void FixedUpdate()
        {
            SimulateStep(Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            OSPlayerBalanceData balance,
            OSPoolRegistry pool,
            OSEnemyRegistry registry,
            OSGameSessionController session,
            Transform muzzle)
        {
            Unsubscribe();
            playerBalance = balance;
            poolRegistry = pool;
            enemyRegistry = registry;
            sessionController = session;
            firePoint = muzzle != null ? muzzle : transform;
            ResolveDefinition();
            Subscribe();
        }

        internal void ConfigureForTesting(
            OSPoolRegistry pool,
            OSEnemyRegistry registry,
            OSGameSessionController session,
            Transform muzzle,
            float damage = DefaultDamage,
            float fireInterval = DefaultFireInterval,
            float range = DefaultRange,
            int projectilePierce = 0)
        {
            Unsubscribe();
            playerBalance = null;
            poolRegistry = pool;
            enemyRegistry = registry;
            sessionController = session;
            firePoint = muzzle != null ? muzzle : transform;
            _damage = Mathf.Max(0.01f, damage);
            _fireInterval = Mathf.Max(0.01f, fireInterval);
            _range = Mathf.Max(0.01f, range);
            pierce = Mathf.Max(0, projectilePierce);
            ResetWeaponState(returnProjectiles: false);
            Subscribe();
        }

        internal void SimulateStep(float deltaTime)
        {
            if (poolRegistry == null || enemyRegistry == null || firePoint == null ||
                !float.IsFinite(deltaTime) || deltaTime < 0f ||
                sessionController != null && !sessionController.IsSimulationRunning)
            {
                return;
            }

            _cooldown = Mathf.Max(0f, _cooldown - deltaTime);
            if (_cooldown > 0f)
            {
                return;
            }

            TryFireNow();
        }

        internal bool TryFireNow()
        {
            var origin = (Vector2)firePoint.position;
            var target = enemyRegistry.FindNearestTarget(origin, _range, _currentTargetRuntimeId);
            if (target == null)
            {
                _currentTargetRuntimeId = 0;
                return false;
            }

            _currentTargetRuntimeId = target.RuntimeId;
            var direction = target.Position - origin;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = Vector2.right;
            }

            var rentResult = poolRegistry.Rent(
                projectilePoolKey,
                firePoint.position,
                Quaternion.identity);
            if (!rentResult.IsAccepted || rentResult.Payload is not OSProjectile projectile)
            {
                return false;
            }

            var idResult = poolRegistry.NextAttackEventId();
            if (!idResult.IsAccepted)
            {
                projectile.ReturnToPool();
                return false;
            }

            var launchResult = projectile.Launch(
                idResult.Payload,
                sourceRuntimeId,
                direction,
                _damage,
                _range,
                pierce,
                this);
            if (!launchResult.IsAccepted)
            {
                projectile.ReturnToPool();
                return false;
            }

            _cooldown = _fireInterval;
            ShotsFired++;
            Fired?.Invoke(new OSHeadFireFeedback(
                idResult.Payload,
                projectile.RuntimeId,
                target.RuntimeId,
                origin,
                direction.normalized));
            return true;
        }

        public void OnProjectileHitConfirmed(OSDamageEvent damageEvent, bool targetDefeated)
        {
            HitsConfirmed++;
            if (targetDefeated)
            {
                DefeatsConfirmed++;
            }

            HitConfirmed?.Invoke(damageEvent);
        }

        private void ResolveDefinition()
        {
            if (playerBalance == null)
            {
                return;
            }

            _damage = playerBalance.HeadDamage;
            _fireInterval = playerBalance.HeadFireInterval;
            _range = playerBalance.HeadRange;
        }

        private void Subscribe()
        {
            if (_subscribed || sessionController == null || !isActiveAndEnabled)
            {
                return;
            }

            sessionController.StateChanged += HandleSessionStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged -= HandleSessionStateChanged;
            }

            _subscribed = false;
        }

        private void HandleSessionStateChanged(OSSessionState previous, OSSessionState current)
        {
            if (current == OSSessionState.Boot)
            {
                ResetWeaponState(returnProjectiles: true);
            }
        }

        private void ResetWeaponState(bool returnProjectiles)
        {
            if (returnProjectiles)
            {
                poolRegistry?.ReturnAll(projectilePoolKey);
            }

            _cooldown = 0f;
            _currentTargetRuntimeId = 0;
            ShotsFired = 0;
            HitsConfirmed = 0;
            DefeatsConfirmed = 0;
        }

        private void OnValidate()
        {
            sourceRuntimeId = Mathf.Max(1, sourceRuntimeId);
            pierce = Mathf.Max(0, pierce);
            ResolveDefinition();
        }
    }
}
