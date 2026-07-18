using NUnit.Framework;
using Ouroboros.Core;
using UnityEngine;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSCombatEventBufferEditModeTests
    {
        [Test]
        public void SameAttack_BodyThenHead_CollapsesToHeadCandidate()
        {
            var buffer = new OSCombatEventBuffer(8);
            var drained = new OSDamageEvent[8];
            Assert.That(buffer.BeginTick(7).IsAccepted, Is.True);
            Assert.That(buffer.EnqueueDamage(Damage(10, 3, OSTargetKind.PlayerBody)).IsAccepted, Is.True);
            Assert.That(buffer.EnqueueDamage(Damage(10, 1, OSTargetKind.PlayerHead)).IsAccepted, Is.True);

            Assert.That(buffer.DrainTo(drained), Is.EqualTo(1));
            Assert.That(drained[0].TargetKind, Is.EqualTo(OSTargetKind.PlayerHead));
            Assert.That(drained[0].CombatTick, Is.EqualTo(7));
        }

        [Test]
        public void SameAttack_HeadThenBody_KeepsHeadAndRejectsBody()
        {
            var buffer = new OSCombatEventBuffer(8);
            var drained = new OSDamageEvent[8];
            buffer.BeginTick(1);
            Assert.That(buffer.EnqueueDamage(Damage(4, 1, OSTargetKind.PlayerHead)).IsAccepted, Is.True);

            var body = buffer.EnqueueDamage(Damage(4, 2, OSTargetKind.PlayerBody));
            Assert.That(body.Code, Is.EqualTo(OSResultCode.Duplicate));
            Assert.That(buffer.DrainTo(drained), Is.EqualTo(1));
            Assert.That(drained[0].TargetKind, Is.EqualTo(OSTargetKind.PlayerHead));
        }

        [Test]
        public void DifferentAttacks_AreDrainedInStableAttackAndTargetOrder()
        {
            var buffer = new OSCombatEventBuffer(8);
            var drained = new OSDamageEvent[8];
            buffer.BeginTick(2);
            buffer.EnqueueDamage(Damage(12, 4, OSTargetKind.PlayerBody));
            buffer.EnqueueDamage(Damage(3, 8, OSTargetKind.PlayerBody));
            buffer.EnqueueDamage(Damage(3, 1, OSTargetKind.PlayerHead));
            buffer.EnqueueDamage(Damage(8, 2, OSTargetKind.PlayerBody));

            Assert.That(buffer.DrainTo(drained), Is.EqualTo(3));
            Assert.That(drained[0].AttackEventId, Is.EqualTo(3));
            Assert.That(drained[0].TargetKind, Is.EqualTo(OSTargetKind.PlayerHead));
            Assert.That(drained[1].AttackEventId, Is.EqualTo(8));
            Assert.That(drained[2].AttackEventId, Is.EqualTo(12));
        }

        [Test]
        public void ExactDuplicateAndCapacity_DoNotMutatePendingBatch()
        {
            var buffer = new OSCombatEventBuffer(1);
            buffer.BeginTick(0);
            var damage = Damage(1, 2, OSTargetKind.PlayerBody);
            Assert.That(buffer.EnqueueDamage(damage).IsAccepted, Is.True);
            Assert.That(buffer.EnqueueDamage(damage).Code, Is.EqualTo(OSResultCode.Duplicate));
            Assert.That(
                buffer.EnqueueDamage(Damage(2, 3, OSTargetKind.PlayerBody)).Code,
                Is.EqualTo(OSResultCode.RejectedCapacity));
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        private static OSDamageEvent Damage(int attackId, int targetId, OSTargetKind targetKind)
        {
            return new OSDamageEvent(
                attackId,
                0,
                99,
                targetId,
                targetKind,
                8f,
                Vector2.zero);
        }
    }
}
