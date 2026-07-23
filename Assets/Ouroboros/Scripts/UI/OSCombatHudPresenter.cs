using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ouroboros.UI
{
    /// <summary>
    /// Collects combat HUD changes into one dirty model and commits text once in LateUpdate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OSCombatHudPresenter : MonoBehaviour
    {
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSPlayerHealth playerHealth;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private OSBodyGrowthController bodyGrowth;
        [SerializeField] private OSBodyRoleRegistry roleRegistry;
        [SerializeField] private OSAttackBodyRole attackRole;
        [SerializeField] private OSLaserBodyRole laserRole;
        [SerializeField] private OSControlBodyRole controlRole;
        [SerializeField] private OSShieldBodyRole shieldRole;
        [SerializeField] private OSBodyDashController bodyDashController;
        [SerializeField] private OSBombController bombController;
        [SerializeField] private OSLevelUpController levelUpController;
        [SerializeField] private OSWaveDirector waveDirector;
        [SerializeField] private OSBossEncounterController bossEncounter;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private TMP_Text threatLabel;
        [SerializeField] private Image healthFill;

        private bool _subscribed;
        private bool _dirty = true;
        private int _lastDynamicTick = -1;
        private int _lastRefreshFrame = -1;
        private float _specialEventVisibleUntil;
        private string _specialEventText = string.Empty;

        public int RefreshCount { get; private set; }
        public int LastRefreshFrame => _lastRefreshFrame;
        public string PrimaryText => primaryLabel != null ? primaryLabel.text : string.Empty;
        public string ActionText => actionLabel != null ? actionLabel.text : string.Empty;
        public string ThreatText => threatLabel != null ? threatLabel.text : string.Empty;
        public float HealthFillAmount => healthFill != null ? healthFill.fillAmount : 0f;

        private void OnEnable()
        {
            Subscribe();
            MarkDirty();
        }

        private void Update()
        {
            var dynamicTick = Mathf.FloorToInt(Time.unscaledTime * 10f);
            if (_lastDynamicTick != dynamicTick)
            {
                _lastDynamicTick = dynamicTick;
                MarkDirty();
            }

            if (_specialEventVisibleUntil > 0f && Time.unscaledTime >= _specialEventVisibleUntil)
            {
                _specialEventVisibleUntil = 0f;
                _specialEventText = string.Empty;
                MarkDirty();
            }
        }

        private void LateUpdate()
        {
            if (!_dirty || _lastRefreshFrame == Time.frameCount)
            {
                return;
            }

            RefreshNow();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            OSGameSessionController session,
            OSPlayerHealth health,
            OSBodyChain chain,
            OSBodyGrowthController growth,
            OSBodyRoleRegistry roles,
            OSAttackBodyRole attack,
            OSLaserBodyRole laser,
            OSControlBodyRole control,
            OSShieldBodyRole shield,
            OSBodyDashController bodyDash,
            OSLevelUpController level,
            OSWaveDirector wave,
            OSBossEncounterController boss,
            TMP_Text primary,
            TMP_Text action,
            TMP_Text threat)
        {
            Unsubscribe();
            sessionController = session;
            playerHealth = health;
            bodyChain = chain;
            bodyGrowth = growth;
            roleRegistry = roles;
            attackRole = attack;
            laserRole = laser;
            controlRole = control;
            shieldRole = shield;
            bodyDashController = bodyDash;
            levelUpController = level;
            waveDirector = wave;
            bossEncounter = boss;
            primaryLabel = primary;
            actionLabel = action;
            threatLabel = threat;
            Subscribe();
            MarkDirty();
            if (!Application.isPlaying)
            {
                RefreshNow();
            }
        }

        public void ForceRefreshForTesting()
        {
            MarkDirty();
            RefreshNow();
        }

        public void ConfigureHealthFill(Image fill)
        {
            healthFill = fill;
            MarkDirty();
            if (!Application.isPlaying)
            {
                RefreshNow();
            }
        }

        public void ConfigureBomb(OSBombController bomb)
        {
            Unsubscribe();
            bombController = bomb;
            Subscribe();
            MarkDirty();
        }

        private void RefreshNow()
        {
            _dirty = false;
            _lastRefreshFrame = Time.frameCount;
            RefreshCount++;

            var shieldCount = roleRegistry != null
                ? roleRegistry.GetCount(OSBodyRoleType.Shield)
                : bodyChain?.GetRoleCount(OSBodyRoleType.Shield) ?? 0;
            var attackCount = roleRegistry != null
                ? roleRegistry.GetCount(OSBodyRoleType.Attack)
                : bodyChain?.GetRoleCount(OSBodyRoleType.Attack) ?? 0;
            var laserCount = roleRegistry != null
                ? roleRegistry.GetCount(OSBodyRoleType.Laser)
                : bodyChain?.GetRoleCount(OSBodyRoleType.Laser) ?? 0;
            var controlCount = roleRegistry != null
                ? roleRegistry.GetCount(OSBodyRoleType.Control)
                : bodyChain?.GetRoleCount(OSBodyRoleType.Control) ?? 0;

            if (primaryLabel != null)
            {
                var invulnerability = playerHealth != null && playerHealth.IsAbilityInvulnerable
                    ? "  BOMB INVULN"
                    : playerHealth != null && playerHealth.IsInvulnerable
                        ? $"  INVULN {playerHealth.HitInvulnerabilityRemaining:0.0}s"
                    : string.Empty;
                primaryLabel.text =
                    $"HP  {playerHealth?.CurrentHealth ?? 0f:0}/{playerHealth?.MaxHealth ?? 0f:0}{invulnerability}\n\n" +
                    $"BODY  {bodyChain?.ActiveCount ?? 0}   [O] S{shieldCount}  [>] A{attackCount}  [=] L{laserCount}  [+] C{controlCount}\n" +
                    $"FRAG  {bodyGrowth?.FragmentProgress ?? 0}/{bodyGrowth?.FragmentRequirement ?? 6}   " +
                    $"LEVEL {levelUpController?.Level ?? 1}  XP {levelUpController?.CurrentExperience ?? 0f:0}/{levelUpController?.RequiredExperience ?? 15}";
            }

            if (healthFill != null)
            {
                healthFill.fillAmount = playerHealth != null && playerHealth.MaxHealth > 0f
                    ? Mathf.Clamp01(playerHealth.CurrentHealth / playerHealth.MaxHealth)
                    : Application.isPlaying ? 0f : 1f;
            }

            if (actionLabel != null)
            {
                var dashText = bodyDashController == null
                    ? "DASH OFFLINE"
                    : bodyDashController.IsDashActive
                        ? $"DASHING  {bodyDashController.DashRemaining:0.0}s  |  BODY CONVERGING"
                        : bodyDashController.CooldownRemaining > 0f
                            ? $"DASH COOLDOWN  {bodyDashController.CooldownRemaining:0.0}s"
                            : $"DASH READY [SPACE]  {bodyDashController.Distance:0.0}u / {bodyDashController.Duration:0.0}s";
                var bombText = bombController == null
                    ? "BOMB OFFLINE"
                    : bombController.Phase == OSBombPhase.DrawingCircle
                        ? $"BOMB DRAWING  {bombController.DrawRemaining:0.0}s"
                        : bombController.Phase == OSBombPhase.Gathering
                            ? $"BOMB GATHER  {bombController.GatherRemaining:0.0}s"
                            : bombController.CooldownRemaining > 0f
                                ? $"BOMB COOLDOWN  {bombController.CooldownRemaining:0.0}s"
                                : bodyChain != null &&
                                  bodyChain.ActiveCount >= bombController.MinimumBodyCount
                                    ? $"BOMB READY [B]  DMG {bombController.Damage:0}"
                                    : $"BOMB NEEDS {bombController.MinimumBodyCount} BODY [B]";
                actionLabel.text =
                    $"{dashText}   |   {bombText}\n" +
                    $"SHIELD [O] {shieldRole?.ChargedCount ?? 0}/{shieldCount}   " +
                    $"ATTACK [>] {attackRole?.ShotsFired ?? 0}   LASER [=] {laserRole?.BeamsFired ?? 0}   " +
                    $"CONTROL [+] {controlRole?.ControlsApplied ?? 0}";
            }

            if (threatLabel != null)
            {
                var elapsed = Mathf.Max(0, Mathf.FloorToInt(waveDirector?.ElapsedSeconds ??
                                                            sessionController?.SessionElapsedTime ?? 0f));
                var baseThreat =
                    $"TIME {elapsed / 60:00}:{elapsed % 60:00}   SWARM {waveDirector?.ActiveEnemyCount ?? 0}/" +
                    $"{waveDirector?.CurrentTargetActiveEnemies ?? 0}";
                if (bossEncounter != null && bossEncounter.IsBossActive)
                {
                    var pattern = bossEncounter.ActivePattern == OSBossPattern.None
                        ? "CORE RECOVERY"
                        : $"DANGER  {FormatPattern(bossEncounter.ActivePattern)}  {bossEncounter.TelegraphRemaining:0.0}s";
                    threatLabel.text =
                        $"{baseThreat}   |   CORE HP {bossEncounter.BossHealth:0}  SHIELD {bossEncounter.ShieldHealth:0}  " +
                        $"LIMIT {Mathf.CeilToInt(bossEncounter.TimeRemaining)}s\n{pattern}";
                }
                else
                {
                    threatLabel.text = string.IsNullOrEmpty(_specialEventText)
                        ? baseThreat
                        : $"{baseThreat}\n{_specialEventText}";
                }
            }
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        private void HandleSpecialEvent(OSWaveSpecialEvent specialEvent)
        {
            _specialEventText = specialEvent switch
            {
                OSWaveSpecialEvent.EliteAccelerator => "DANGER  [ELITE] ACCELERATION AURA INBOUND",
                OSWaveSpecialEvent.BossWarning => "DANGER  [BOSS] SWARM CORE SIGNAL  60s",
                OSWaveSpecialEvent.BossSwarmCore => "DANGER  [BOSS] SWARM CORE ENTERED",
                _ => string.Empty
            };
            _specialEventVisibleUntil = Time.unscaledTime +
                                        (specialEvent == OSWaveSpecialEvent.BossWarning ? 8f : 4f);
            MarkDirty();
        }

        private static string FormatPattern(OSBossPattern pattern)
        {
            return pattern switch
            {
                OSBossPattern.FanProjectiles => "FAN VOLLEY",
                OSBossPattern.SwarmSummon => "SWARM PORTALS",
                OSBossPattern.AttractionPulse => "ATTRACTION PULSE",
                OSBossPattern.Shield => "CORE SHIELD",
                _ => "CORE RECOVERY"
            };
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged += HandleStateChanged;
                sessionController.ActiveSelectionChanged += HandleSelectionChanged;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged += HandleHealthChanged;
                playerHealth.HeadDamaged += HandleHeadDamaged;
                playerHealth.Healed += HandleHealed;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentCountChanged += HandleCountChanged;
                bodyChain.SegmentsRemoved += HandleSegmentsRemoved;
            }

            if (bodyGrowth != null)
            {
                bodyGrowth.FragmentProgressChanged += HandleProgressChanged;
                bodyGrowth.RoleConfirmed += HandleRoleConfirmed;
            }

            if (roleRegistry != null)
            {
                roleRegistry.RoleListsChanged += MarkDirty;
            }

            if (shieldRole != null)
            {
                shieldRole.ShieldConsumed += HandleShieldChanged;
                shieldRole.ShieldRecharged += HandleShieldChanged;
            }

            if (bodyDashController != null)
            {
                bodyDashController.DashStarted += HandleBodyDashSnapshot;
                bodyDashController.DashCompleted += HandleBodyDashResolved;
            }

            if (bombController != null)
            {
                bombController.BombStarted += HandleBombSnapshot;
                bombController.BombExploded += HandleBombResolution;
                bombController.BombCompleted += HandleBombResolution;
            }

            if (levelUpController != null)
            {
                levelUpController.ExperienceChanged += HandleExperienceChanged;
                levelUpController.UpgradeApplied += HandleUpgradeApplied;
            }

            if (waveDirector != null)
            {
                waveDirector.SpecialEventTriggered += HandleSpecialEvent;
                waveDirector.EnemySpawned += HandleEnemySpawned;
            }

            if (bossEncounter != null)
            {
                bossEncounter.EncounterStateChanged += MarkDirty;
            }

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
                sessionController.StateChanged -= HandleStateChanged;
                sessionController.ActiveSelectionChanged -= HandleSelectionChanged;
            }

            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= HandleHealthChanged;
                playerHealth.HeadDamaged -= HandleHeadDamaged;
                playerHealth.Healed -= HandleHealed;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentCountChanged -= HandleCountChanged;
                bodyChain.SegmentsRemoved -= HandleSegmentsRemoved;
            }

            if (bodyGrowth != null)
            {
                bodyGrowth.FragmentProgressChanged -= HandleProgressChanged;
                bodyGrowth.RoleConfirmed -= HandleRoleConfirmed;
            }

            if (roleRegistry != null)
            {
                roleRegistry.RoleListsChanged -= MarkDirty;
            }

            if (shieldRole != null)
            {
                shieldRole.ShieldConsumed -= HandleShieldChanged;
                shieldRole.ShieldRecharged -= HandleShieldChanged;
            }

            if (bodyDashController != null)
            {
                bodyDashController.DashStarted -= HandleBodyDashSnapshot;
                bodyDashController.DashCompleted -= HandleBodyDashResolved;
            }

            if (bombController != null)
            {
                bombController.BombStarted -= HandleBombSnapshot;
                bombController.BombExploded -= HandleBombResolution;
                bombController.BombCompleted -= HandleBombResolution;
            }

            if (levelUpController != null)
            {
                levelUpController.ExperienceChanged -= HandleExperienceChanged;
                levelUpController.UpgradeApplied -= HandleUpgradeApplied;
            }

            if (waveDirector != null)
            {
                waveDirector.SpecialEventTriggered -= HandleSpecialEvent;
                waveDirector.EnemySpawned -= HandleEnemySpawned;
            }

            if (bossEncounter != null)
            {
                bossEncounter.EncounterStateChanged -= MarkDirty;
            }

            _subscribed = false;
        }

        private void HandleStateChanged(OSSessionState previous, OSSessionState current) => MarkDirty();
        private void HandleSelectionChanged(OSSelectionRequest? request) => MarkDirty();
        private void HandleHealthChanged(float current, float maximum) => MarkDirty();
        private void HandleHeadDamaged(OSDamageEvent damageEvent, float remaining) => MarkDirty();
        private void HandleHealed(float amount) => MarkDirty();
        private void HandleCountChanged(int count) => MarkDirty();
        private void HandleSegmentsRemoved(OSBodyRemovalEvent removal) => MarkDirty();
        private void HandleProgressChanged(int progress, int required) => MarkDirty();
        private void HandleRoleConfirmed(OSBodyRoleType role, int stableId) => MarkDirty();
        private void HandleShieldChanged(OSShieldChargeEvent chargeEvent) => MarkDirty();
        private void HandleBodyDashSnapshot(OSBodyDashSnapshot snapshot) => MarkDirty();
        private void HandleBodyDashResolved(OSBodyDashResolution resolution) => MarkDirty();
        private void HandleBombSnapshot(OSBombSnapshot snapshot) => MarkDirty();
        private void HandleBombResolution(OSBombResolution resolution) => MarkDirty();
        private void HandleExperienceChanged(int level, float current, int required) => MarkDirty();
        private void HandleUpgradeApplied(OSUpgradeCandidate candidate, OSUpgradeModifiers modifiers) => MarkDirty();
        private void HandleEnemySpawned(OSEnemyController enemy) => MarkDirty();
    }
}
