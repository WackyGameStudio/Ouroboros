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
        public void Circle_UsesOnePointFiveTimesTheFormerRadiusAndClosesAtStart()
        {
            const int remaining = 18;
            const float spacing = 0.55f;
            var start = new Vector2(2f, -3f);
            var forward = new Vector2(0.6f, 0.8f);
            var radius = OSBombMath.CalculateRadius(remaining, spacing);
            var center = OSBombMath.CalculateCenter(start, forward, radius);

            Assert.That(
                radius * Mathf.PI * 2f,
                Is.EqualTo(remaining * spacing * 1.5f).Within(0.0001f));
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
        public void RhythmicProgress_EasesAtBothEndsAndMovesFastestNearTheMiddle()
        {
            var startStep = OSBombMath.CalculateRhythmicProgress(0.1f) -
                            OSBombMath.CalculateRhythmicProgress(0f);
            var middleStep = OSBombMath.CalculateRhythmicProgress(0.55f) -
                             OSBombMath.CalculateRhythmicProgress(0.45f);
            var endStep = OSBombMath.CalculateRhythmicProgress(1f) -
                          OSBombMath.CalculateRhythmicProgress(0.9f);

            Assert.That(OSBombMath.CalculateRhythmicProgress(0f), Is.Zero);
            Assert.That(OSBombMath.CalculateRhythmicProgress(0.5f), Is.EqualTo(0.5f));
            Assert.That(OSBombMath.CalculateRhythmicProgress(1f), Is.EqualTo(1f));
            Assert.That(middleStep, Is.GreaterThan(startStep * 3f));
            Assert.That(middleStep, Is.GreaterThan(endStep * 3f));
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
        public void Damage_ScalesWithBodyCountAndUpgradeWhileCooldownKeepsFloor()
        {
            Assert.That(OSBombMath.CalculateDamage(10, 10f, 1f), Is.EqualTo(100f));
            Assert.That(OSBombMath.CalculateDamage(20, 10f, 1f), Is.EqualTo(200f));
            Assert.That(OSBombMath.CalculateDamage(20, 10f, 1.6f), Is.EqualTo(320f));
            Assert.That(OSBombMath.CalculateCooldown(10f, -3f), Is.EqualTo(7f));
            Assert.That(OSBombMath.CalculateCooldown(10f, -100f), Is.EqualTo(5f));
        }
    }
}
