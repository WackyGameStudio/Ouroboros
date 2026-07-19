using NUnit.Framework;
using Ouroboros.Core;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSBodyGrowthProgressEditModeTests
    {
        [Test]
        public void FiveAndSixFragments_CreateOnlyTheExpectedRequest()
        {
            var progress = new OSBodyGrowthProgress(6, 64);

            var five = progress.AddFragments(5, 0, 0);
            Assert.That(five.Payload, Is.Zero);
            Assert.That(progress.FragmentProgress, Is.EqualTo(5));

            var six = progress.AddFragments(1, 0, 0);
            Assert.That(six.Payload, Is.EqualTo(1));
            Assert.That(progress.FragmentProgress, Is.Zero);
        }

        [TestCase(11, 1, 5)]
        [TestCase(12, 2, 0)]
        public void MultiThresholdCollection_PreservesRemainder(
            int amount,
            int expectedRequests,
            int expectedRemainder)
        {
            var progress = new OSBodyGrowthProgress(6, 64);

            var result = progress.AddFragments(amount, 0, 0);

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.Payload, Is.EqualTo(expectedRequests));
            Assert.That(progress.FragmentProgress, Is.EqualTo(expectedRemainder));
        }

        [Test]
        public void TechnicalGuard_HoldsOneFullGaugeAndResumesAfterCapacityReturns()
        {
            var progress = new OSBodyGrowthProgress(6, 64);

            var deferred = progress.AddFragments(12, 63, 1);
            Assert.That(deferred.IsAccepted, Is.True);
            Assert.That(deferred.Payload, Is.Zero);
            Assert.That(progress.FragmentProgress, Is.EqualTo(6));
            Assert.That(progress.HasDeferredRequest, Is.True);

            var resumed = progress.TryResumeDeferred(62, 1);
            Assert.That(resumed.IsAccepted, Is.True);
            Assert.That(resumed.Payload, Is.EqualTo(1));
            Assert.That(progress.FragmentProgress, Is.Zero);
        }
    }
}
