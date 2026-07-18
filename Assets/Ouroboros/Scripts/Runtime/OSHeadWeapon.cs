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
        private const float DefaultBodyDamageRate = 0.04f;
        private const float AuxiliaryAngleStep = 4f;

        [SerializeField] private OSPlayerBalanceData playerBalance;
        [SerializeField] private OSBodyBalanceData bodyBalance;
        [SerializeField] private OSBodyChain bodyChain;
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
        private float _bodyDamageRate = DefaultBodyDamageRate;
        private OSUpgradeModifiers _upgradeModifiers = OSUpgradeModifiers.Default;
        private float _cooldown;
        private int _currentTargetRuntimeId;
        private bool _subscribed;

        public event Action<OSHeadFireFeedback> Fired;
        public event Action<OSDamageEvent> HitConfirmed;

        public float Cooldown => _cooldown;
        public float Damage => CalculateVolleyDamage();
        public float FireInterval => OSUpgradeMath.CalculateHeadFireInterval(
            _fireInterval,
            _upgradeModifiers.HeadRateBonus);
        public float Range => _range;
        public int Pierce => Mathf.Max(0, pierce + _upgradeModifiers.HeadPierceBonus);
        public bool ElitePriority => _upgradeModifiers.ElitePriority;
        public int CurrentTargetRuntimeId => _currentTargetRuntimeId;
        public int ProjectileCountPerVolley => 1 + ((bodyChain != null ? bodyChain.ActiveCount : 0) / 5);
        public int LastVolleyProjectileCount { get; private set; }
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
            Transform muzzle,
            OSBodyChain chain = null,
            OSBodyBalanceData bodyData = null)
        {
            Unsubscribe();
            playerBalance = balance;
            poolRegistry = pool;
            enemyRegistry = registry;
            sessionController = session;
            firePoint = muzzle != null ? muzzle : transform;
            bodyChain = chain;
            bodyBalance = bodyData;
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
            int projectilePierce = 0,
            OSBodyChain chain = null,
            float bodyDamageRate = DefaultBodyDamageRate)
        {
            Unsubscribe();
            playerBalance = null;
            poolRegistry = pool;
            enemyRegistry = registry;
            sessionController = session;
            firePoint = muzzle != null ? muzzle : transform;
            bodyChain = chain;
            bodyBalance = null;
            _damage = Mathf.Max(0.01f, damage);
            _fireInterval = Mathf.Max(0.01f, fireInterval);
            _range = Mathf.Max(0.01f, range);
            _bodyDamageRate = Mathf.Max(0f, bodyDamageRate);
            pierce = Mathf.Max(0, projectilePierce);
            _upgradeModifiers = OSUpgradeModifiers.Default;
            ResetWeaponState(returnProjectiles: false);
            Subscribe();
        }

        public void ApplyUpgradeModifiers(OSUpgradeModifiers modifiers)
        {
            _upgradeModifiers = modifiers;
            _cooldown = Mathf.Min(_cooldown, FireInterval);
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
            var target = enemyRegistry.FindNearestTarget(
                origin,
                _range,
                _currentTargetRuntimeId,
                _upgradeModifiers.ElitePriority);
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

            var projectileCount = ProjectileCountPerVolley;
            var damage = CalculateVolleyDamage();
            var firedCount = 0;
            for (var index = 0; index < projectileCount; index++)
            {
                var angleOffset = (index - ((projectileCount - 1) * 0.5f)) * AuxiliaryAngleStep;
                var shotDirection = Quaternion.Euler(0f, 0f, angleOffset) * direction;
                if (!TryLaunchProjectile(target, origin, shotDirection, damage))
                {
                    break;
                }

                firedCount++;
            }

            if (firedCount <= 0)
            {
                LastVolleyProjectileCount = 0;
                return false;
            }

            _cooldown = FireInterval;
            LastVolleyProjectileCount = firedCount;
            ShotsFired += firedCount;
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
            _bodyDamageRate = bodyBalance != null ? bodyBalance.BodyDamageRate : DefaultBodyDamageRate;
        }

        private bool TryLaunchProjectile(
            OSEnemyController target,
            Vector2 origin,
            Vector2 direction,
            float damage)
        {
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
                damage,
                _range,
                Pierce,
                this);
            if (!launchResult.IsAccepted)
            {
                projectile.ReturnToPool();
                return false;
            }

            Fired?.Invoke(new OSHeadFireFeedback(
                idResult.Payload,
                projectile.RuntimeId,
                target.RuntimeId,
                origin,
                direction.normalized));
            return true;
        }

        private float CalculateVolleyDamage()
        {
            var length = bodyChain != null ? bodyChain.ActiveCount : 0;
            var perSegmentRate = Mathf.Min(0.06f, _bodyDamageRate + _upgradeModifiers.BodyDamageRateBonus);
            return _damage * _upgradeModifiers.HeadDamageMultiplier *
                   (1f + (length * perSegmentRate));
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
            LastVolleyProjectileCount = 0;
        }

        private void OnValidate()
        {
            sourceRuntimeId = Mathf.Max(1, sourceRuntimeId);
            pierce = Mathf.Max(0, pierce);
            ResolveDefinition();
        }
    }
}
