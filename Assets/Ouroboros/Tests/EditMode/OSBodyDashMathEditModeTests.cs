using NUnit.Framework;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSBodyDashMathEditModeTests
    {
        [Test]
        public void EaseOutStepDistancesSumToConfiguredDistance()
        {
            var travelled = 0f;
            for (var step = 0; step < 25; step++)
            {
                travelled += OSBodyDashMath.CalculateStepDistance(
                    4.5f,
                    0.5f,
                    step * 0.02f,
                    (step + 1) * 0.02f);
            }

            Assert.That(travelled, Is.EqualTo(4.5f).Within(0.0001f));
            Assert.That(
                OSBodyDashMath.CalculateStepDistance(4.5f, 0.5f, 0f, 0.02f),
                Is.GreaterThan(OSBodyDashMath.CalculateStepDistance(4.5f, 0.5f, 0.48f, 0.5f)));
        }

        [Test]
        public void DirectionUsesCurrentInputThenFallsBackToLastDirection()
        {
            Assert.That(
                OSBodyDashMath.ResolveDirection(Vector2.up * 0.5f, Vector2.left),
                Is.EqualTo(Vector2.up));
            Assert.That(
                OSBodyDashMath.ResolveDirection(Vector2.zero, Vector2.left),
                Is.EqualTo(Vector2.left));
            Assert.That(
                OSBodyDashMath.ResolveDirection(Vector2.zero, Vector2.zero),
                Is.EqualTo(Vector2.right));
        }

        [Test]
        public void DashUpgradeValuesRespectSafetyFloors()
        {
            Assert.That(OSBodyDashMath.CalculateDistance(4.5f, 1.15f), Is.EqualTo(5.175f).Within(0.0001f));
            Assert.That(OSBodyDashMath.CalculateCooldown(2f, 0.88f), Is.EqualTo(1.76f).Within(0.0001f));
            Assert.That(OSBodyDashMath.CalculateCooldown(2f, -10f), Is.EqualTo(0.5f));
            Assert.That(OSBodyDashMath.CalculateRecoveryDuration(0.25f, -1f), Is.EqualTo(0.1f));
        }
    }
}
