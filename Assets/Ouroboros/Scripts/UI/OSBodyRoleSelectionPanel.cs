using System;
using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ouroboros.UI
{
    /// <summary>
    /// Displays the fixed Shield-Attack-Laser-Control order and confirms one role per request.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OSBodyRoleSelectionPanel : MonoBehaviour
    {
        private static readonly OSBodyRoleType[] FixedRoles =
        {
            OSBodyRoleType.Shield,
            OSBodyRoleType.Attack,
            OSBodyRoleType.Laser,
            OSBodyRoleType.Control
        };

        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSBodyGrowthController bodyGrowth;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private OSBodyBalanceData bodyBalance;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Button[] roleButtons = new Button[4];
        [SerializeField] private TMP_Text[] roleLabels = new TMP_Text[4];

        private bool _subscribed;
        private int _committedRequestId;
        private int _lastCommitFrame = -1;

        public int DisplayedRoleCount => FixedRoles.Length;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
            SelectFirstButton();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public OSBodyRoleType GetDisplayedRole(int index)
        {
            if ((uint)index >= (uint)FixedRoles.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return FixedRoles[index];
        }

        public void SelectShield() => TryConfirm(OSBodyRoleType.Shield);
        public void SelectAttack() => TryConfirm(OSBodyRoleType.Attack);
        public void SelectLaser() => TryConfirm(OSBodyRoleType.Laser);
        public void SelectControl() => TryConfirm(OSBodyRoleType.Control);

        private void TryConfirm(OSBodyRoleType role)
        {
            if (sessionController == null || bodyGrowth == null ||
                !sessionController.ActiveSelection.HasValue)
            {
                return;
            }

            var request = sessionController.ActiveSelection.Value;
            if (request.Kind is not OSSelectionKind.StartBody and not OSSelectionKind.BodyRole ||
                request.RequestId == _committedRequestId || _lastCommitFrame == Time.frameCount)
            {
                return;
            }

            _committedRequestId = request.RequestId;
            _lastCommitFrame = Time.frameCount;
            var result = bodyGrowth.ConfirmRole(role);
            if (!result.IsAccepted)
            {
                _committedRequestId = 0;
                _lastCommitFrame = -1;
            }
        }

        private void Subscribe()
        {
            if (_subscribed || sessionController == null || !isActiveAndEnabled)
            {
                return;
            }

            sessionController.ActiveSelectionChanged += HandleActiveSelectionChanged;
            if (bodyChain != null)
            {
                bodyChain.SegmentCountChanged += HandleSegmentCountChanged;
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
                sessionController.ActiveSelectionChanged -= HandleActiveSelectionChanged;
            }

            if (bodyChain != null)
            {
                bodyChain.SegmentCountChanged -= HandleSegmentCountChanged;
            }

            _subscribed = false;
        }

        private void HandleActiveSelectionChanged(OSSelectionRequest? request)
        {
            _committedRequestId = 0;
            Refresh();
            SelectFirstButton();
        }

        private void HandleSegmentCountChanged(int count)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (titleLabel != null && sessionController != null)
            {
                var prefix = sessionController.State == OSSessionState.StartBodySelection
                    ? "START BODY"
                    : "BODY GROWTH";
                titleLabel.text = $"{prefix}  |  CHOOSE A ROLE\nOne segment is added to the tail";
            }

            for (var index = 0; index < FixedRoles.Length; index++)
            {
                if (roleLabels == null || index >= roleLabels.Length || roleLabels[index] == null)
                {
                    continue;
                }

                var role = FixedRoles[index];
                roleLabels[index].text = BuildRoleText(role);
            }
        }

        private string BuildRoleText(OSBodyRoleType role)
        {
            var count = bodyChain != null ? bodyChain.GetRoleCount(role) : 0;
            var definition = FindDefinition(role);
            var detail = role switch
            {
                OSBodyRoleType.Shield => $"Blocks one hit\nR {definition?.Radius ?? 1.5f:0.0}",
                OSBodyRoleType.Attack => $"Auto projectile\nDMG {definition?.Damage ?? 6f:0} / {definition?.Interval ?? 1f:0.0}s",
                OSBodyRoleType.Laser => $"Piercing beam\nDMG {definition?.Damage ?? 12f:0} / {definition?.Interval ?? 2.5f:0.0}s",
                OSBodyRoleType.Control => $"Stops movement\n{definition?.NormalControlDuration ?? 1f:0.0}s / {definition?.Interval ?? 4f:0.0}s",
                _ => string.Empty
            };
            return $"{role.ToString().ToUpperInvariant()}\n{detail}\nOWNED {count}\n[ ADD TO TAIL ]";
        }

        private OSBodyRoleDefinition FindDefinition(OSBodyRoleType role)
        {
            if (bodyBalance == null)
            {
                return null;
            }

            for (var index = 0; index < bodyBalance.RoleDefinitions.Count; index++)
            {
                var definition = bodyBalance.RoleDefinitions[index];
                if (definition != null && definition.RoleType == role)
                {
                    return definition;
                }
            }

            return null;
        }

        private void SelectFirstButton()
        {
            if (!isActiveAndEnabled || roleButtons == null || roleButtons.Length == 0 ||
                roleButtons[0] == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(roleButtons[0].gameObject);
        }
    }
}
