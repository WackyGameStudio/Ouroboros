using Ouroboros.Core;
using Ouroboros.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSResultPanel : MonoBehaviour
    {
        [SerializeField] private OSGameSessionController sessionController;
        [SerializeField] private OSRunSummaryController summaryController;
        [SerializeField] private TMP_Text resultLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private string mainMenuScene = "10_MainMenu";

        private bool _subscribed;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(
            OSGameSessionController session,
            OSRunSummaryController summary,
            TMP_Text label,
            Button restart,
            Button menu)
        {
            Unsubscribe();
            sessionController = session;
            summaryController = summary;
            resultLabel = label;
            restartButton = restart;
            menuButton = menu;
            Subscribe();
            Refresh();
        }

        public void RestartRun()
        {
            if (sessionController != null && sessionController.State == OSSessionState.Result)
            {
                sessionController.RestartSession();
            }
        }

        public void ReturnToMainMenu()
        {
            if (sessionController != null && sessionController.State == OSSessionState.Result)
            {
                SceneManager.LoadScene(mainMenuScene, LoadSceneMode.Single);
            }
        }

        private void Refresh()
        {
            if (resultLabel == null || summaryController == null || !summaryController.HasSummary)
            {
                if (resultLabel != null)
                {
                    resultLabel.text = "FINALIZING SESSION RESULT...";
                }

                SetButtons(false);
                return;
            }

            var summary = summaryController.Summary;
            var seconds = Mathf.Max(0, Mathf.FloorToInt(summary.DurationSeconds));
            var title = summary.ResultKind switch
            {
                OSSessionResultKind.BossDefeated => "SWARM CORE DESTROYED  |  RUN CLEARED",
                OSSessionResultKind.BossTimeout => "CORE BREACH FAILED  |  150s TIMEOUT",
                _ => "CORE LOST  |  RUN FAILED"
            };
            var prompt = sessionController != null && sessionController.State == OSSessionState.Result
                ? "[ENTER] RESTART  |  BUTTON: MAIN MENU"
                : "[ENTER] CONFIRM RESULT";
            resultLabel.text =
                $"{title}\n" +
                $"TIME {seconds / 60:00}:{seconds % 60:00}  |  KILLS {summary.TotalKills}  " +
                $"ELITE {summary.EliteKills}  DASH {summary.DashUseCount}\n" +
                $"BODY MAX {summary.MaxBodyCount} / FINAL {summary.FinalBodyCount}  |  " +
                $"GAIN {summary.AcquiredBodyCount}  CUT {summary.CutBodyCount}  " +
                $"DASH BODY PULL {summary.DashConvergedBodyCount}\n" +
                $"HEAD DAMAGE {summary.ReceivedHeadDamage:0.0}\n" +
                $"ROLE MAX  S{summary.MaxRoleCounts.Shield} A{summary.MaxRoleCounts.Attack} " +
                $"L{summary.MaxRoleCounts.Laser} C{summary.MaxRoleCounts.Control}\n" +
                $"ROLE FINAL  S{summary.FinalRoleCounts.Shield} A{summary.FinalRoleCounts.Attack} " +
                $"L{summary.FinalRoleCounts.Laser} C{summary.FinalRoleCounts.Control}\n" +
                $"LEVEL {summary.Level}  |  UPGRADES {summary.AppliedUpgradeCount}\n" +
                $"BUILD  {summary.UpgradeSummary}\n" +
                $"RUN SEED {summary.RunSeed}  |  {summary.DataVersion}\n" +
                prompt;
            SetButtons(sessionController != null && sessionController.State == OSSessionState.Result);
        }

        private void SetButtons(bool active)
        {
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(active);
                restartButton.interactable = active;
            }

            if (menuButton != null)
            {
                menuButton.gameObject.SetActive(active);
                menuButton.interactable = active;
            }
        }

        private void HandleStateChanged(OSSessionState previous, OSSessionState current)
        {
            Refresh();
        }

        private void HandleSummaryBuilt(OSSessionSummary summary)
        {
            Refresh();
        }

        private void Subscribe()
        {
            if (_subscribed || !isActiveAndEnabled)
            {
                return;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged += HandleStateChanged;
            }

            if (summaryController != null)
            {
                summaryController.SummaryBuilt += HandleSummaryBuilt;
            }

            restartButton?.onClick.AddListener(RestartRun);
            menuButton?.onClick.AddListener(ReturnToMainMenu);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (sessionController != null)
            {
                sessionController.StateChanged -= HandleStateChanged;
            }

            if (summaryController != null)
            {
                summaryController.SummaryBuilt -= HandleSummaryBuilt;
            }

            restartButton?.onClick.RemoveListener(RestartRun);
            menuButton?.onClick.RemoveListener(ReturnToMainMenu);
            _subscribed = false;
        }
    }
}
