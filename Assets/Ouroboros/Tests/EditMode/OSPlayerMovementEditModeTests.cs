using NUnit.Framework;
using Ouroboros.Runtime;
using UnityEngine;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSPlayerMovementEditModeTests
    {
        [Test]
        public void StraightAndDiagonalInputsHaveIdenticalTopSpeed()
        {
            var straight = OSPlayerController.CalculateDisplacement(Vector2.right, 5.5f, 1f);
            var diagonal = OSPlayerController.CalculateDisplacement(Vector2.one, 5.5f, 1f);

            Assert.That(straight.magnitude, Is.EqualTo(5.5f).Within(0.0001f));
            Assert.That(diagonal.magnitude, Is.EqualTo(straight.magnitude).Within(0.0001f));
        }

        [Test]
        public void ThirtyAndSixtyFpsIntegrationsCoverSameDistance()
        {
            var atThirty = Vector2.zero;
            for (var frame = 0; frame < 30; frame++)
            {
                atThirty += OSPlayerController.CalculateDisplacement(Vector2.right, 5.5f, 1f / 30f);
            }

            var atSixty = Vector2.zero;
            for (var frame = 0; frame < 60; frame++)
            {
                atSixty += OSPlayerController.CalculateDisplacement(Vector2.right, 5.5f, 1f / 60f);
            }

            Assert.That(atThirty.x, Is.EqualTo(5.5f).Within(0.0001f));
            Assert.That(atSixty.x, Is.EqualTo(atThirty.x).Within(0.0001f));
        }

        [Test]
        public void InvalidMovementValuesHaveNoSideEffects()
        {
            Assert.That(
                OSPlayerController.NormalizeMoveInput(new Vector2(float.NaN, 1f)),
                Is.EqualTo(Vector2.zero));
            Assert.That(
                OSPlayerController.CalculateDisplacement(Vector2.right, float.PositiveInfinity, 1f),
                Is.EqualTo(Vector2.zero));
            Assert.That(
                OSPlayerController.CalculateDisplacement(Vector2.right, 5.5f, -1f),
                Is.EqualTo(Vector2.zero));
        }
    }
}
