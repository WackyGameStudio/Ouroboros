using TMPro;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    public sealed class OSCombatDebugPresenter : MonoBehaviour
    {
        [SerializeField] private OSHeadWeapon headWeapon;
        [SerializeField] private OSEnemyRegistry enemyRegistry;
        [SerializeField] private TMP_Text label;

        public void Configure(OSHeadWeapon weapon, OSEnemyRegistry registry, TMP_Text targetLabel)
        {
            headWeapon = weapon;
            enemyRegistry = registry;
            label = targetLabel;
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (label == null || headWeapon == null || enemyRegistry == null)
            {
                return;
            }

            label.text =
                $"STEP 07 AUTO FIRE  |  ENEMY {enemyRegistry.Count}  |  " +
                $"SHOT {headWeapon.ShotsFired}  HIT {headWeapon.HitsConfirmed}  " +
                $"KILL {headWeapon.DefeatsConfirmed}";
        }
    }
}
