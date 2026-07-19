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
    public sealed class OSStep11ScenePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameScene_FourRolesRegisterWithDistinctShapesPoolsViewsAndHud()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var registry = Object.FindAnyObjectByType<OSBodyRoleRegistry>();
            var attack = Object.FindAnyObjectByType<OSAttackBodyRole>();
            var laser = Object.FindAnyObjectByType<OSLaserBodyRole>();
            var control = Object.FindAnyObjectByType<OSControlBodyRole>();
            var shield = Object.FindAnyObjectByType<OSShieldBodyRole>();
            var pool = Object.FindAnyObjectByType<OSPoolRegistry>();
            var presenter = Object.FindAnyObjectByType<OSBodyRoleCombatPresenter>(
                FindObjectsInactive.Include);

            Assert.That(session, Is.Not.Null);
            Assert.That(growth, Is.Not.Null);
            Assert.That(chain, Is.Not.Null);
            Assert.That(registry, Is.Not.Null);
            Assert.That(attack, Is.Not.Null);
            Assert.That(laser, Is.Not.Null);
            Assert.That(control, Is.Not.Null);
            Assert.That(shield, Is.Not.Null);
            Assert.That(pool, Is.Not.Null);
            Assert.That(presenter, Is.Not.Null);

            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            Assert.That(chain.SetDebugSegmentCount(4).IsAccepted, Is.True);
            yield return null;

            Assert.That(attack.ActiveSegmentCount, Is.EqualTo(1));
            Assert.That(laser.ActiveSegmentCount, Is.EqualTo(1));
            Assert.That(control.ActiveSegmentCount, Is.EqualTo(1));
            Assert.That(shield.ActiveSegmentCount, Is.EqualTo(1));
            Assert.That(shield.ChargedCount, Is.EqualTo(1));
            Assert.That(pool.GetCapacity("body_control_projectile"), Is.EqualTo(64));

            var roleRoot = GameObject.Find("OSBodyRoleSystems")?.transform;
            Assert.That(roleRoot, Is.Not.Null);
            Assert.That(roleRoot.Find("RoleViews/LaserTelegraphs")?.childCount, Is.EqualTo(64));
            Assert.That(roleRoot.Find("RoleViews/ShieldRanges")?.childCount, Is.EqualTo(64));
            Assert.That(GameObject.Find("RoleCombatStatusLabel"), Is.Not.Null);

            var spriteNames = new HashSet<string>();
            for (var index = 0; index < 4; index++)
            {
                var sprite = chain.GetActiveSegment(index).View
                    .GetComponentInChildren<SpriteRenderer>(true)?.sprite;
                Assert.That(sprite, Is.Not.Null);
                spriteNames.Add(sprite.name);
            }

            Assert.That(spriteNames.Count, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator GameScene_CutRemovesLostRolesWhileBodyDashPreservesRemainingRolesAndFirepower()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;

            var growth = Object.FindAnyObjectByType<OSBodyGrowthController>();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var attack = Object.FindAnyObjectByType<OSAttackBodyRole>();
            var laser = Object.FindAnyObjectByType<OSLaserBodyRole>();
            var control = Object.FindAnyObjectByType<OSControlBodyRole>();
            var shield = Object.FindAnyObjectByType<OSShieldBodyRole>();
            var bodyDash = Object.FindAnyObjectByType<OSBodyDashController>();
            var player = Object.FindAnyObjectByType<OSPlayerController>();
            var headWeapon = Object.FindAnyObjectByType<OSHeadWeapon>();

            Assert.That(growth.ConfirmRole(OSBodyRoleType.Shield).IsAccepted, Is.True);
            Assert.That(growth.ConfirmRole(OSBodyRoleType.Attack).IsAccepted, Is.True);
            Assert.That(chain.SetDebugSegmentCount(8).IsAccepted, Is.True);
            yield return null;

            Assert.That(attack.ActiveSegmentCount, Is.EqualTo(2));
            Assert.That(laser.ActiveSegmentCount, Is.EqualTo(2));
            Assert.That(control.ActiveSegmentCount, Is.EqualTo(2));
            Assert.That(shield.ActiveSegmentCount, Is.EqualTo(2));
            var damageAtEight = headWeapon.Damage;

            Assert.That(chain.RemoveTailSegments(1).IsAccepted, Is.True);
            Assert.That(control.ActiveSegmentCount, Is.EqualTo(1));
            Assert.That(attack.ActiveSegmentCount, Is.EqualTo(2));
            Assert.That(laser.ActiveSegmentCount, Is.EqualTo(2));
            Assert.That(shield.ActiveSegmentCount, Is.EqualTo(2));

            var bodyCountBeforeDash = chain.ActiveCount;
            var damageBeforeDash = headWeapon.Damage;
            Assert.That(bodyDash.RequestBodyDash().Payload, Is.EqualTo(bodyCountBeforeDash));
            for (var step = 0; step < 25; step++)
            {
                player.SimulateBodyDashStep(0.02f);
            }

            Assert.That(chain.ActiveCount, Is.EqualTo(bodyCountBeforeDash));
            Assert.That(attack.ActiveSegmentCount, Is.EqualTo(2));
            Assert.That(laser.ActiveSegmentCount, Is.EqualTo(2));
            Assert.That(control.ActiveSegmentCount, Is.EqualTo(1));
            Assert.That(shield.ActiveSegmentCount, Is.EqualTo(2));
            Assert.That(headWeapon.Damage, Is.EqualTo(damageBeforeDash));
            Assert.That(headWeapon.Damage, Is.LessThan(damageAtEight));
        }
    }
}
