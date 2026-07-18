using System.Collections;
using NUnit.Framework;
using Ouroboros.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep05ScenePlayModeTests
    {
        private const string GameScenePath = "Assets/Ouroboros/Scenes/20_Game.unity";

        [UnityTest]
        public IEnumerator GameScene_PrewarmsSixtyFourTriggerSegmentsAndWaitsForStartSelection()
        {
            yield return SceneManager.LoadSceneAsync(GameScenePath, LoadSceneMode.Single);
            yield return null;

            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            Assert.That(chain, Is.Not.Null);
            Assert.That(chain.PoolCapacity, Is.EqualTo(64));
            Assert.That(chain.PathCapacity, Is.EqualTo(329));
            Assert.That(chain.ActiveCount, Is.Zero);
            Assert.That(Physics2D.GetIgnoreLayerCollision(
                LayerMask.NameToLayer("PlayerBodyHurtbox"),
                LayerMask.NameToLayer("WorldBlocker")), Is.True);
        }
    }
}
