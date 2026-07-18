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
        [SerializeField, Range(0, 200)] private int initialEnemyCount = 12;
        [SerializeField, Min(1f)] private float minimumRadius = 6.5f;
        [SerializeField, Min(1f)] private float maximumRadius = 11.5f;
        [SerializeField, Min(0f)] private float replacementDelay = 1.5f;

        private bool _subscribed;
        private int _pendingReplacements;
        private int _spawnSequence;
        private float _nextReplacementAt;

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            SpawnInitialEnemies();
        }

        private void Update()
        {
            if (_pendingReplacements <= 0 || sessionController == null ||
                sessionController.State != Core.OSSessionState.Combat || Time.time < _nextReplacementAt)
            {
                return;
            }

            if (TrySpawnEnemy(_spawnSequence++))
            {
                _pendingReplacements--;
            }

            _nextReplacementAt = Time.time + replacementDelay;
        }

        private void OnDisable()
        {
            UnsubscribeActiveEnemies();
            Unsubscribe();
        }

        public int SpawnInitialEnemies()
        {
            if (poolRegistry == null || target == null)
            {
                return 0;
            }

            var spawned = 0;
            for (var index = 0; index < initialEnemyCount; index++)
            {
                if (!TrySpawnEnemy(_spawnSequence++))
                {
                    break;
                }

                spawned++;
            }

            return spawned;
        }

        private bool TrySpawnEnemy(int sequence)
        {
            if (poolRegistry == null || target == null)
            {
                return false;
            }

            var normalized = initialEnemyCount <= 1
                ? 0f
                : sequence % initialEnemyCount / (float)(initialEnemyCount - 1);
            var radius = Mathf.Lerp(minimumRadius, maximumRadius, normalized);
            var angle = sequence * GoldenAngle;
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            var result = poolRegistry.Rent(poolKey, target.position + offset, Quaternion.identity);
            if (!result.IsAccepted || result.Payload is not OSEnemyController enemy)
            {
                return false;
            }

            enemy.Died -= HandleEnemyDied;
            enemy.Died += HandleEnemyDied;
            return true;
        }

        private void HandleEnemyDied(OSEnemyController enemy)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
            }

            if (sessionController == null ||
                sessionController.State is not Core.OSSessionState.Combat and
                    not Core.OSSessionState.ExplosionTelegraph)
            {
                return;
            }

            _pendingReplacements++;
            if (_pendingReplacements == 1)
            {
                _nextReplacementAt = Time.time + replacementDelay;
            }
        }

        private void UnsubscribeActiveEnemies()
        {
            if (enemyRegistry == null)
            {
                return;
            }

            for (var index = 0; index < enemyRegistry.Count; index++)
            {
                var enemy = enemyRegistry.GetAt(index);
                if (enemy != null)
                {
                    enemy.Died -= HandleEnemyDied;
                }
            }
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
            if (current is Core.OSSessionState.Dead or Core.OSSessionState.Cleared)
            {
                _pendingReplacements = 0;
                return;
            }

            if (current != Core.OSSessionState.Boot)
            {
                return;
            }

            UnsubscribeActiveEnemies();
            enemyRegistry?.ReturnAll();
            _pendingReplacements = 0;
            _spawnSequence = 0;
            SpawnInitialEnemies();
        }

        private void OnValidate()
        {
            initialEnemyCount = Mathf.Clamp(initialEnemyCount, 0, 200);
            minimumRadius = Mathf.Max(1f, minimumRadius);
            maximumRadius = Mathf.Max(minimumRadius, maximumRadius);
            replacementDelay = Mathf.Max(0f, replacementDelay);
        }
    }
}
