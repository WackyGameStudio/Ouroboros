using System;
using NUnit.Framework;
using Ouroboros.Core;
using UnityEditor;
using UnityEngine;

namespace Ouroboros.Tests.EditMode
{
    public sealed class OSDataValidationEditModeTests
    {
        private DataFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _fixture = DataFixture.Create();
        }

        [TearDown]
        public void TearDown()
        {
            _fixture.Dispose();
        }

        [Test]
        public void ProjectDataPassesCrossValidation()
        {
            var result = _fixture.Validate();

            Assert.That(result.Code, Is.EqualTo(OSResultCode.Accepted), result.Payload.Message);
            Assert.That(result.Payload.IsValid, Is.True, result.Payload.Message);
        }

        [Test]
        public void NullEnemyPrefabIsConfigurationError()
        {
            SetObjectReference(_fixture.Encounter, "enemyDefinitions.Array.data[0].prefab", null);

            var result = _fixture.Validate();

            AssertConfigurationError(result, "required prefab is missing");
        }

        [Test]
        public void DuplicateEnemyAndUpgradeIdsAreRejected()
        {
            SetString(_fixture.Encounter, "enemyDefinitions.Array.data[1].id", "enemy_chaser");
            SetString(_fixture.Upgrades, "entries.Array.data[1].id", "head_damage");

            var result = _fixture.Validate();

            AssertConfigurationError(result, "duplicate ID 'enemy_chaser'");
            Assert.That(result.Payload.Message, Does.Contain("duplicate ID 'head_damage'"));
        }

        [Test]
        public void ZeroFragmentRequirementIsRejected()
        {
            SetInt(_fixture.Body, "fragmentRequirement", 0);

            var result = _fixture.Validate();

            AssertConfigurationError(result, "fragmentRequirement");
        }

        [Test]
        public void NegativeTimeDistanceAndDamageAreRejected()
        {
            SetFloat(_fixture.Waves, "entries.Array.data[0].startSeconds", -1f);
            SetFloat(_fixture.Body, "segmentSpacing", -0.5f);
            SetFloat(_fixture.Player, "headDamage", -10f);

            var result = _fixture.Validate();

            AssertConfigurationError(result, "startSeconds");
            Assert.That(result.Payload.Message, Does.Contain("segmentSpacing"));
            Assert.That(result.Payload.Message, Does.Contain("headDamage"));
        }

        [Test]
        public void NaNAndInfinityAreRejected()
        {
            SetFloat(_fixture.Player, "moveSpeed", float.NaN);
            SetFloat(_fixture.Waves, "entries.Array.data[0].endSeconds", float.PositiveInfinity);

            var result = _fixture.Validate();

            AssertConfigurationError(result, "moveSpeed");
            Assert.That(result.Payload.Message, Does.Contain("endSeconds"));
        }

        [Test]
        public void FewerThanThreeEligibleUpgradeCandidatesAreRejected()
        {
            var serialized = new SerializedObject(_fixture.Upgrades);
            serialized.FindProperty("entries").arraySize = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var result = _fixture.Validate();

            AssertConfigurationError(result, "at least 3 eligible upgrade candidates");
        }

        [Test]
        public void InitializeFromCreatesIndependentRuntimeStateWithoutMutatingAssets()
        {
            var before = _fixture.SerializedHashSource();

            var first = _fixture.Initialize();
            var second = _fixture.Initialize();

            Assert.That(first.IsAccepted, Is.True);
            Assert.That(second.IsAccepted, Is.True);
            Assert.That(first.Payload, Is.Not.SameAs(second.Payload));
            Assert.That(first.Payload.BodyRoles, Is.Not.SameAs(_fixture.Body.RoleDefinitions));
            Assert.That(first.Payload.WaveEntries, Is.Not.SameAs(_fixture.Waves.Entries));
            Assert.That(first.Payload.UpgradeDefinitions, Is.Not.SameAs(_fixture.Upgrades.Entries));
            Assert.That(_fixture.SerializedHashSource(), Is.EqualTo(before));

            var originalRuntimeRoleId = first.Payload.BodyRoles[0].Id;
            SetString(_fixture.Body, "roleDefinitions.Array.data[0].id", "changed_after_copy");
            Assert.That(first.Payload.BodyRoles[0].Id, Is.EqualTo(originalRuntimeRoleId));
        }

