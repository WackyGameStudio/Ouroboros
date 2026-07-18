using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using Ouroboros.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep08ScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator GameScene_StartSelectionsGrowthAndPickupContractReachTwentyOneSegments()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            var panel = Object.FindAnyObjectByType<OSBodyRoleSelectionPanel>(FindObjectsInactive.Include);
            var pool = Object.FindAnyObjectByType<OSPoolRegistry>();
            var spawner = Object.FindAnyObjectByType<OSPickupSpawner>();

            Assert.That(session, Is.Not.Null);
            Assert.That(chain, Is.Not.Null);
            Assert.That(growth, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Assert.That(pool, Is.Not.Null);
            Assert.That(spawner, Is.Not.Null);
            Assert.That(session.State, Is.EqualTo(OSSessionState.StartBodySelection));
            Assert.That(chain.ActiveCount, Is.Zero);
            Assert.That(pool.GetCapacity("body_fragment_pickup"), Is.EqualTo(256));
            Assert.That(LayerMask.NameToLayer("Pickup"), Is.GreaterThanOrEqualTo(0));
            Assert.That(LayerMask.NameToLayer("PickupCollector"), Is.GreaterThanOrEqualTo(0));
            Assert.That(
                Physics2D.GetIgnoreLayerCollision(
                    LayerMask.NameToLayer("Pickup"),
                    LayerMask.NameToLayer("PickupCollector")),
                Is.False);

            var displayedRoles = new HashSet<OSBodyRoleType>();
            Assert.That(panel.DisplayedRoleCount, Is.EqualTo(4));
            for (var index = 0; index < panel.DisplayedRoleCount; index++)
            {
                displayedRoles.Add(panel.GetDisplayedRole(index));
            }

            Assert.That(displayedRoles.Count, Is.EqualTo(4));
            CollectionAssert.AreEqual(
                new[]
                {
                    OSBodyRoleType.Shield,
                    OSBodyRoleType.Attack,
                    OSBodyRoleType.Laser,
                    OSBodyRoleType.Control
                },
                new[]
                {
                    panel.GetDisplayedRole(0),
                    panel.GetDisplayedRole(1),
                    panel.GetDisplayedRole(2),
                    panel.GetDisplayedRole(3)
                });

            panel.SelectShield();
            panel.SelectShield();
            Assert.That(chain.ActiveCount, Is.EqualTo(1), "Same-frame duplicate confirmation appended twice.");
            Assert.That(session.State, Is.EqualTo(OSSessionState.StartBodySelection));
            yield return null;

            panel.SelectAttack();
            Assert.That(chain.ActiveCount, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(chain.GetRoleCount(OSBodyRoleType.Shield), Is.EqualTo(1));
            Assert.That(chain.GetRoleCount(OSBodyRoleType.Attack), Is.EqualTo(1));
            yield return null;

            for (var request = 0; request < 19; request++)
            {
                var add = growth.AddFragments(12);
                Assert.That(add.IsAccepted, Is.True, add.ReasonKey);
                Assert.That(session.State, Is.EqualTo(OSSessionState.BodyRoleSelection));
                panel.SelectLaser();
                yield return null;
            }

            Assert.That(chain.ActiveCount, Is.EqualTo(21));
            Assert.That(chain.GetRoleCount(OSBodyRoleType.Laser), Is.EqualTo(19));
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(growth.FragmentProgress, Is.Zero);
        }
    }
}
