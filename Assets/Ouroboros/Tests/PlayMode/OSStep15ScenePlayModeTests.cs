using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ouroboros.Core;
using Ouroboros.Runtime;
using Ouroboros.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Ouroboros.Tests.PlayMode
{
    public sealed class OSStep15ScenePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameScene_ConsolidatedHudTutorialFeedbackAndSortingAreConnected()
        {
            yield return LoadGame();

            var canvas = GameObject.Find("Canvas")?.transform;
            var hud = Object.FindAnyObjectByType<OSCombatHudPresenter>(FindObjectsInactive.Include);
            var tutorial = Object.FindAnyObjectByType<OSTutorialPresenter>(FindObjectsInactive.Include);
            var feedback = Object.FindAnyObjectByType<OSCombatFeedbackPresenter>(FindObjectsInactive.Include);
            var animators = Object.FindObjectsByType<OSSelectionPanelAnimator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var head = GameObject.Find("Head")?.transform;

            Assert.That(hud, Is.Not.Null);
            Assert.That(tutorial, Is.Not.Null);
            Assert.That(feedback, Is.Not.Null);
            Assert.That(animators.Length, Is.EqualTo(2));
            Assert.That(canvas?.Find("ReadabilityHUD/CombatSummaryPanel/PrimaryLabel"), Is.Not.Null);
            var healthFill = canvas?.Find(
                    "ReadabilityHUD/CombatSummaryPanel/HealthBarBackground/HealthBarFill")
                ?.GetComponent<Image>();
            Assert.That(healthFill, Is.Not.Null);
            Assert.That(healthFill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(healthFill.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
            Assert.That(healthFill.fillOrigin, Is.EqualTo((int)Image.OriginHorizontal.Left));
            Assert.That(canvas?.Find("ReadabilityHUD/ThreatPriorityPanel/ThreatLabel"), Is.Not.Null);
            Assert.That(head?.Find("CoreReadabilityRing"), Is.Not.Null);
            Assert.That(head.GetComponentsInChildren<SpriteRenderer>(true).Min(renderer => renderer.sortingOrder),
                Is.GreaterThanOrEqualTo(220));

            var legacy = canvas.GetComponentsInChildren<TMP_Text>(true)
                .First(label => label.name == "HealthLabel");
            Assert.That(legacy.enabled, Is.False);
            var staticHealth = canvas.GetComponentsInChildren<TMP_Text>(true)
                .First(label => label.name == "HP");
            Assert.That(staticHealth.enabled, Is.False);
            Assert.That(canvas.Find("CombatHUD/PlayerHealthHUD").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayerDamage_UpdatesTheSingleHpReadout()
        {
            yield return LoadGameAndEnterCombat();
            var health = Object.FindAnyObjectByType<OSPlayerHealth>();
            var hud = Object.FindAnyObjectByType<OSCombatHudPresenter>();
            hud.ForceRefreshForTesting();

            Assert.That(hud.PrimaryText, Does.Contain("HP  100/100"));
            Assert.That(hud.PrimaryText, Does.Not.Contain("CORE  100/100"));
            Assert.That(hud.HealthFillAmount, Is.EqualTo(1f).Within(0.001f));

            var damage = new OSDamageEvent(
                1514001,
                1,
                1,
                1,
                OSTargetKind.PlayerHead,
                8f,
                Vector2.zero);
            Assert.That(health.TryApplyHeadDamage(damage).IsAccepted, Is.True);
            yield return null;
            yield return new WaitForEndOfFrame();

            Assert.That(hud.PrimaryText, Does.Contain("HP  92/100"));
            Assert.That(hud.HealthFillAmount, Is.EqualTo(0.92f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator BodySixtyFour_HudShowsCountWithoutTechnicalMaximumAndBatchesRefresh()
        {
            yield return LoadGameAndEnterCombat();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var hud = Object.FindAnyObjectByType<OSCombatHudPresenter>();
            yield return null;
            var previousRefreshes = hud.RefreshCount;

            Assert.That(chain.SetDebugSegmentCount(40).IsAccepted, Is.True);
            yield return null;
            Assert.That(hud.PrimaryText, Does.Contain("BODY  40"));

            previousRefreshes = hud.RefreshCount;
            Assert.That(chain.SetDebugSegmentCount(64).IsAccepted, Is.True);
            yield return null;

            Assert.That(hud.RefreshCount - previousRefreshes, Is.LessThanOrEqualTo(1));
            Assert.That(hud.PrimaryText, Does.Contain("BODY  64"));
            Assert.That(hud.PrimaryText, Does.Not.Contain("/64"));
            Assert.That(hud.PrimaryText, Does.Not.Contain("MAX"));
            Assert.That(hud.PrimaryText, Does.Contain("[O]"));
            Assert.That(hud.PrimaryText, Does.Contain("[>]"));
            Assert.That(hud.PrimaryText, Does.Contain("[=]"));
            Assert.That(hud.PrimaryText, Does.Contain("[+]"));

            Assert.That(chain.SetDebugSegmentCount(4).IsAccepted, Is.True);
            yield return null;
            Assert.That(hud.ActionText, Does.Contain("DASH READY"));
            Assert.That(hud.ActionText, Does.Contain("4.5u / 0.5s"));
        }

        [UnityTest]
        public IEnumerator EnemyOneHundredFifty_ReadabilityPriorityRemainsStructural()
        {
            yield return LoadGameAndEnterCombat();
            var registry = Object.FindAnyObjectByType<OSEnemyRegistry>();
            var pools = Object.FindAnyObjectByType<OSPoolRegistry>();
            while (registry.NormalEnemyCount < 150)
            {
                var index = registry.NormalEnemyCount;
                var position = new Vector2(8f + index % 10 * 0.1f, -4f + index % 20 * 0.2f);
                Assert.That(pools.Rent("enemy_chaser", position, Quaternion.identity).IsAccepted, Is.True);
            }

            var headOrder = GameObject.Find("Head").GetComponentsInChildren<SpriteRenderer>(true)
                .Min(renderer => renderer.sortingOrder);
            var charger = Object.FindObjectsByType<OSEnemyController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .First(enemy => enemy.DefinitionId == "enemy_charger");
            var dangerOrder = charger.GetComponentsInChildren<LineRenderer>(true)
                .Max(line => line.sortingOrder);
            var pickupOrder = Object.FindAnyObjectByType<OSPickup>(FindObjectsInactive.Include)
                .GetComponent<SpriteRenderer>().sortingOrder;

            Assert.That(registry.NormalEnemyCount, Is.EqualTo(150));
            Assert.That(headOrder, Is.GreaterThan(dangerOrder));
            Assert.That(dangerOrder, Is.GreaterThan(pickupOrder));
        }

        [UnityTest]
        public IEnumerator SelectionPanelAnimation_ContinuesWhileSimulationIsPaused()
        {
            yield return LoadGame();
            var animator = Object.FindObjectsByType<OSSelectionPanelAnimator>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .First(candidate => candidate.gameObject.name == "BodyRoleSelectionPanel");
            var before = animator.UnscaledTickCount;

            Assert.That(Time.timeScale, Is.Zero);
            yield return new WaitForSecondsRealtime(0.08f);

            Assert.That(animator.UsesUnscaledTime, Is.True);
            Assert.That(animator.UnscaledTickCount, Is.GreaterThan(before));
        }

        [UnityTest]
        public IEnumerator Tutorial_StartsOnlyForFirstSessionAcrossSceneReload()
        {
            OSTutorialPresenter.ResetApplicationStateForTesting();
            yield return LoadGameAndEnterCombat();
            var first = Object.FindAnyObjectByType<OSTutorialPresenter>();
            Assert.That(first.CurrentStage, Is.EqualTo(OSTutorialStage.Movement));
            Assert.That(first.IsTutorialVisible, Is.True);

            yield return LoadGameAndEnterCombat();
            var second = Object.FindAnyObjectByType<OSTutorialPresenter>();

            Assert.That(second.CurrentStage, Is.EqualTo(OSTutorialStage.None));
            Assert.That(second.IsTutorialVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator ControlledEnemy_ShowsPatternedCountdownWithoutChangingRuleResult()
        {
            yield return LoadGameAndEnterCombat();
            var pools = Object.FindAnyObjectByType<OSPoolRegistry>();
            var rent = pools.Rent("enemy_chaser", new Vector2(4f, 0f), Quaternion.identity);
            Assert.That(rent.IsAccepted, Is.True);
            var enemy = rent.Payload as OSEnemyController;
            Assert.That(enemy, Is.Not.Null);

            Assert.That(enemy.ApplyControl(0.25f).IsAccepted, Is.True);
            Assert.That(enemy.ControlStatusVisible, Is.True);
            enemy.SimulateStep(0.3f);

            Assert.That(enemy.ControlStatusVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator BodyCut_ShowsOneRoleBreakdownAndWorldBoundary()
        {
            yield return LoadGameAndEnterCombat();
            var chain = Object.FindAnyObjectByType<OSBodyChain>();
            var feedback = Object.FindAnyObjectByType<OSCombatFeedbackPresenter>();
            Assert.That(chain.SetDebugSegmentCount(8).IsAccepted, Is.True);

            Assert.That(chain.TryCutFrom(4, new Vector2(1f, 2f)).IsAccepted, Is.True);

            Assert.That(feedback.LastMessage, Does.Contain("BODY CUT"));
            Assert.That(feedback.LastMessage, Does.Contain("[O]"));
            Assert.That(feedback.LastMessage, Does.Contain("[>]"));
            Assert.That(feedback.LastMessage, Does.Contain("[=]"));
            Assert.That(feedback.LastMessage, Does.Contain("[+]"));
            Assert.That(feedback.MessageRevision, Is.EqualTo(1));
            Assert.That(feedback.VisibleWorldMarkerCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ReadabilityPanels_DoNotOverlapAtReferenceResolution()
        {
            yield return LoadGame();
            var root = GameObject.Find("Canvas")?.transform.Find("ReadabilityHUD");
            var primary = (RectTransform)root.Find("CombatSummaryPanel");
            var action = (RectTransform)root.Find("ActionSummaryPanel");
            var threat = (RectTransform)root.Find("ThreatPriorityPanel");
            var tutorial = (RectTransform)root.Find("TutorialPanel");
            Canvas.ForceUpdateCanvases();

            Assert.That(Overlaps(primary, threat), Is.False);
            Assert.That(Overlaps(primary, action), Is.False);
            Assert.That(Overlaps(action, tutorial), Is.False);
        }

        private static bool Overlaps(RectTransform first, RectTransform second)
        {
            var firstCorners = new Vector3[4];
            var secondCorners = new Vector3[4];
            first.GetWorldCorners(firstCorners);
            second.GetWorldCorners(secondCorners);
            var firstRect = Rect.MinMaxRect(firstCorners[0].x, firstCorners[0].y, firstCorners[2].x, firstCorners[2].y);
            var secondRect = Rect.MinMaxRect(secondCorners[0].x, secondCorners[0].y, secondCorners[2].x, secondCorners[2].y);
            return firstRect.Overlaps(secondRect);
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("20_Game", LoadSceneMode.Single);
            yield return null;
        }

        private static IEnumerator LoadGameAndEnterCombat()
        {
            yield return LoadGame();
            var session = Object.FindAnyObjectByType<OSGameSessionController>();
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.CompleteActiveSelection().IsAccepted, Is.True);
            Assert.That(session.State, Is.EqualTo(OSSessionState.Combat));
            yield return null;
        }
    }
}
