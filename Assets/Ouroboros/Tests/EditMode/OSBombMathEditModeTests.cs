using NUnit.Framework;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSBombMathEditModeTests
    {
        [TestCase(9, 0)]
        [TestCase(10, 1)]
        [TestCase(19, 1)]
        [TestCase(20, 2)]
        [TestCase(64, 6)]
        public void ConsumeCount_UsesTenPercentFloor(int bodyCount, int expected)
        {
            Assert.That(
                OSBombMath.CalculateConsumeCount(bodyCount, 0.1f),
                Is.EqualTo(expected));
        }

        [Test]
        public void Circle_UsesRemainingSnakeLengthAndClosesAtStart()
        {
            const int remaining = 18;
            const float spacing = 0.55f;
            var start = new Vector2(2f, -3f);
            var forward = new Vector2(0.6f, 0.8f);
            var radius = OSBombMath.CalculateRadius(remaining, spacing);
            var center = OSBombMath.CalculateCenter(start, forward, radius);

            Assert.That(radius * Mathf.PI * 2f, Is.EqualTo(remaining * spacing).Within(0.0001f));
            Assert.That(center, Is.EqualTo(start + (forward.normalized * radius)));
            Assert.That(
                Vector2.Distance(
                    OSBombMath.CalculateOrbitPoint(
                        start,
                        forward,
                        radius,
                        1f,
                        OSBombTurnSide.Left),
                    start),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector2.Distance(
                    OSBombMath.CalculateOrbitPoint(
                        start,
                        forward,
                        radius,
                        1f,
                        OSBombTurnSide.Right),
                    start),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void TurnSide_ChoosesOppositeBodyMajorityAndUsesDistanceTieBreak()
        {
            Assert.That(
                OSBombMath.ResolveTurnSide(5, 2, 3f),
                Is.EqualTo(OSBombTurnSide.Right));
            Assert.That(
                OSBombMath.ResolveTurnSide(2, 5, -3f),
                Is.EqualTo(OSBombTurnSide.Left));
            Assert.That(
                OSBombMath.ResolveTurnSide(3, 3, 2f),
                Is.EqualTo(OSBombTurnSide.Right));
            Assert.That(
                OSBombMath.ResolveTurnSide(3, 3, -2f),
                Is.EqualTo(OSBombTurnSide.Left));
            Assert.That(
                OSBombMath.ResolveTurnSide(3, 3, 0f),
                Is.EqualTo(OSBombTurnSide.Right));
        }

        [Test]
        public void RightTurn_StartsOnHeadRightAndLeftTurnStartsOnHeadLeft()
        {
            var right = OSBombMath.CalculateOrbitPoint(
                Vector2.zero,
                Vector2.right,
                2f,
                0.01f,
                OSBombTurnSide.Right);
            var left = OSBombMath.CalculateOrbitPoint(
                Vector2.zero,
                Vector2.right,
                2f,
                0.01f,
                OSBombTurnSide.Left);

            Assert.That(right.y, Is.LessThan(0f));
            Assert.That(left.y, Is.GreaterThan(0f));
        }

        [Test]
        public void Upgrades_KeepFixedDamageAndCooldownFloor()
        {
            Assert.That(OSBombMath.CalculateDamage(100f, 1.6f), Is.EqualTo(160f));
            Assert.That(OSBombMath.CalculateCooldown(10f, -3f), Is.EqualTo(7f));
            Assert.That(OSBombMath.CalculateCooldown(10f, -100f), Is.EqualTo(5f));
        }
    }
}
