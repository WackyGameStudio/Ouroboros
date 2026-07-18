using System.Collections;
using Ouroboros.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ouroboros.Runtime
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class OSBootstrap : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private OSPlayerBalanceData playerBalanceData;
        [SerializeField] private OSBodyBalanceData bodyBalanceData;
        [SerializeField] private OSEncounterBalanceData encounterBalanceData;
        [SerializeField] private OSWaveScheduleData waveScheduleData;
        [SerializeField] private OSUpgradeCatalog upgradeCatalog;
        [SerializeField] private OSFeedbackCatalog feedbackCatalog;
        [SerializeField] private bool loadMainMenuOnStart = true;
        [SerializeField] private string mainMenuScene = OSProjectSettingsValidator.MainMenuScene;

        private bool _hasLoggedFailure;

        public bool ValidationPassed { get; private set; }

        public string LastValidationMessage { get; private set; } = string.Empty;

        public OSSessionRuntimeState RuntimeState { get; private set; }

        private IEnumerator Start()
        {
            OSBuildInfo.LogStartupOnce();

            var result = OSProjectSettingsValidator.Validate(inputActions);
            if (!result.IsValid)
            {
                LastValidationMessage = result.Message;
                ValidationPassed = false;
                LogFailureOnce(result.Message);
                yield break;
            }

            var runtimeResult = OSSessionRuntimeState.InitializeFrom(
                playerBalanceData,
                bodyBalanceData,
                encounterBalanceData,
                waveScheduleData,
                upgradeCatalog,
                feedbackCatalog,
                out var dataReport);
            if (!runtimeResult.IsAccepted)
            {
                LastValidationMessage = dataReport.Message;
                ValidationPassed = false;
                LogFailureOnce(dataReport.Message);
                yield break;
            }

            RuntimeState = runtimeResult.Payload;
            LastValidationMessage = dataReport.Message;
            ValidationPassed = true;
            Debug.Log("[OUROBOROS][BOOT] Project foundation and data validation passed.", this);

            if (!loadMainMenuOnStart)
            {
                yield break;
            }

            yield return null;
            yield return SceneManager.LoadSceneAsync(mainMenuScene, LoadSceneMode.Single);
        }

        private void LogFailureOnce(string message)
        {
            if (_hasLoggedFailure)
            {
                return;
            }

            _hasLoggedFailure = true;
            Debug.LogError($"[OUROBOROS][BOOT] Game entry blocked.\n{message}", this);
        }
    }
}
