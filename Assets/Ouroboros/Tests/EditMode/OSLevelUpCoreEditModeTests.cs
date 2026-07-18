using NUnit.Framework;
using Ouroboros.Core;
using UnityEditor;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSLevelUpCoreEditModeTests
    {
        private const string CatalogPath = "Assets/Ouroboros/Data/Upgrades/OSUpgradeCatalog.asset";
        private OSUpgradeCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<OSUpgradeCatalog>(CatalogPath);
            Assert.That(_catalog, Is.Not.Null);
        }

        [Test]
        public void Experience_ExactBoundaryAndOverflowArePreserved()
        {
            var progress = new OSExperienceProgress();

            Assert.That(progress.AddExperience(14f).Payload, Is.EqualTo(0));
            Assert.That(progress.Level, Is.EqualTo(1));
            Assert.That(progress.CurrentExperience, Is.EqualTo(14f));

            var boundary = progress.AddExperience(1f);

            Assert.That(boundary.Payload, Is.EqualTo(1));
            Assert.That(progress.Level, Is.EqualTo(2));
            Assert.That(progress.CurrentExperience, Is.EqualTo(0f));
            Assert.That(progress.RequiredExperience, Is.EqualTo(18));
        }

        [Test]
        public void Experience_OneGrantCreatesMultipleRequestsAndKeepsRemainder()
        {
            var progress = new OSExperienceProgress();

            var result = progress.AddExperience(100f);

            Assert.That(result.Payload, Is.EqualTo(4));
            Assert.That(progress.Level, Is.EqualTo(5));
            Assert.That(progress.RequiredExperience, Is.EqualTo(31));
            Assert.That(progress.CurrentExperience, Is.EqualTo(19f).Within(0.001f));
        }

        [Test]
        public void Candidates_AreUniqueDeterministicAndExcludeMaximumLevel()
        {
            var first = new OSUpgradeRunState(_catalog.Entries);
            var second = new OSUpgradeRunState(_catalog.Entries);
            Assert.That(first.Apply("head_damage").IsAccepted, Is.True);
            Assert.That(first.Apply("head_damage").IsAccepted, Is.True);
            Assert.That(first.Apply("head_damage").IsAccepted, Is.True);
            Assert.That(second.Apply("head_damage").IsAccepted, Is.True);
            Assert.That(second.Apply("head_damage").IsAccepted, Is.True);
            Assert.That(second.Apply("head_damage").IsAccepted, Is.True);
            Assert.That(first.Apply("head_damage").Code, Is.EqualTo(OSResultCode.RejectedCapacity));

            var firstCandidates = new OSUpgradeCandidate[3];
            var secondCandidates = new OSUpgradeCandidate[3];
            Assert.That(first.BuildCandidates(4, new OSRunRandom(4401), firstCandidates).IsAccepted, Is.True);
            Assert.That(second.BuildCandidates(4, new OSRunRandom(4401), secondCandidates).IsAccepted, Is.True);

            for (var index = 0; index < 3; index++)
            {
                Assert.That(firstCandidates[index].Id, Is.EqualTo(secondCandidates[index].Id));
                Assert.That(firstCandidates[index].Id, Is.Not.EqualTo("head_damage"));
                for (var other = index + 1; other < 3; other++)
                {
                    Assert.That(firstCandidates[index].Id, Is.Not.EqualTo(firstCandidates[other].Id));
                }
            }
        }

        [Test]
        public void FirstThreeScreens_ContainFirepowerBodyAndSurvival()
        {
            var state = new OSUpgradeRunState(_catalog.Entries);
            var random = new OSRunRandom(12012);
            var candidates = new OSUpgradeCandidate[3];

            for (var ordinal = 1; ordinal <= 3; ordinal++)
            {
                Assert.That(state.BuildCandidates(ordinal, random, candidates).IsAccepted, Is.True);
                Assert.That(ContainsCategory(candidates, OSUpgradeCategory.Firepower), Is.True);
                Assert.That(ContainsCategory(candidates, OSUpgradeCategory.Body), Is.True);
                Assert.That(ContainsCategory(candidates, OSUpgradeCategory.Survival), Is.True);
            }
        }

        [Test]
        public void ApplyingAllOperationsBuildsExpectedRuntimeModifiersWithoutMutatingCatalog()
        {
            var before = EditorJsonUtility.ToJson(_catalog);
            var state = new OSUpgradeRunState(_catalog.Entries);
            foreach (var definition in _catalog.Entries)
            {
                Assert.That(state.Apply(definition.Id).IsAccepted, Is.True, definition.Id);
            }

            var modifiers = state.Modifiers;
            Assert.That(modifiers.HeadDamageMultiplier, Is.EqualTo(1.15f).Within(0.0001f));
            Assert.That(modifiers.HeadRateBonus, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(modifiers.HeadPierceBonus, Is.EqualTo(1));
            Assert.That(modifiers.FragmentRequirementMultiplier, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(modifiers.BodyDamageRateBonus, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(modifiers.RoleCooldownMultiplier, Is.EqualTo(0.92f).Within(0.0001f));
            Assert.That(modifiers.ExplosionRadiusMultiplier, Is.EqualTo(1.15f).Within(0.0001f));
            Assert.That(modifiers.ExplosionDamageMultiplier, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(modifiers.ExplosionConsumeRateDelta, Is.EqualTo(-0.05f).Within(0.0001f));
            Assert.That(modifiers.MaxHealthMultiplier, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(modifiers.MoveSpeedMultiplier, Is.EqualTo(1.08f).Within(0.0001f));
            Assert.That(modifiers.HealMultiplier, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(modifiers.MagnetMultiplier, Is.EqualTo(1.3f).Within(0.0001f));
            Assert.That(modifiers.ExperienceMultiplier, Is.EqualTo(1.1f).Within(0.0001f));
            Assert.That(modifiers.ElitePriority, Is.True);
            Assert.That(EditorJsonUtility.ToJson(_catalog), Is.EqualTo(before));
        }

        [Test]
        public void UpgradeMath_EnforcesFireRateFragmentConsumeAndMoveClamps()
        {
            Assert.That(OSUpgradeMath.CalculateHeadFireInterval(0.5f, 100f), Is.EqualTo(0.15f));
            Assert.That(OSUpgradeMath.CalculateFragmentRequirement(12f, 0.01f), Is.EqualTo(8));
            Assert.That(OSUpgradeMath.CalculateExplosionConsumeRate(0.3f, -1f), Is.EqualTo(0.15f));
            Assert.That(OSUpgradeMath.CalculateMoveSpeed(7f, 2f), Is.EqualTo(7.5f));
        }

        private static bool ContainsCategory(
            OSUpgradeCandidate[] candidates,
            OSUpgradeCategory category)
        {
            for (var index = 0; index < candidates.Length; index++)
            {
                if (candidates[index].Category == category)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
