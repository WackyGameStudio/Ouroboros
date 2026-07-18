using NUnit.Framework;
using Ouroboros.Core;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSBodyGrowthProgressEditModeTests
    {
        [Test]
        public void ElevenAndTwelveFragments_CreateOnlyTheExpectedRequest()
        {
            var progress = new OSBodyGrowthProgress(12, 64);

            var eleven = progress.AddFragments(11, 0, 0);
            Assert.That(eleven.Payload, Is.Zero);
            Assert.That(progress.FragmentProgress, Is.EqualTo(11));

            var twelve = progress.AddFragments(1, 0, 0);
            Assert.That(twelve.Payload, Is.EqualTo(1));
            Assert.That(progress.FragmentProgress, Is.Zero);
        }

        [TestCase(23, 1, 11)]
        [TestCase(24, 2, 0)]
        public void MultiThresholdCollection_PreservesRemainder(
            int amount,
            int expectedRequests,
            int expectedRemainder)
        {
            var progress = new OSBodyGrowthProgress(12, 64);

            var result = progress.AddFragments(amount, 0, 0);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.Payload, Is.EqualTo(expectedRequests));
            Assert.That(progress.FragmentProgress, Is.EqualTo(expectedRemainder));
        }

        [Test]
        public void TechnicalGuard_HoldsOneFullGaugeAndResumesAfterCapacityReturns()
        {
            var progress = new OSBodyGrowthProgress(12, 64);

            var deferred = progress.AddFragments(24, 63, 1);
            Assert.That(deferred.IsAccepted, Is.True);
            Assert.That(deferred.Payload, Is.Zero);
            Assert.That(progress.FragmentProgress, Is.EqualTo(12));
            Assert.That(progress.HasDeferredRequest, Is.True);

            var resumed = progress.TryResumeDeferred(62, 1);
            Assert.That(resumed.IsAccepted, Is.True);
            Assert.That(resumed.Payload, Is.EqualTo(1));
            Assert.That(progress.FragmentProgress, Is.Zero);
        }
    }
}