        [Test]
        public void MissingRequiredAssetReturnsConfigurationErrorNotRuleRejection()
        {
            var result = OSDataValidator.Validate(
                null,
                _fixture.Body,
                _fixture.Encounter,
                _fixture.Waves,
                _fixture.Upgrades,
                _fixture.Feedback);

            AssertConfigurationError(result, "required data asset is missing");
            Assert.That(result.Code, Is.Not.EqualTo(OSResultCode.RejectedState));
            Assert.That(result.Code, Is.Not.EqualTo(OSResultCode.RejectedRequirement));
        }

        private static void AssertConfigurationError(
            OSRuleResult<OSDataValidationReport> result,
            string expectedMessage)
        {
            Assert.That(result.Code, Is.EqualTo(OSResultCode.ConfigurationError));
            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Payload, Is.Not.Null);
            Assert.That(result.Payload.Message, Does.Contain(expectedMessage));
        }

        private static void SetString(UnityEngine.Object target, string path, string value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, $"Missing property: {path}");
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(UnityEngine.Object target, string path, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, $"Missing property: {path}");
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string path, float value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, $"Missing property: {path}");
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(UnityEngine.Object target, string path, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(path);
            Assert.That(property, Is.Not.Null, $"Missing property: {path}");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class DataFixture : IDisposable
        {
            private const string Root = "Assets/Ouroboros/Data";

            private DataFixture()
            {
                Player = Clone<OSPlayerBalanceData>(Root + "/Balance/OSPlayerBalance.asset");
                Body = Clone<OSBodyBalanceData>(Root + "/Balance/OSBodyBalance.asset");
                Encounter = Clone<OSEncounterBalanceData>(Root + "/Enemies/OSEncounterBalance.asset");
                Waves = Clone<OSWaveScheduleData>(Root + "/Waves/OSWaveSchedule.asset");
                Upgrades = Clone<OSUpgradeCatalog>(Root + "/Upgrades/OSUpgradeCatalog.asset");
                Feedback = Clone<OSFeedbackCatalog>(Root + "/Balance/OSFeedbackCatalog.asset");
            }

            public OSPlayerBalanceData Player { get; }
            public OSBodyBalanceData Body { get; }
            public OSEncounterBalanceData Encounter { get; }
            public OSWaveScheduleData Waves { get; }
            public OSUpgradeCatalog Upgrades { get; }
            public OSFeedbackCatalog Feedback { get; }

            public static DataFixture Create()
            {
                return new DataFixture();
            }

            public OSRuleResult<OSDataValidationReport> Validate()
            {
                return OSDataValidator.Validate(Player, Body, Encounter, Waves, Upgrades, Feedback);
            }

            public OSRuleResult<OSSessionRuntimeState> Initialize()
            {
                return OSSessionRuntimeState.InitializeFrom(
                    Player,
                    Body,
                    Encounter,
                    Waves,
                    Upgrades,
                    Feedback);
            }

            public string SerializedHashSource()
            {
                return string.Join(
                    "\n",
                    EditorJsonUtility.ToJson(Player),
                    EditorJsonUtility.ToJson(Body),
                    EditorJsonUtility.ToJson(Encounter),
                    EditorJsonUtility.ToJson(Waves),
                    EditorJsonUtility.ToJson(Upgrades),
                    EditorJsonUtility.ToJson(Feedback));
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Player);
                UnityEngine.Object.DestroyImmediate(Body);
                UnityEngine.Object.DestroyImmediate(Encounter);
                UnityEngine.Object.DestroyImmediate(Waves);
                UnityEngine.Object.DestroyImmediate(Upgrades);
                UnityEngine.Object.DestroyImmediate(Feedback);
            }

            private static T Clone<T>(string path)
                where T : ScriptableObject
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                Assert.That(asset, Is.Not.Null, $"Missing Step 02 data asset: {path}");
                return UnityEngine.Object.Instantiate(asset);
            }
        }
    }
}
