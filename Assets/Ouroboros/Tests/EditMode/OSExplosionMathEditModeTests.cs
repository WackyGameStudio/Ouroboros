using NUnit.Framework;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSExplosionMathEditModeTests
    {
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(5, 2)]
        [TestCase(10, 3)]
        [TestCase(64, 20)]
        public void CalculateConsumeCount_UsesCeilThirtyPercent(int activeCount, int expected)
        {
            Assert.That(OSExplosionMath.CalculateConsumeCount(activeCount, 0.30f), Is.EqualTo(expected));
        }

        [Test]
        public void IsInsideAnyCircle_OverlappingCentersStillReturnSingleMembership()
        {
            var centers = new[] { Vector2.zero, new Vector2(1f, 0f) };

            Assert.That(OSExplosionMath.IsInsideAnyCircle(new Vector2(0.5f, 0f), centers, 1.8f), Is.True);
            Assert.That(OSExplosionMath.IsInsideAnyCircle(new Vector2(4f, 0f), centers, 1.8f), Is.False);
        }
    }
}
