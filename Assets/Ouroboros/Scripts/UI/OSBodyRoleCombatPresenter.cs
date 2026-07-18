using Ouroboros.Runtime;
using TMPro;
using UnityEngine;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSBodyRoleCombatPresenter : MonoBehaviour
    {
        [SerializeField] private OSAttackBodyRole attackRole;
        [SerializeField] private OSLaserBodyRole laserRole;
        [SerializeField] private OSControlBodyRole controlRole;
        [SerializeField] private OSShieldBodyRole shieldRole;
        [SerializeField] private TMP_Text statusLabel;

        private int _lastAttackCount = -1;
        private int _lastAttackShots = -1;
        private int _lastLaserCount = -1;
        private int _lastLaserShots = -1;
        private int _lastControlCount = -1;
        private int _lastControlShots = -1;
        private int _lastShieldCount = -1;
        private int _lastChargedShields = -1;

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        public void Configure(
            OSAttackBodyRole attack,
            OSLaserBodyRole laser,
            OSControlBodyRole control,
            OSShieldBodyRole shield,
            TMP_Text label)
        {
            attackRole = attack;
            laserRole = laser;
            controlRole = control;
            shieldRole = shield;
            statusLabel = label;
            ResetCache();
            Refresh();
        }

        private void Refresh()
        {
            if (statusLabel == null || attackRole == null || laserRole == null ||
                controlRole == null || shieldRole == null)
            {
                return;
            }

            var attackCount = attackRole.ActiveSegmentCount;
            var attackShots = attackRole.ShotsFired;
            var laserCount = laserRole.ActiveSegmentCount;
            var laserShots = laserRole.BeamsFired;
            var controlCount = controlRole.ActiveSegmentCount;
            var controlShots = controlRole.ControlsApplied;
            var shieldCount = shieldRole.ActiveSegmentCount;
            var chargedShields = shieldRole.ChargedCount;
            if (_lastAttackCount == attackCount && _lastAttackShots == attackShots &&
                _lastLaserCount == laserCount && _lastLaserShots == laserShots &&
                _lastControlCount == controlCount && _lastControlShots == controlShots &&
                _lastShieldCount == shieldCount && _lastChargedShields == chargedShields)
            {
                return;
            }

            _lastAttackCount = attackCount;
            _lastAttackShots = attackShots;
            _lastLaserCount = laserCount;
            _lastLaserShots = laserShots;
            _lastControlCount = controlCount;
            _lastControlShots = controlShots;
            _lastShieldCount = shieldCount;
            _lastChargedShields = chargedShields;
            statusLabel.text =
                $"ROLE FX  |  A {attackCount}:{attackShots}  " +
                $"L {laserCount}:{laserShots}  C {controlCount}:{controlShots}  " +
                $"S {chargedShields}/{shieldCount}";
        }

        private void ResetCache()
        {
            _lastAttackCount = -1;
            _lastAttackShots = -1;
            _lastLaserCount = -1;
            _lastLaserShots = -1;
            _lastControlCount = -1;
            _lastControlShots = -1;
            _lastShieldCount = -1;
            _lastChargedShields = -1;
        }
    }
}
