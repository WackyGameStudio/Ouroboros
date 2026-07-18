using System;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(OSEnemyController))]
    public sealed class OSBossController : MonoBehaviour
    {
        private const float ShieldCapacity = 600f;
        private const float FanTelegraphSeconds = 0.6f;
        private const float SummonTelegraphSeconds = 0.8f;
        private const float PullTelegraphSeconds = 0.8f;
        private const float ShieldTelegraphSeconds = 0.5f;
        private const float PullDistance = 2.2f;
        private const float ProjectileRange = 16f;
        private const float ProjectileDamage = 14f;
        private const int ProjectileLimit = 120;
        private const string EnemyProjectileKey = "enemy_projectile";

        [SerializeField] private OSEnemyController enemy;
        [SerializeField] private LineRenderer patternTelegraph;
        [SerializeField] private LineRenderer shieldRing;

        private OSGameSessionController _session;
        private OSPoolRegistry _pools;
        private OSWaveDirector _waveDirector;
        private OSPlayerController _player;
        private float _patternCooldown;
        private float _telegraphRemaining;
        private int _patternSequence;

        public event Action StateChanged;

        public OSBossPhase Phase => enemy != null
            ? OSBossMath.GetPhase(enemy.CurrentHealth, enemy.MaxHealth)
            : OSBossPhase.PhaseOne;
        public OSBossPattern ActivePattern { get; private set; }
        public float TelegraphRemaining => _telegraphRemaining;
        public float ShieldHealth { get; private set; }
        public float ShieldMaxHealth => ShieldCapacity;
        public int FanCastCount { get; private set; }
        public int SummonCastCount { get; private set; }
        public int PullCastCount { get; private set; }
        public int ShieldCastCount { get; private set; }
        public int ActiveHighRiskTelegraphCount => ActivePattern == OSBossPattern.None ? 0 : 1;

        private void Awake()
        {
            ResolveReferences();
            SetPatternTelegraphVisible(false);
            RefreshShieldVisual();
        }

        private void FixedUpdate()
        {
            SimulateStep(Time.fixedDeltaTime);
        }

        public void ConfigureRuntime(
            OSGameSessionController session,
            OSPoolRegistry pools,
            OSWaveDirector waveDirector,
            OSPlayerController player)
        {
            _session = session;
            _pools = pools;
            _waveDirector = waveDirector;
            _player = player;
            ResolveReferences();
        }

        internal void HandleRented()
        {
            ShieldHealth = 0f;
            ActivePattern = OSBossPattern.None;
            _telegraphRemaining = 0f;
            _patternCooldown = 2f;
            _patternSequence = 0;
            FanCastCount = 0;
            SummonCastCount = 0;
            PullCastCount = 0;
            ShieldCastCount = 0;
            SetPatternTelegraphVisible(false);
            RefreshShieldVisual();
            StateChanged?.Invoke();
        }

        internal void HandleReturning()
        {
            ActivePattern = OSBossPattern.None;
            _telegraphRemaining = 0f;
            ShieldHealth = 0f;
            SetPatternTelegraphVisible(false);
            RefreshShieldVisual();
            StateChanged?.Invoke();
        }

        public float AbsorbIncomingDamage(float damage)
        {
            var resolution = OSBossMath.ResolveShieldDamage(ShieldHealth, damage);
            ShieldHealth = resolution.RemainingShield;
            RefreshShieldVisual();
            StateChanged?.Invoke();
            return resolution.HealthDamage;
        }

        internal void SimulateStep(float deltaTime)
        {
            if (enemy == null || !enemy.IsRented || enemy.IsDeathConfirmed ||
                _session != null && !_session.IsSimulationRunning ||
                !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            if (ActivePattern != OSBossPattern.None)
            {
                _telegraphRemaining = Mathf.Max(0f, _telegraphRemaining - deltaTime);
                if (_telegraphRemaining <= 0f)
                {
                    ResolveActivePattern();
                }

                StateChanged?.Invoke();
                return;
            }

            _patternCooldown = Mathf.Max(0f, _patternCooldown - deltaTime);
            if (_patternCooldown <= 0f)
            {
                BeginPattern(SelectNextPattern());
            }
        }

        internal void BeginPatternForTesting(OSBossPattern pattern)
        {
            BeginPattern(pattern);
        }

        internal void ResolvePatternForTesting()
        {
            ResolveActivePattern();
        }

        internal void ActivateShieldForTesting()
        {
            ShieldHealth = ShieldCapacity;
            RefreshShieldVisual();
            StateChanged?.Invoke();
        }

        private OSBossPattern SelectNextPattern()
        {
            var phase = Phase;
            var sequenceIndex = _patternSequence++;
            var pattern = phase switch
            {
                OSBossPhase.PhaseOne => sequenceIndex % 2 == 0
                    ? OSBossPattern.FanProjectiles
                    : OSBossPattern.SwarmSummon,
                OSBossPhase.PhaseTwo => (sequenceIndex % 3) switch
                {
                    0 => OSBossPattern.FanProjectiles,
                    1 => OSBossPattern.AttractionPulse,
                    _ => OSBossPattern.SwarmSummon
                },
                _ => (sequenceIndex % 4) switch
                {
                    0 => OSBossPattern.Shield,
                    1 => OSBossPattern.FanProjectiles,
                    2 => OSBossPattern.SwarmSummon,
                    _ => OSBossPattern.AttractionPulse
                }
            };
            if (pattern == OSBossPattern.Shield && ShieldHealth > 0f)
            {
                pattern = OSBossPattern.FanProjectiles;
            }

            return pattern;
        }

        private void BeginPattern(OSBossPattern pattern)
        {
            if (pattern == OSBossPattern.None || enemy == null || !enemy.IsRented)
            {
                return;
            }

            ActivePattern = pattern;
            _telegraphRemaining = pattern switch
            {
                OSBossPattern.FanProjectiles => FanTelegraphSeconds,
                OSBossPattern.SwarmSummon => SummonTelegraphSeconds,
                OSBossPattern.AttractionPulse => PullTelegraphSeconds,
                OSBossPattern.Shield => ShieldTelegraphSeconds,
                _ => 0f
            };
            ConfigurePatternTelegraph(pattern);
            StateChanged?.Invoke();
        }

        private void ResolveActivePattern()
        {
            if (ActivePattern == OSBossPattern.None)
            {
                return;
            }

            var resolvedPattern = ActivePattern;
            switch (resolvedPattern)
            {
                case OSBossPattern.FanProjectiles:
                    FireFanProjectiles();
                    FanCastCount++;
                    break;
                case OSBossPattern.SwarmSummon:
                    SummonSwarm();
                    SummonCastCount++;
                    break;
                case OSBossPattern.AttractionPulse:
                    ApplyAttractionPulse();
                    PullCastCount++;
                    break;
                case OSBossPattern.Shield:
                    ShieldHealth = ShieldCapacity;
                    ShieldCastCount++;
                    RefreshShieldVisual();
                    break;
            }

            ActivePattern = OSBossPattern.None;
            _telegraphRemaining = 0f;
            _patternCooldown = OSBossMath.GetPatternInterval(Phase);
            SetPatternTelegraphVisible(false);
            StateChanged?.Invoke();
        }

        private void FireFanProjectiles()
        {
            if (_pools == null || _player == null || enemy == null)
            {
                return;
            }

            var direction = (Vector2)_player.transform.position - enemy.Position;
            direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.down;
            var count = OSBossMath.GetFanProjectileCount(Phase);
            var halfAngle = Phase == OSBossPhase.PhaseThree ? 42f : 34f;
            for (var index = 0; index < count; index++)
            {
                if (GetActiveProjectileCount() >= ProjectileLimit)
                {
                    break;
                }

                var t = count <= 1 ? 0.5f : index / (float)(count - 1);
                var shotDirection = Quaternion.Euler(0f, 0f, Mathf.Lerp(-halfAngle, halfAngle, t)) * direction;
                var eventId = _pools.NextAttackEventId();
                if (!eventId.IsAccepted)
                {
                    break;
                }

                var rent = _pools.Rent(EnemyProjectileKey, enemy.Position, Quaternion.identity);
                if (!rent.IsAccepted || rent.Payload is not OSEnemyProjectile projectile)
                {
                    break;
                }

                if (!projectile.Launch(
                        eventId.Payload,
                        enemy.RuntimeId,
                        shotDirection,
                        ProjectileDamage,
                        ProjectileRange).IsAccepted)
                {
                    projectile.ReturnToPool();
                }
            }
        }

        private void SummonSwarm()
        {
            if (_waveDirector == null)
            {
                return;
            }

            var count = Phase == OSBossPhase.PhaseThree ? 8 : 6;
            for (var index = 0; index < count; index++)
            {
                var enemyId = index % 3 == 2 ? "enemy_splitter" : "enemy_chaser";
                if (!_waveDirector.TrySpawnBossSummon(enemyId, out _))
                {
                    break;
                }
            }
        }

        private void ApplyAttractionPulse()
        {
            if (_player == null || enemy == null)
            {
                return;
            }

            var offset = enemy.Position - (Vector2)_player.transform.position;
            if (offset.sqrMagnitude > 0.000001f)
            {
                _player.ApplyExternalDisplacement(offset.normalized * PullDistance);
            }
        }

        private int GetActiveProjectileCount()
        {
            return _pools.GetActiveCount("head_projectile") +
                   _pools.GetActiveCount("body_control_projectile") +
                   _pools.GetActiveCount(EnemyProjectileKey);
        }

        private void ConfigurePatternTelegraph(OSBossPattern pattern)
        {
            if (patternTelegraph == null || enemy == null)
            {
                return;
            }

            patternTelegraph.enabled = true;
            patternTelegraph.useWorldSpace = true;
            patternTelegraph.loop = pattern is OSBossPattern.AttractionPulse or OSBossPattern.Shield;
            var color = pattern switch
            {
                OSBossPattern.FanProjectiles => new Color(1f, 0.28f, 0.16f, 0.9f),
                OSBossPattern.SwarmSummon => new Color(0.75f, 0.28f, 1f, 0.9f),
                OSBossPattern.AttractionPulse => new Color(0.2f, 0.9f, 1f, 0.9f),
                OSBossPattern.Shield => new Color(0.2f, 0.55f, 1f, 0.9f),
                _ => Color.white
            };
            patternTelegraph.startColor = color;
            patternTelegraph.endColor = color;

            if (pattern == OSBossPattern.FanProjectiles)
            {
                var targetDirection = _player != null
                    ? ((Vector2)_player.transform.position - enemy.Position).normalized
                    : Vector2.down;
                const int rays = 7;
                patternTelegraph.positionCount = rays * 2 + 1;
                patternTelegraph.SetPosition(0, enemy.Position);
                for (var index = 0; index < rays; index++)
                {
                    var t = index / (float)(rays - 1);
                    var ray = Quaternion.Euler(0f, 0f, Mathf.Lerp(-42f, 42f, t)) * targetDirection;
                    patternTelegraph.SetPosition(index * 2 + 1, (Vector3)enemy.Position + ray * 7f);
                    patternTelegraph.SetPosition(index * 2 + 2, enemy.Position);
                }
            }
            else
            {
                var radius = pattern == OSBossPattern.SwarmSummon ? 3.5f : 5f;
                patternTelegraph.positionCount = 64;
                for (var index = 0; index < 64; index++)
                {
                    var angle = index * Mathf.PI * 2f / 64f;
                    patternTelegraph.SetPosition(
                        index,
                        enemy.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }
        }

        private void SetPatternTelegraphVisible(bool visible)
        {
            if (patternTelegraph != null)
            {
                patternTelegraph.enabled = visible;
            }
        }

        private void RefreshShieldVisual()
        {
            if (shieldRing != null)
            {
                shieldRing.enabled = ShieldHealth > 0f;
            }
        }

        private void ResolveReferences()
        {
            enemy ??= GetComponent<OSEnemyController>();
        }
    }
}
