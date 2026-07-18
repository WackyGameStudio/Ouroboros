using NUnit.Framework;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSBodyRoleMathEditModeTests
    {
        [TestCase(0f, 0f, true)]
        [TestCase(7f, 0.175f, true)]
        [TestCase(7.01f, 0f, false)]
        [TestCase(3f, 0.176f, false)]
        [TestCase(-0.01f, 0f, false)]
        public void IsInsideBeam_UsesSnapshotLengthAndFullWidth(float x, float y, bool expected)
        {
            Assert.That(
                OSBodyRoleMath.IsInsideBeam(
                    new Vector2(x, y),
                    Vector2.zero,
                    Vector2.right,
                    7f,
                    0.35f),
                Is.EqualTo(expected));
        }

        [Test]
        public void IsInsideBeam_RejectsInvalidGeometry()
        {
            Assert.That(
                OSBodyRoleMath.IsInsideBeam(
                    Vector2.zero,
                    Vector2.zero,
                    Vector2.zero,
                    7f,
                    0.35f),
                Is.False);
            Assert.That(
                OSBodyRoleMath.IsInsideBeam(
                    Vector2.zero,
                    Vector2.zero,
                    Vector2.right,
                    float.NaN,
                    0.35f),
                Is.False);
        }

        [Test]
        public void SelectControlDuration_UsesEliteValueForEliteAndBossOnly()
        {
            Assert.That(
                OSBodyRoleMath.SelectControlDuration(OSEnemyArchetype.Chaser, 1f, 0.5f),
                Is.EqualTo(1f));
            Assert.That(
                OSBodyRoleMath.SelectControlDuration(
                    OSEnemyArchetype.EliteAccelerator,
                    1f,
                    0.5f),
                Is.EqualTo(0.5f));
            Assert.That(
                OSBodyRoleMath.SelectControlDuration(
                    OSEnemyArchetype.BossSwarmCore,
                    1f,
                    0.5f),
                Is.EqualTo(0.5f));
        }
    }
}
