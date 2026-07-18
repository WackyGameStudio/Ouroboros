using UnityEngine;

namespace Ouroboros.Runtime
{
    /// <summary>
    /// Explicit pooled-object lifecycle. OnDisable is intentionally not used as a Return signal.
    /// </summary>
    public abstract class OSPoolableBehaviour : MonoBehaviour
    {
        private OSPoolRegistry _owner;
        private string _poolKey = string.Empty;
        private int _bucketIndex = -1;
        private int _instanceIndex = -1;
        private Vector3 _pooledLocalScale = Vector3.one;

        public OSPoolRegistry PoolOwner => _owner;
        public string PoolKey => _poolKey;
        public int RuntimeId { get; private set; }
        public bool IsRented { get; private set; }

        internal int PoolBucketIndex => _bucketIndex;
        internal int PoolInstanceIndex => _instanceIndex;

        public bool ReturnToPool()
        {
            return _owner != null && _owner.Return(this).IsAccepted;
        }

        internal void InitializePoolOwnership(
            OSPoolRegistry owner,
            string poolKey,
            int bucketIndex,
            int instanceIndex)
        {
            _owner = owner;
            _poolKey = poolKey ?? string.Empty;
            _bucketIndex = bucketIndex;
            _instanceIndex = instanceIndex;
            _pooledLocalScale = transform.localScale;
            RuntimeId = 0;
            IsRented = false;
        }

        internal void PrepareRent(int runtimeId, Vector3 position, Quaternion rotation)
        {
            RuntimeId = runtimeId;
            IsRented = true;
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = _pooledLocalScale;
            gameObject.SetActive(true);
            OnRented();
        }

        internal void PrepareReturn(Transform poolRoot)
        {
            OnReturning();
            IsRented = false;
            RuntimeId = 0;
            gameObject.SetActive(false);
            transform.SetParent(poolRoot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = _pooledLocalScale;
        }

        protected abstract void OnRented();
        protected abstract void OnReturning();
    }
}
