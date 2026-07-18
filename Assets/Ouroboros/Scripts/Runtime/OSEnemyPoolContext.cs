using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    public sealed class OSEnemyPoolContext : MonoBehaviour, IOSPoolRentInitializer
    {
        [SerializeField] private OSEnemyRegistry enemyRegistry;
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private Transform target;

        public void Configure(
            OSEnemyRegistry registry,
            OSGameSessionController session,
            Transform enemyTarget)
        {
            enemyRegistry = registry;
            sessionController = session;
            target = enemyTarget;
        }

        public void PrepareForRent(OSPoolableBehaviour instance)
        {
            if (instance is OSEnemyController enemy)
            {
                enemy.ConfigureRuntime(enemyRegistry, sessionController, target);
            }
            else if (instance is OSProjectile projectile)
            {
                projectile.ConfigureRuntime(sessionController);
            }
        }
    }
}
