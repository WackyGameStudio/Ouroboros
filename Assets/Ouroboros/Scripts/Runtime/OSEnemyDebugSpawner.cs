using UnityEngine;

namespace Ouroboros.Runtime
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class OSEnemyDebugSpawner : MonoBehaviour
    {
        private const float GoldenAngle = 2.39996323f;

        [SerializeField] private OSPoolRegistry poolRegistry;
        [SerializeField] private OSEnemyRegistry enemyRegistry;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private Transform target;
        [SerializeField] private string poolKey = "enemy_chaser";
        [SerializeField, Range(0, 200)] private int initialEnemyCount = 100;
        [SerializeField, Min(1f)] private float minimumRadius = 6.5f;
        [SerializeField, Min(1f)] private float maximumRadius = 11.5f;

        private bool _subscribed;

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            SpawnInitialEnemies();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public int SpawnInitialEnemies()
        {
            if (poolRegistry == null || target == null)
            {
                return 0;
            }

            var spawned = 0;
            var targetPosition = target.position;
            for (var index = 0; index < initialEnemyCount; index++)
            {
                var normalized = initialEnemyCount <= 1 ? 0f : index / (float)(initialEnemyCount - 1);
                var radius = Mathf.Lerp(minimumRadius, maximumRadius, normalized);
                var angle = index * GoldenAngle;
                var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                var result = poolRegistry.Rent(poolKey, targetPosition + offset, Quaternion.identity);
                if (!result.IsAccepted)
                {
                    break;
                }

                spawned++;
            }

            return spawned;
        }

        private void Subscribe()
        {
            if (_subscribed || sessionController == null)
            {
                return;
            }

            sessionController.StateChanged += HandleSessionStateChanged;
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
                sessionController.StateChanged -= HandleSessionStateChanged;
            }

            _subscribed = false;
        }

        private void HandleSessionStateChanged(Core.OSSessionState previous, Core.OSSessionState current)
        {
            if (current != Core.OSSessionState.Boot)
            {
                return;
            }

            enemyRegistry?.ReturnAll();
            SpawnInitialEnemies();
        }

        private void OnValidate()
        {
            initialEnemyCount = Mathf.Clamp(initialEnemyCount, 0, 200);
            minimumRadius = Mathf.Max(1f, minimumRadius);
            maximumRadius = Mathf.Max(minimumRadius, maximumRadius);
        }
    }
}
