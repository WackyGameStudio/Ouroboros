using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSBossPresenter : MonoBehaviour
    {
        [SerializeField] private OSBossEncounterController bossEncounter;
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private TMP_Text patternLabel;

        private bool _subscribed;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            OSBossEncounterController encounter,
            TMP_Text health,
            TMP_Text pattern)
        {
            Unsubscribe();
            bossEncounter = encounter;
            healthLabel = health;
            patternLabel = pattern;
            Subscribe();
            Refresh();
        }

        private void Refresh()
        {
            if (healthLabel == null || patternLabel == null)
            {
                return;
            }

            if (bossEncounter == null || !bossEncounter.IsBossActive)
            {
                healthLabel.text = string.Empty;
                patternLabel.text = string.Empty;
                return;
            }

            healthLabel.text =
                $"SWARM CORE  |  HP {bossEncounter.BossHealth:0}/{bossEncounter.BossMaxHealth:0}  |  " +
                $"SHIELD {bossEncounter.ShieldHealth:0}/{bossEncounter.ShieldMaxHealth:0}  |  " +
                $"LIMIT {Mathf.CeilToInt(bossEncounter.TimeRemaining)}s";

            var pattern = bossEncounter.ActivePattern;
            patternLabel.text = pattern == OSBossPattern.None
                ? $"{FormatPhase(bossEncounter.Phase)}  |  CORE PATTERN RECOVERY"
                : $"DANGER  |  {FormatPattern(pattern)}  |  {bossEncounter.TelegraphRemaining:0.00}s";
        }

        private static string FormatPhase(OSBossPhase phase)
        {
            return phase switch
            {
                OSBossPhase.PhaseOne => "PHASE I",
                OSBossPhase.PhaseTwo => "PHASE II",
                OSBossPhase.PhaseThree => "PHASE III",
                _ => "PHASE I"
            };
        }

        private static string FormatPattern(OSBossPattern pattern)
        {
            return pattern switch
            {
                OSBossPattern.FanProjectiles => "FAN VOLLEY",
                OSBossPattern.SwarmSummon => "SWARM PORTALS",
                OSBossPattern.AttractionPulse => "ATTRACTION PULSE",
                OSBossPattern.Shield => "CORE SHIELD",
                _ => "RECOVERY"
            };
        }

        private void Subscribe()
        {
            if (_subscribed || bossEncounter == null || !isActiveAndEnabled)
            {
                return;
            }

            bossEncounter.EncounterStateChanged += Refresh;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && bossEncounter != null)
            {
                bossEncounter.EncounterStateChanged -= Refresh;
            }

            _subscribed = false;
        }
    }
}
