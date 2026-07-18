using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    public sealed class OSMainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameScene = OSProjectSettingsValidator.GameScene;

        private bool _isLoading;

        private void Awake()
        {
            OSBuildInfo.LogStartupOnce();
        }

        private void Update()
        {
            if (_isLoading)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                StartGame();
            }
        }

        public void StartGame()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;
            SceneManager.LoadSceneAsync(gameScene, LoadSceneMode.Single);
        }
    }
}
