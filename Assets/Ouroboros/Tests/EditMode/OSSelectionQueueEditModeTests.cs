using NUnit.Framework;
using Ouroboros.Core;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSSelectionQueueEditModeTests
    {
        [Test]
        public void BodyTwoAndLevelUpTwoDequeueInBodyPriorityFifoOrder()
        {
            var queue = new OSSelectionQueue();
            Assert.That(queue.Enqueue(new OSSelectionRequest(1, OSSelectionKind.LevelUp, 0)).IsAccepted, Is.True);
            Assert.That(queue.Enqueue(new OSSelectionRequest(2, OSSelectionKind.BodyRole, 0)).IsAccepted, Is.True);
            Assert.That(queue.Enqueue(new OSSelectionRequest(3, OSSelectionKind.LevelUp, 0)).IsAccepted, Is.True);
            Assert.That(queue.Enqueue(new OSSelectionRequest(4, OSSelectionKind.BodyRole, 0)).IsAccepted, Is.True);

            AssertNext(queue, 2, OSSelectionKind.BodyRole);
            AssertNext(queue, 4, OSSelectionKind.BodyRole);
            AssertNext(queue, 1, OSSelectionKind.LevelUp);
            AssertNext(queue, 3, OSSelectionKind.LevelUp);
            Assert.That(queue.Count, Is.Zero);
        }

        [Test]
        public void DuplicateRequestIsRejectedUntilQueueIsClearedForNewSession()
        {
            var queue = new OSSelectionQueue();
            var request = new OSSelectionRequest(7, OSSelectionKind.BodyRole, 3);

            Assert.That(queue.Enqueue(request).Code, Is.EqualTo(OSResultCode.Queued));
            Assert.That(queue.TryDequeue(out _), Is.True);
            Assert.That(queue.Enqueue(request).Code, Is.EqualTo(OSResultCode.Duplicate));

            queue.Clear();
            Assert.That(queue.Enqueue(request).Code, Is.EqualTo(OSResultCode.Queued));
        }

        private static void AssertNext(OSSelectionQueue queue, int requestId, OSSelectionKind kind)
        {
            Assert.That(queue.TryDequeue(out var request), Is.True);
            Assert.That(request.RequestId, Is.EqualTo(requestId));
            Assert.That(request.Kind, Is.EqualTo(kind));
        }
    }
}
