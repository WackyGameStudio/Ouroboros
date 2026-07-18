using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    public sealed class OSCombatTargetIdentity : MonoBehaviour
    {
        [SerializeField] private int runtimeId = 1;
        [SerializeField] private OSTargetKind targetKind = OSTargetKind.PlayerHead;

        public int RuntimeId => runtimeId;
        public OSTargetKind TargetKind => targetKind;

        public void Configure(int id, OSTargetKind kind)
        {
            runtimeId = Mathf.Max(1, id);
            targetKind = kind;
        }

        private void OnValidate()
        {
            runtimeId = Mathf.Max(1, runtimeId);
        }
    }
}
