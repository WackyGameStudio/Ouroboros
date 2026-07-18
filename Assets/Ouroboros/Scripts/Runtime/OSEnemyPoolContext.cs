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
        [SerializeField] private OSPlayerCombatResolver playerCombatResolver;

        public void Configure(
            OSEnemyRegistry registry,
            OSGameSessionController session,
            Transform enemyTarget,
            OSPickupSpawner pickups = null,
            OSPlayerCombatResolver combatResolver = null)
        {
            enemyRegistry = registry;
            sessionController = session;
            target = enemyTarget;
            pickupSpawner = pickups;
            playerCombatResolver = combatResolver;
        }

        public void PrepareForRent(OSPoolableBehaviour instance)
        {
            if (instance is OSEnemyController enemy)
            {
                enemy.ConfigureRuntime(enemyRegistry, sessionController, target);
                enemy.Died += HandleEnemyDied;
                enemy.ContactAttackRequested += HandleContactAttackRequested;
            }
            else if (instance is OSProjectile projectile)
            {
                projectile.ConfigureRuntime(sessionController);
            }
            else if (instance is OSControlProjectile controlProjectile)
            {
                controlProjectile.ConfigureRuntime(sessionController);
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

        private void HandleContactAttackRequested(Ouroboros.Core.OSDamageEvent damageEvent)
        {
            playerCombatResolver?.EnqueueDamage(damageEvent);
        }
    }
}
