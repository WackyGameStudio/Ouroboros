using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    public sealed class OSEnemyPoolContext : MonoBehaviour, IOSPoolRentInitializer
    {
        [SerializeField] private OSEnemyRegistry enemyRegistry;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private Transform target;
        [SerializeField] private OSPickupSpawner pickupSpawner;

        public void Configure(
            OSEnemyRegistry registry,
            OSGameSessionController session,
            Transform enemyTarget,
            OSPickupSpawner pickups = null)
        {
            enemyRegistry = registry;
            sessionController = session;
            target = enemyTarget;
            pickupSpawner = pickups;
        }

        public void PrepareForRent(OSPoolableBehaviour instance)
        {
            if (instance is OSEnemyController enemy)
            {
                enemy.ConfigureRuntime(enemyRegistry, sessionController, target);
                enemy.Died += HandleEnemyDied;
            }
            else if (instance is OSProjectile projectile)
            {
                projectile.ConfigureRuntime(sessionController);
            }
        }

        private void HandleEnemyDied(OSEnemyController enemy)
        {
            if (enemy != null && pickupSpawner != null)
            {
                pickupSpawner.TrySpawnFragmentDrop(
                    enemy.Position,
                    enemy.FragmentDropAmount,
                    enemy.FragmentDropChance);
            }
        }
    }
}
