using NUnit.Framework;
using Ouroboros.Core;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSTutorialProgressEditModeTests
    {
        [Test]
        public void FirstSession_ProgressesThroughConditionalSequence()
        {
            var progress = new OSTutorialProgress();

            Assert.That(progress.BeginFirstSession(), Is.True);
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.Movement));
            progress.Advance(1f, true);
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.AutoAttack));

            progress.NotifyEnemyDefeated();
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.WaitForFragment));
            progress.NotifyFragmentCollected();
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.BodyGrowth));
            progress.NotifyRoleConfirmed();
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.BodyDash));

            progress.NotifyBodyDashResolved();
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.WaitForCut));
            progress.NotifyBodyCut();
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.CutDifference));
            progress.Advance(3f, false);
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.Completed));
        }

        [Test]
        public void Movement_RequiresActualInputForOneUnscaledSecond()
        {
            var progress = new OSTutorialProgress();
            progress.BeginFirstSession();

            progress.Advance(5f, false);
            progress.Advance(0.6f, true);
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.Movement));
            progress.Advance(0.4f, true);

            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.AutoAttack));
        }

        [Test]
        public void BodyDashHint_TimesOutAfterTwentyUnscaledSeconds()
        {
            var progress = ReachBodyDash();

            progress.Advance(19.99f, false);
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.BodyDash));
            progress.Advance(0.01f, false);

            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.WaitForCut));
        }

        [Test]
        public void EarlyCut_IsRememberedUntilBodyDashHintEnds()
        {
            var progress = ReachBodyDash();
            progress.NotifyBodyCut();
            progress.NotifyBodyDashResolved();

            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.CutDifference));
        }

        [Test]
        public void Restart_DoesNotStartTutorialTwice()
        {
            var progress = new OSTutorialProgress();

            Assert.That(progress.BeginFirstSession(), Is.True);
            progress.Advance(1f, true);
            Assert.That(progress.BeginFirstSession(), Is.False);
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.AutoAttack));
        }

        private static OSTutorialProgress ReachBodyDash()
        {
            var progress = new OSTutorialProgress();
            progress.BeginFirstSession();
            progress.Advance(1f, true);
            progress.NotifyFragmentCollected();
            progress.NotifyEnemyDefeated();
            progress.NotifyRoleConfirmed();
            Assert.That(progress.Stage, Is.EqualTo(OSTutorialStage.BodyDash));
            return progress;
        }
    }
}
