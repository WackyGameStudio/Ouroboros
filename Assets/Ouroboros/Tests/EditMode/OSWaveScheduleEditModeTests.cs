using NUnit.Framework;
using Ouroboros.Core;
using UnityEditor;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSWaveScheduleEditModeTests
    {
        private const string WavePath = "Assets/Ouroboros/Data/Waves/OSWaveSchedule.asset";
        private OSWaveScheduleData _waves;
        private OSWaveScheduleRuntime _runtime;

        [SetUp]
        public void SetUp()
        {
            _waves = AssetDatabase.LoadAssetAtPath<OSWaveScheduleData>(WavePath);
            Assert.That(_waves, Is.Not.Null);
            Assert.That(_waves.Validate().IsValid, Is.True, _waves.LastValidationMessage);
            _runtime = new OSWaveScheduleRuntime(_waves.Entries);
        }

        [Test]
        public void Schedule_CoversZeroToTenMinutesWithoutOverlapAndKeepsStep14BossDeferred()
        {
            for (var second = 0; second < 600; second++)
            {
                Assert.That(_runtime.FindEntryIndex(second + 0.5f), Is.GreaterThanOrEqualTo(0),
                    $"No wave entry at {second}s.");
            }

            Assert.That(_runtime.Count, Is.EqualTo(12));
            Assert.That(_runtime.GetEntry(10).StartSeconds, Is.EqualTo(540f));
            Assert.That(_runtime.GetEntry(10).SpecialEvent, Is.EqualTo(OSWaveSpecialEvent.BossWarning));
            Assert.That(_runtime.GetEntry(11).StartSeconds, Is.EqualTo(600f));
            Assert.That(_runtime.GetEntry(11).SpecialEvent, Is.EqualTo(OSWaveSpecialEvent.BossSwarmCore));
        }

        [Test]
        public void SpecialEvents_AreExactlyAtThreeSixAndNineMinutes()
        {
            Assert.That(_runtime.GetEntry(3).StartSeconds, Is.EqualTo(180f));
            Assert.That(_runtime.GetEntry(3).SpecialEvent,
                Is.EqualTo(OSWaveSpecialEvent.EliteAccelerator));
            Assert.That(_runtime.GetEntry(7).StartSeconds, Is.EqualTo(360f));
            Assert.That(_runtime.GetEntry(7).SpecialEvent,
                Is.EqualTo(OSWaveSpecialEvent.EliteAccelerator));
            Assert.That(_runtime.GetEntry(10).StartSeconds, Is.EqualTo(540f));
            Assert.That(_runtime.GetEntry(10).SpecialEvent,
                Is.EqualTo(OSWaveSpecialEvent.BossWarning));
        }

        [Test]
        public void HealthAndReducedSpawnMultipliers_CompoundPerMinuteWithoutMutatingData()
        {
            var before = EditorJsonUtility.ToJson(_waves);

            Assert.That(OSWaveScheduleRuntime.CalculateHealthMultiplier(0f), Is.EqualTo(1f));
            Assert.That(OSWaveScheduleRuntime.CalculateHealthMultiplier(60f),
                Is.EqualTo(1.12f).Within(0.0001f));
            Assert.That(OSWaveScheduleRuntime.CalculateHealthMultiplier(600f),
                Is.EqualTo((float)System.Math.Pow(1.12d, 10d)).Within(0.0001f));
            Assert.That(OSWaveScheduleRuntime.CalculateSpawnRateMultiplier(60f),
                Is.EqualTo(1.08f).Within(0.0001f));
            Assert.That(OSWaveScheduleRuntime.CalculateSpawnRateMultiplier(600f),
                Is.EqualTo((float)System.Math.Pow(1.08d, 10d)).Within(0.0001f));
            Assert.That(EditorJsonUtility.ToJson(_waves), Is.EqualTo(before));
        }

        [Test]
        public void SpawnPacing_UsesStep15Point9ReducedBaseRates()
        {
            var expectedRates = new[]
            {
                0.4f, 0.6f, 0.8f, 0f, 0.96f, 1.12f,
                1.28f, 0f, 1.44f, 1.6f, 1.6f, 1.6f
            };

            Assert.That(_runtime.Count, Is.EqualTo(expectedRates.Length));
            for (var index = 0; index < expectedRates.Length; index++)
            {
                Assert.That(_runtime.GetEntry(index).SpawnRate,
                    Is.EqualTo(expectedRates[index]).Within(0.0001f),
                    $"Unexpected spawn rate at wave entry {index}.");
            }
        }

        [Test]
        public void WeightedSelection_IsDeterministicAndIncludesAllFourRegularArchetypes()
        {
            var first = new OSRunRandom(13013);
            var second = new OSRunRandom(13013);
            var seenChaser = false;
            var seenCharger = false;
            var seenShooter = false;
            var seenSplitter = false;

            var mixedEntry = _runtime.FindEntryIndex(300f);
            for (var index = 0; index < 200; index++)
            {
                var firstId = _runtime.SelectEnemyId(mixedEntry, first);
                Assert.That(firstId, Is.EqualTo(_runtime.SelectEnemyId(mixedEntry, second)));
                seenChaser |= firstId == "enemy_chaser";
                seenCharger |= firstId == "enemy_charger";
                seenShooter |= firstId == "enemy_shooter";
                seenSplitter |= firstId == "enemy_splitter";
            }

            Assert.That(seenChaser && seenCharger && seenShooter && seenSplitter, Is.True);
        }

        [Test]
        public void SpawnGate_DefersAtTargetAndHardCap()
        {
            Assert.That(OSWaveScheduleRuntime.CanSpawn(24, 180, 25), Is.True);
            Assert.That(OSWaveScheduleRuntime.CanSpawn(25, 180, 25), Is.False);
            Assert.That(OSWaveScheduleRuntime.CanSpawn(179, 180, 200), Is.True);
            Assert.That(OSWaveScheduleRuntime.CanSpawn(180, 180, 200), Is.False);
        }
    }
}
