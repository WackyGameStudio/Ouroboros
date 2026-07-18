using System;
using System.Collections.Generic;
using Ouroboros.Core;
using Ouroboros.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ouroboros.Editor
{
    public static class OSStep02DataSetup
    {
        private const string Root = "Assets/Ouroboros";
        private const string BootScenePath = Root + "/Scenes/00_Boot.unity";
        private const string PlayerPath = Root + "/Data/Balance/OSPlayerBalance.asset";
        private const string BodyPath = Root + "/Data/Balance/OSBodyBalance.asset";
        private const string EncounterPath = Root + "/Data/Enemies/OSEncounterBalance.asset";
        private const string WavesPath = Root + "/Data/Waves/OSWaveSchedule.asset";
        private const string UpgradesPath = Root + "/Data/Upgrades/OSUpgradeCatalog.asset";
        private const string FeedbackPath = Root + "/Data/Balance/OSFeedbackCatalog.asset";
        private const string EnemyPrefabPath = Root + "/Prefabs/Enemies/PF_DataValidationEnemy.prefab";
        private const string DataVersion = "step02-v1";

        [MenuItem("Ouroboros/Setup/Apply Step 02 Data Foundation")]
        public static void ApplyStep02DataFoundation()
        {
            var enemyPrefab = EnsureEnemyPrefab();
            var player = EnsureAsset<OSPlayerBalanceData>(PlayerPath, ConfigurePlayer);
            var body = EnsureAsset<OSBodyBalanceData>(BodyPath, ConfigureBody);
            var encounter = EnsureAsset<OSEncounterBalanceData>(
                EncounterPath,
                serialized => ConfigureEncounter(serialized, enemyPrefab));
            var waves = EnsureAsset<OSWaveScheduleData>(WavesPath, ConfigureWaves);
            var upgrades = EnsureAsset<OSUpgradeCatalog>(UpgradesPath, ConfigureUpgrades);
            var feedback = EnsureAsset<OSFeedbackCatalog>(FeedbackPath, ConfigureFeedback);

            AssignBootData(player, body, encounter, waves, upgrades, feedback);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OUROBOROS][SETUP] Step 02 data foundation applied.");
        }

        private static T EnsureAsset<T>(string path, Action<SerializedObject> configure)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            var serialized = new SerializedObject(asset);
            configure(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static GameObject EnsureEnemyPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (prefab != null)
            {
                return prefab;
            }

            var instance = new GameObject("PF_DataValidationEnemy", typeof(SpriteRenderer));
            var renderer = instance.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                Root + "/Art/Placeholders/Enemy_Chaser.png");
            prefab = PrefabUtility.SaveAsPrefabAsset(instance, EnemyPrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static void ConfigurePlayer(SerializedObject serialized)
        {
            SetString(serialized, "dataVersion", DataVersion);
            SetInt(serialized, "maxHealth", 100);
            SetFloat(serialized, "moveSpeed", 5.5f);
            SetFloat(serialized, "hitInvulnerability", 0.6f);
            SetFloat(serialized, "headDamage", 10f);
            SetFloat(serialized, "headFireInterval", 0.5f);
            SetFloat(serialized, "headRange", 6f);
            SetFloat(serialized, "magnetRadius", 1.25f);
        }

        private static void ConfigureBody(SerializedObject serialized)
        {
            SetString(serialized, "dataVersion", DataVersion);
            SetInt(serialized, "fragmentRequirement", 12);
            SetInt(serialized, "technicalGuard", 64);
            SetFloat(serialized, "segmentSpacing", 0.55f);
            SetFloat(serialized, "pathSampleInterval", 0.12f);
            SetFloat(serialized, "pathReserveDistance", 4f);
            SetFloat(serialized, "bodyDamageRate", 0.04f);
            SetFloat(serialized, "cutGuardDuration", 0.35f);

            var roles = Require(serialized, "roleDefinitions");
            roles.arraySize = 4;
            ConfigureRole(roles.GetArrayElementAtIndex(0), "shield", OSBodyRoleType.Shield,
                range: 0f, damage: 0f, interval: 0f, radius: 1.5f, charges: 1,
                recharge: 6f, beamWidth: 0f, telegraph: 0f, normalControl: 0f, eliteControl: 0f);
            ConfigureRole(roles.GetArrayElementAtIndex(1), "attack", OSBodyRoleType.Attack,
                range: 6f, damage: 6f, interval: 1f, radius: 0f, charges: 0,
                recharge: 0f, beamWidth: 0f, telegraph: 0f, normalControl: 0f, eliteControl: 0f);
            ConfigureRole(roles.GetArrayElementAtIndex(2), "laser", OSBodyRoleType.Laser,
                range: 7f, damage: 12f, interval: 2.5f, radius: 0f, charges: 0,
                recharge: 0f, beamWidth: 0.35f, telegraph: 0.2f, normalControl: 0f, eliteControl: 0f);
            ConfigureRole(roles.GetArrayElementAtIndex(3), "control", OSBodyRoleType.Control,
                range: 6f, damage: 0f, interval: 4f, radius: 0f, charges: 0,
                recharge: 0f, beamWidth: 0f, telegraph: 0f, normalControl: 1f, eliteControl: 0.5f);

            var explosion = Require(serialized, "explosion");
            explosion.FindPropertyRelative("minimumSegments").intValue = 4;
            explosion.FindPropertyRelative("consumeRate").floatValue = 0.3f;
            explosion.FindPropertyRelative("telegraphDuration").floatValue = 0.25f;
            explosion.FindPropertyRelative("radius").floatValue = 1.8f;
            explosion.FindPropertyRelative("damagePerSegment").floatValue = 35f;
            explosion.FindPropertyRelative("headInvulnerability").floatValue = 0.4f;
        }

        private static void ConfigureRole(
            SerializedProperty property,
            string id,
            OSBodyRoleType type,
            float range,
            float damage,
            float interval,
            float radius,
            int charges,
            float recharge,
            float beamWidth,
            float telegraph,
            float normalControl,
            float eliteControl)
        {
            property.FindPropertyRelative("id").stringValue = id;
            property.FindPropertyRelative("roleType").enumValueIndex = (int)type;
            property.FindPropertyRelative("range").floatValue = range;
            property.FindPropertyRelative("damage").floatValue = damage;
            property.FindPropertyRelative("interval").floatValue = interval;
            property.FindPropertyRelative("radius").floatValue = radius;
            property.FindPropertyRelative("charges").intValue = charges;
            property.FindPropertyRelative("rechargeDuration").floatValue = recharge;
            property.FindPropertyRelative("beamWidth").floatValue = beamWidth;
            property.FindPropertyRelative("telegraphDuration").floatValue = telegraph;
            property.FindPropertyRelative("normalControlDuration").floatValue = normalControl;
            property.FindPropertyRelative("eliteControlDuration").floatValue = eliteControl;
        }

        private static void ConfigureEncounter(SerializedObject serialized, GameObject prefab)
        {
            SetString(serialized, "dataVersion", DataVersion);
            SetInt(serialized, "activeEnemyLimit", 180);
            SetInt(serialized, "projectileLimit", 120);
            SetInt(serialized, "pickupLimit", 256);
            SetInt(serialized, "vfxLimit", 160);

            var enemies = Require(serialized, "enemyDefinitions");
            enemies.arraySize = 5;
            ConfigureEnemy(enemies.GetArrayElementAtIndex(0), "enemy_chaser", OSEnemyArchetype.Chaser,
                prefab, 18f, 2.1f, 8f, 1f, 64);
            ConfigureEnemy(enemies.GetArrayElementAtIndex(1), "enemy_charger", OSEnemyArchetype.Charger,
                prefab, 42f, 1.6f, 14f, 1.5f, 32);
            ConfigureEnemy(enemies.GetArrayElementAtIndex(2), "enemy_shooter", OSEnemyArchetype.Shooter,
                prefab, 32f, 1.4f, 10f, 1.5f, 32);
            ConfigureEnemy(enemies.GetArrayElementAtIndex(3), "enemy_splitter", OSEnemyArchetype.Splitter,
                prefab, 36f, 1.5f, 7f, 1f, 32);
            ConfigureEnemy(enemies.GetArrayElementAtIndex(4), "enemy_splitter_spawn",
                OSEnemyArchetype.SplitterSpawn, prefab, 12f, 2.4f, 4f, 1f, 64);
            ConfigureEnemy(Require(serialized, "eliteDefinition"), "enemy_elite_accelerator",
                OSEnemyArchetype.EliteAccelerator, prefab, 650f, 1.8f, 16f, 1f, 4);
            ConfigureEnemy(Require(serialized, "bossDefinition"), "boss_swarm_core",
                OSEnemyArchetype.BossSwarmCore, prefab, 6000f, 1.2f, 20f, 1.5f, 1);
        }

        private static void ConfigureEnemy(
            SerializedProperty property,
            string id,
            OSEnemyArchetype archetype,
            GameObject prefab,
            float health,
            float speed,
            float damage,
            float interval,
            int capacity)
        {
            property.FindPropertyRelative("id").stringValue = id;
            property.FindPropertyRelative("archetype").enumValueIndex = (int)archetype;
            property.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            property.FindPropertyRelative("maxHealth").floatValue = health;
            property.FindPropertyRelative("moveSpeed").floatValue = speed;
            property.FindPropertyRelative("contactDamage").floatValue = damage;
            property.FindPropertyRelative("attackInterval").floatValue = interval;
            property.FindPropertyRelative("poolCapacity").intValue = capacity;
            property.FindPropertyRelative("controlAffectsMovement").boolValue = true;
            property.FindPropertyRelative("controlAffectsAttack").boolValue = false;

            var drop = property.FindPropertyRelative("dropTable");
            drop.FindPropertyRelative("experienceAmount").intValue = 1;
            drop.FindPropertyRelative("fragmentAmount").intValue = 1;
            drop.FindPropertyRelative("fragmentChance").floatValue = 0.15f;
            drop.FindPropertyRelative("healAmount").intValue = 10;
            drop.FindPropertyRelative("healChance").floatValue = 0.02f;
        }

        private static void ConfigureWaves(SerializedObject serialized)
        {
            SetString(serialized, "dataVersion", DataVersion);
            var entries = Require(serialized, "entries");
            entries.arraySize = 12;
            ConfigureWave(entries.GetArrayElementAtIndex(0), 0f, 60f, 0.5f, 25,
                OSWaveSpecialEvent.None, ("enemy_chaser", 1f));
            ConfigureWave(entries.GetArrayElementAtIndex(1), 60f, 120f, 0.75f, 45,
                OSWaveSpecialEvent.None, ("enemy_chaser", 0.8f), ("enemy_charger", 0.2f));
            ConfigureWave(entries.GetArrayElementAtIndex(2), 120f, 180f, 1f, 65,
                OSWaveSpecialEvent.None, ("enemy_chaser", 0.65f), ("enemy_charger", 0.2f),
                ("enemy_shooter", 0.15f));
            ConfigureWave(entries.GetArrayElementAtIndex(3), 180f, 181f, 0f, 65,
                OSWaveSpecialEvent.EliteAccelerator);
            ConfigureWave(entries.GetArrayElementAtIndex(4), 181f, 240f, 1.2f, 85,
                OSWaveSpecialEvent.None, ("enemy_chaser", 0.5f), ("enemy_charger", 0.25f),
                ("enemy_shooter", 0.25f));
            ConfigureWave(entries.GetArrayElementAtIndex(5), 240f, 300f, 1.4f, 105,
                OSWaveSpecialEvent.None, ("enemy_chaser", 0.4f), ("enemy_charger", 0.2f),
                ("enemy_shooter", 0.2f), ("enemy_splitter", 0.2f));
            ConfigureWave(entries.GetArrayElementAtIndex(6), 300f, 360f, 1.6f, 125,
                OSWaveSpecialEvent.None, ("enemy_chaser", 0.3f), ("enemy_charger", 0.25f),
                ("enemy_shooter", 0.25f), ("enemy_splitter", 0.2f));
            ConfigureWave(entries.GetArrayElementAtIndex(7), 360f, 361f, 0f, 125,
                OSWaveSpecialEvent.EliteAccelerator);
            ConfigureWave(entries.GetArrayElementAtIndex(8), 361f, 480f, 1.8f, 150,
                OSWaveSpecialEvent.None, ("enemy_chaser", 0.3f), ("enemy_charger", 0.25f),
                ("enemy_shooter", 0.2f), ("enemy_splitter", 0.25f));
            ConfigureWave(entries.GetArrayElementAtIndex(9), 480f, 540f, 2f, 155,
                OSWaveSpecialEvent.None, ("enemy_chaser", 0.2f), ("enemy_charger", 0.4f),
                ("enemy_shooter", 0.3f), ("enemy_splitter", 0.1f));
            ConfigureWave(entries.GetArrayElementAtIndex(10), 540f, 600f, 2f, 160,
                OSWaveSpecialEvent.BossWarning, ("enemy_chaser", 0.3f), ("enemy_charger", 0.25f),
                ("enemy_shooter", 0.25f), ("enemy_splitter", 0.2f));
            ConfigureWave(entries.GetArrayElementAtIndex(11), 600f, 690f, 2f, 180,
                OSWaveSpecialEvent.BossSwarmCore, ("enemy_chaser", 0.45f),
                ("enemy_splitter", 0.55f));
        }

        private static void ConfigureWave(
            SerializedProperty property,
            float start,
            float end,
            float spawnRate,
            int target,
            OSWaveSpecialEvent specialEvent,
            params (string id, float weight)[] weights)
        {
            property.FindPropertyRelative("startSeconds").floatValue = start;
            property.FindPropertyRelative("endSeconds").floatValue = end;
            property.FindPropertyRelative("spawnRate").floatValue = spawnRate;
            property.FindPropertyRelative("specialEvent").enumValueIndex = (int)specialEvent;
            property.FindPropertyRelative("targetActiveEnemies").intValue = target;
            var serializedWeights = property.FindPropertyRelative("enemyWeights");
            serializedWeights.arraySize = weights.Length;
            for (var i = 0; i < weights.Length; i++)
            {
                var weight = serializedWeights.GetArrayElementAtIndex(i);
                weight.FindPropertyRelative("enemyId").stringValue = weights[i].id;
                weight.FindPropertyRelative("weight").floatValue = weights[i].weight;
            }
        }

        private static void ConfigureUpgrades(SerializedObject serialized)
        {
            SetString(serialized, "dataVersion", DataVersion);
            var entries = Require(serialized, "entries");
            entries.arraySize = 15;
            ConfigureUpgrade(entries, 0, "head_damage", OSUpgradeCategory.Firepower,
                OSUpgradeOperation.AddHeadDamageMultiplier, 0.15f, 3, 0f, 10f);
            ConfigureUpgrade(entries, 1, "head_rate", OSUpgradeCategory.Firepower,
                OSUpgradeOperation.AddHeadRateMultiplier, 0.12f, 3, 0.15f, 10f);
            ConfigureUpgrade(entries, 2, "head_pierce", OSUpgradeCategory.Firepower,
                OSUpgradeOperation.AddHeadPierce, 1f, 3, 0f, 3f);
            ConfigureUpgrade(entries, 3, "body_fragment_efficiency", OSUpgradeCategory.Body,
                OSUpgradeOperation.AddFragmentRequirementMultiplier, -0.1f, 2, 8f, 12f);
            ConfigureUpgrade(entries, 4, "body_damage_rate", OSUpgradeCategory.Body,
                OSUpgradeOperation.AddBodyDamageRate, 0.01f, 2, 0.04f, 0.06f);
            ConfigureUpgrade(entries, 5, "role_overclock", OSUpgradeCategory.Body,
                OSUpgradeOperation.AddRoleCooldownMultiplier, -0.08f, 3, 0.5f, 1f);
            ConfigureUpgrade(entries, 6, "explosion_radius", OSUpgradeCategory.Explosion,
                OSUpgradeOperation.AddExplosionRadiusMultiplier, 0.15f, 3, 1f, 3f);
            ConfigureUpgrade(entries, 7, "explosion_damage", OSUpgradeCategory.Explosion,
                OSUpgradeOperation.AddExplosionDamageMultiplier, 0.2f, 3, 1f, 3f);
            ConfigureUpgrade(entries, 8, "explosion_efficiency", OSUpgradeCategory.Explosion,
                OSUpgradeOperation.AddExplosionConsumeRate, -0.05f, 3, 0.15f, 0.3f);
            ConfigureUpgrade(entries, 9, "max_health", OSUpgradeCategory.Survival,
                OSUpgradeOperation.AddMaxHealth, 0.2f, 2, 0f, 10f);
            ConfigureUpgrade(entries, 10, "move_speed", OSUpgradeCategory.Survival,
                OSUpgradeOperation.AddMoveSpeedMultiplier, 0.08f, 2, 0f, 7.5f);
            ConfigureUpgrade(entries, 11, "heal_amount", OSUpgradeCategory.Survival,
                OSUpgradeOperation.AddHealMultiplier, 0.25f, 2, 0f, 10f);
            ConfigureUpgrade(entries, 12, "magnet_radius", OSUpgradeCategory.Utility,
                OSUpgradeOperation.AddMagnetMultiplier, 0.3f, 2, 0f, 10f);
            ConfigureUpgrade(entries, 13, "experience_gain", OSUpgradeCategory.Utility,
                OSUpgradeOperation.AddExperienceMultiplier, 0.1f, 2, 0f, 10f);
            ConfigureUpgrade(entries, 14, "elite_priority", OSUpgradeCategory.Utility,
                OSUpgradeOperation.EnableElitePriority, 1f, 1, 0f, 1f);
        }

        private static void ConfigureUpgrade(
            SerializedProperty entries,
            int index,
            string id,
            OSUpgradeCategory category,
            OSUpgradeOperation operation,
            float perLevel,
            int maxLevel,
            float clampMin,
            float clampMax)
        {
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("category").enumValueIndex = (int)category;
            entry.FindPropertyRelative("operation").enumValueIndex = (int)operation;
            entry.FindPropertyRelative("perLevelValue").floatValue = perLevel;
            entry.FindPropertyRelative("maxLevel").intValue = maxLevel;
            entry.FindPropertyRelative("clampMinimum").floatValue = clampMin;
            entry.FindPropertyRelative("clampMaximum").floatValue = clampMax;
            entry.FindPropertyRelative("candidateWeight").floatValue = 1f;
        }

        private static void ConfigureFeedback(SerializedObject serialized)
        {
            SetString(serialized, "dataVersion", DataVersion);
            var visuals = Require(serialized, "roleVisuals");
            visuals.arraySize = 4;
            ConfigureVisual(visuals.GetArrayElementAtIndex(0), "shield", OSBodyRoleType.Shield,
                "Body_Shield", "cross_ring", new Color32(61, 139, 255, 255));
            ConfigureVisual(visuals.GetArrayElementAtIndex(1), "attack", OSBodyRoleType.Attack,
                "Body_Attack", "arrow", new Color32(255, 77, 92, 255));
            ConfigureVisual(visuals.GetArrayElementAtIndex(2), "laser", OSBodyRoleType.Laser,
                "Body_Laser", "double_bar", new Color32(222, 74, 255, 255));
            ConfigureVisual(visuals.GetArrayElementAtIndex(3), "control", OSBodyRoleType.Control,
                "Body_Control", "diamond_cross", new Color32(65, 230, 145, 255));
            SetStringArray(serialized, "attackVfxKeys", "head_projectile", "body_projectile", "laser_beam");
            SetStringArray(serialized, "telegraphKeys", "charger_line", "laser_line", "explosion_reserved");
            SetStringArray(serialized, "audioKeys", "ui_select", "hit_head", "cut_body", "explosion");
        }

        private static void ConfigureVisual(
            SerializedProperty property,
            string id,
            OSBodyRoleType type,
            string spriteName,
            string patternKey,
            Color32 color)
        {
            property.FindPropertyRelative("id").stringValue = id;
            property.FindPropertyRelative("roleType").enumValueIndex = (int)type;
            property.FindPropertyRelative("sprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Art/Placeholders/{spriteName}.png");
            property.FindPropertyRelative("color").colorValue = color;
            property.FindPropertyRelative("patternKey").stringValue = patternKey;
        }

        private static void AssignBootData(
            OSPlayerBalanceData player,
            OSBodyBalanceData body,
            OSEncounterBalanceData encounter,
            OSWaveScheduleData waves,
            OSUpgradeCatalog upgrades,
            OSFeedbackCatalog feedback)
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Additive);
            try
            {
                OSBootstrap bootstrap = null;
                var roots = scene.GetRootGameObjects();
                for (var i = 0; i < roots.Length && bootstrap == null; i++)
                {
                    bootstrap = roots[i].GetComponentInChildren<OSBootstrap>(true);
                }

                if (bootstrap == null)
                {
                    throw new InvalidOperationException($"OSBootstrap is missing from '{BootScenePath}'.");
                }

                var serialized = new SerializedObject(bootstrap);
                Require(serialized, "playerBalanceData").objectReferenceValue = player;
                Require(serialized, "bodyBalanceData").objectReferenceValue = body;
                Require(serialized, "encounterBalanceData").objectReferenceValue = encounter;
                Require(serialized, "waveScheduleData").objectReferenceValue = waves;
                Require(serialized, "upgradeCatalog").objectReferenceValue = upgrades;
                Require(serialized, "feedbackCatalog").objectReferenceValue = feedback;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void SetString(SerializedObject serialized, string name, string value)
        {
            Require(serialized, name).stringValue = value;
        }

        private static void SetInt(SerializedObject serialized, string name, int value)
        {
            Require(serialized, name).intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string name, float value)
        {
            Require(serialized, name).floatValue = value;
        }

        private static void SetStringArray(SerializedObject serialized, string name, params string[] values)
        {
            var property = Require(serialized, name);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private static SerializedProperty Require(SerializedObject serialized, string name)
        {
            return serialized.FindProperty(name)
                   ?? throw new InvalidOperationException(
                       $"Serialized property '{name}' is missing on {serialized.targetObject.GetType().Name}.");
        }
    }
}
