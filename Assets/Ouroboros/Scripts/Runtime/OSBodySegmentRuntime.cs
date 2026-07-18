using Ouroboros.Core;

namespace Ouroboros.Runtime
{
    public sealed class OSBodySegmentRuntime
    {
        public int StableId { get; private set; }
        public int ChainIndex { get; private set; } = -1;
        public OSBodyRoleType Role { get; private set; }
        public OSBodySegmentView View { get; private set; }
        public bool IsActive { get; private set; }

        internal void Activate(
            int stableId,
            int chainIndex,
            OSBodyRoleType role,
            OSBodySegmentView view)
        {
            StableId = stableId;
            ChainIndex = chainIndex;
            Role = role;
            View = view;
            IsActive = true;
        }

        internal void Deactivate()
        {
            StableId = 0;
            ChainIndex = -1;
            Role = default;
            IsActive = false;
            View = null;
        }
    }
}
