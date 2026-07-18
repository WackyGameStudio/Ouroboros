using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;

namespace Ouroboros.UI
{
    /// <summary>
    /// Presents body length, role counts, and fragment progress without exposing the technical guard.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OSBodyGrowthPresenter : MonoBehaviour
    {
        [SerializeField] private OSBodyGrowthController bodyGrowth;
        [SerializeField] private OSBodyChain bodyChain;
        [SerializeField] private TMP_Text label;

        private void Update()
        {
            if (bodyGrowth == null || bodyChain == null || label == null)
            {
                return;
            }

            label.text =
                $"BODY {bodyChain.ActiveCount}  " +
                $"S {bodyChain.GetRoleCount(OSBodyRoleType.Shield)}  " +
                $"A {bodyChain.GetRoleCount(OSBodyRoleType.Attack)}  " +
                $"L {bodyChain.GetRoleCount(OSBodyRoleType.Laser)}  " +
                $"C {bodyChain.GetRoleCount(OSBodyRoleType.Control)}\n" +
                $"FRAGMENT {bodyGrowth.FragmentProgress}/{bodyGrowth.FragmentRequirement}";
        }
    }
}
