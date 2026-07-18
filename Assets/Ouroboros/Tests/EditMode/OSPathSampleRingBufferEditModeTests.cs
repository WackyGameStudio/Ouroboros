using NUnit.Framework;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSPathSampleRingBufferEditModeTests
    {
        [Test]
        public void Evaluate_InterpolatesBoundariesAndVirtualStraightPath()
        {
            var buffer = new OSPathSampleRingBuffer(8);
            buffer.Append(Vector2.zero, 0f, Vector2.right);
            buffer.Append(Vector2.right, 1f, Vector2.right);
            buffer.Append(Vector2.right * 2f, 2f, Vector2.right);

            Assert.That(buffer.TryEvaluate(-0.5f, out var before), Is.True);
            Assert.That(before.Position, Is.EqualTo(new Vector2(-0.5f, 0f)));
            Assert.That(buffer.TryEvaluate(0.5f, out var middle), Is.True);
            Assert.That(middle.Position.x, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(buffer.TryEvaluate(2.5f, out var after), Is.True);
            Assert.That(after.Position.x, Is.EqualTo(2.5f).Within(0.00001f));
        }

        [Test]
        public void Append_WrapAroundPreservesChronologicalOrder()
        {
            var buffer = new OSPathSampleRingBuffer(4);
            for (var index = 0; index <= 5; index++)
            {
                buffer.Append(Vector2.right * index, index, Vector2.right);
            }

            Assert.That(buffer.Count, Is.EqualTo(4));
            Assert.That(buffer.Oldest.CumulativeDistance, Is.EqualTo(2f));
            Assert.That(buffer.Newest.CumulativeDistance, Is.EqualTo(5f));
            for (var index = 0; index < buffer.Count; index++)
            {
                Assert.That(buffer[index].CumulativeDistance, Is.EqualTo(index + 2f));
            }

            Assert.That(buffer.TryEvaluate(3.5f, out var sample), Is.True);
            Assert.That(sample.Position.x, Is.EqualTo(3.5f).Within(0.00001f));
        }

        [Test]
        public void DiscardBefore_RetainsInterpolationSampleAtBoundary()
        {
            var buffer = new OSPathSampleRingBuffer(16);
            for (var index = 0; index <= 10; index++)
            {
                buffer.Append(Vector2.right * index, index, Vector2.right);
            }

            buffer.DiscardBefore(5.5f);

            Assert.That(buffer.Oldest.CumulativeDistance, Is.EqualTo(5f));
            Assert.That(buffer.Newest.CumulativeDistance, Is.EqualTo(10f));
            Assert.That(buffer.TryEvaluate(5.5f, out var sample), Is.True);
            Assert.That(sample.Position.x, Is.EqualTo(5.5f).Within(0.00001f));
        }

        [Test]
        public void CalculateRequiredCapacity_CoversSixtyFourSegmentsAndReserve()
        {
            var capacity = OSPathSampleRingBuffer.CalculateRequiredCapacity(64, 0.55f, 0.12f, 4f);
            var storedSpan = (capacity - 2) * 0.12f;

            Assert.That(capacity, Is.EqualTo(329));
            Assert.That(storedSpan, Is.GreaterThanOrEqualTo((64 * 0.55f) + 4f));
        }
    }
}
