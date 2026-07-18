using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSWavePresenter : MonoBehaviour
    {
        [SerializeField] private OSWaveDirector waveDirector;
        [SerializeField] private TMP_Text waveLabel;
        [SerializeField] private TMP_Text eventLabel;

        private float _eventVisibleUntil;
        private bool _subscribed;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void Update()
        {
            Refresh();
            if (eventLabel != null && Time.unscaledTime >= _eventVisibleUntil)
            {
                eventLabel.text = string.Empty;
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(OSWaveDirector director, TMP_Text status, TMP_Text specialEvent)
        {
            Unsubscribe();
            waveDirector = director;
            waveLabel = status;
            eventLabel = specialEvent;
            Subscribe();
            Refresh();
        }

        private void Refresh()
        {
            if (waveDirector == null || waveLabel == null)
            {
                return;
            }

            var elapsed = Mathf.Max(0, Mathf.FloorToInt(waveDirector.ElapsedSeconds));
            waveLabel.text =
                $"TIME {elapsed / 60:00}:{elapsed % 60:00}  |  SWARM {waveDirector.ActiveEnemyCount}/" +
                $"{waveDirector.CurrentTargetActiveEnemies}  |  CAP {waveDirector.ActiveEnemyLimit}  |  " +
                GetNextUnlock(waveDirector.ElapsedSeconds);
        }

        private void HandleSpecialEvent(OSWaveSpecialEvent specialEvent)
        {
            if (eventLabel == null)
            {
                return;
            }

            eventLabel.text = specialEvent switch
            {
                OSWaveSpecialEvent.EliteAccelerator => "ELITE ACCELERATOR INBOUND  |  AURA 4.5m",
                OSWaveSpecialEvent.BossWarning => "WARNING  |  SWARM CORE SIGNAL DETECTED  |  60s",
                OSWaveSpecialEvent.BossSwarmCore => "SWARM CORE HAS ENTERED THE ARENA",
                _ => string.Empty
            };
            _eventVisibleUntil = Time.unscaledTime + (specialEvent == OSWaveSpecialEvent.BossWarning ? 8f : 4f);
        }

        private static string GetNextUnlock(float elapsed)
        {
            if (elapsed < 60f)
            {
                return $"CHARGER IN {Mathf.CeilToInt(60f - elapsed)}s";
            }

            if (elapsed < 120f)
            {
                return $"SHOOTER IN {Mathf.CeilToInt(120f - elapsed)}s";
            }

            if (elapsed < 180f)
            {
                return $"ELITE IN {Mathf.CeilToInt(180f - elapsed)}s";
            }

            if (elapsed < 240f)
            {
                return $"SPLITTER IN {Mathf.CeilToInt(240f - elapsed)}s";
            }

            if (elapsed < 360f)
            {
                return $"ELITE II IN {Mathf.CeilToInt(360f - elapsed)}s";
            }

            if (elapsed < 540f)
            {
                return $"CORE WARNING IN {Mathf.CeilToInt(540f - elapsed)}s";
            }

            return elapsed < 600f
                ? $"CORE SIGNAL IN {Mathf.CeilToInt(600f - elapsed)}s"
                : "SWARM CORE ACTIVE";
        }

        private void Subscribe()
        {
            if (_subscribed || waveDirector == null || !isActiveAndEnabled)
            {
                return;
            }

            waveDirector.SpecialEventTriggered += HandleSpecialEvent;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (_subscribed && waveDirector != null)
            {
                waveDirector.SpecialEventTriggered -= HandleSpecialEvent;
            }

            _subscribed = false;
        }
    }
}
