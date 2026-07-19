using System;
using Ouroboros.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Ouroboros.Runtime
{
    public enum OSInputMode
    {
        None,
        Player,
        UI
    }

    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class OSInputRouter : MonoBehaviour
    {
        private const string PlayerMapName = "Player";
        private const string UiMapName = "UI";

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private InputSystemUIInputModule uiInputModule;

        private InputActionMap _playerMap;
        private InputActionMap _uiMap;
        private InputAction _moveAction;
        private InputAction _bodyDashAction;
        private InputAction _submitAction;
        private InputAction _cancelAction;
        private bool _isBound;
        private bool _afterInputUpdateSubscribed;
        private bool _submitPending;
        private Vector2 _moveValue;

        public event Action<Vector2> MoveChanged;
        public event Action BodyDashRequested;
        public event Action SubmitRequested;
        public event Action CancelRequested;

        public OSInputMode CurrentMode { get; private set; }
        public Vector2 MoveValue => _moveValue;
        public bool PlayerMapEnabled => _playerMap?.enabled == true;
        public bool UiMapEnabled => _uiMap?.enabled == true;
        public bool IsConfigured => ResolveActions();

        public bool IsMapStateValid => CurrentMode switch
        {
            OSInputMode.None => !PlayerMapEnabled && !UiMapEnabled,
            OSInputMode.Player => PlayerMapEnabled && !UiMapEnabled,
            OSInputMode.UI => !PlayerMapEnabled && UiMapEnabled,
            _ => false
        };

        private void Awake()
        {
            ResolveActions();
        }

        private void OnEnable()
        {
            if (!ResolveActions())
            {
                return;
            }

            BindActions();
            SubscribeAfterInputUpdate();
            ApplyInputMode();
        }

        private void OnDisable()
        {
            UnsubscribeAfterInputUpdate();
            _submitPending = false;
            UnbindActions();
            DisableAllMaps();
            CurrentMode = OSInputMode.None;
            ResetMoveValue();
        }

        /// <summary>
        /// Assigns the action asset and optional uGUI input module before session state routing begins.
        /// </summary>
        public void Configure(InputActionAsset actions, InputSystemUIInputModule module = null)
        {
            var wasActive = isActiveAndEnabled;
            if (wasActive)
            {
                UnbindActions();
                DisableAllMaps();
            }

            inputActions = actions;
            uiInputModule = module;
            ClearResolvedActions();
            ResolveActions();

            if (wasActive && IsConfigured)
            {
                BindActions();
                SubscribeAfterInputUpdate();
                ApplyInputMode();
            }
        }

        /// <summary>
        /// Applies the mutually exclusive input mode required by a confirmed session state.
        /// </summary>
        public void SetForState(OSSessionState state)
        {
            var mode = state switch
            {
                OSSessionState.Combat => OSInputMode.Player,
                OSSessionState.BodyDash => OSInputMode.Player,
                OSSessionState.StartBodySelection => OSInputMode.UI,
                OSSessionState.BodyRoleSelection => OSInputMode.UI,
                OSSessionState.LevelUpSelection => OSInputMode.UI,
                OSSessionState.Dead => OSInputMode.UI,
                OSSessionState.Cleared => OSInputMode.UI,
                OSSessionState.Result => OSInputMode.UI,
                _ => OSInputMode.None
            };

            SetInputMode(mode);
        }

        /// <summary>
        /// Disables both maps before enabling exactly one requested map.
        /// </summary>
        public void SetInputMode(OSInputMode mode)
        {
            CurrentMode = mode;
            if (!ResolveActions())
            {
                DisableAllMaps();
                return;
            }

            if (isActiveAndEnabled)
            {
                BindActions();
                SubscribeAfterInputUpdate();
            }

            ApplyInputMode();
        }

        private bool ResolveActions()
        {
            if (inputActions == null)
            {
                return false;
            }

            _playerMap ??= inputActions.FindActionMap(PlayerMapName, false);
            _uiMap ??= inputActions.FindActionMap(UiMapName, false);
            _moveAction ??= _playerMap?.FindAction("Move", false);
            _bodyDashAction ??= _playerMap?.FindAction("BodyDash", false);
            _submitAction ??= _uiMap?.FindAction("Submit", false);
            _cancelAction ??= _uiMap?.FindAction("Cancel", false);
            return _playerMap != null && _uiMap != null && _moveAction != null &&
                   _bodyDashAction != null && _submitAction != null && _cancelAction != null;
        }

        private void ClearResolvedActions()
        {
            _playerMap = null;
            _uiMap = null;
            _moveAction = null;
            _bodyDashAction = null;
            _submitAction = null;
            _cancelAction = null;
            _isBound = false;
        }

        private void BindActions()
        {
            if (_isBound || !ResolveActions())
            {
                return;
            }

            _moveAction.performed += HandleMovePerformed;
            _moveAction.canceled += HandleMoveCanceled;
            _bodyDashAction.performed += HandleBodyDashPerformed;
            _submitAction.performed += HandleSubmitPerformed;
            _cancelAction.performed += HandleCancelPerformed;
            _isBound = true;
        }

        private void UnbindActions()
        {
            if (!_isBound)
            {
                return;
            }

            _moveAction.performed -= HandleMovePerformed;
            _moveAction.canceled -= HandleMoveCanceled;
            _bodyDashAction.performed -= HandleBodyDashPerformed;
            _submitAction.performed -= HandleSubmitPerformed;
            _cancelAction.performed -= HandleCancelPerformed;
            _isBound = false;
        }

        private void ApplyInputMode()
        {
            DisableAllMaps();
            ResetActionPhases();

            switch (CurrentMode)
            {
                case OSInputMode.Player:
                    _playerMap.Enable();
                    break;
                case OSInputMode.UI:
                    _uiMap.Enable();
                    if (uiInputModule != null)
                    {
                        uiInputModule.enabled = true;
                    }

                    break;
            }
        }

        private void DisableAllMaps()
        {
            if (uiInputModule != null)
            {
                uiInputModule.enabled = false;
            }

            _playerMap?.Disable();
            _uiMap?.Disable();
            ResetMoveValue();
        }

        private void ResetActionPhases()
        {
            _moveAction?.Reset();
            _bodyDashAction?.Reset();
            _submitAction?.Reset();
            _cancelAction?.Reset();
        }

        private void ResetMoveValue()
        {
            if (_moveValue == Vector2.zero)
            {
                return;
            }

            _moveValue = Vector2.zero;
            MoveChanged?.Invoke(_moveValue);
        }

        private void HandleMovePerformed(InputAction.CallbackContext context)
        {
            _moveValue = Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f);
            MoveChanged?.Invoke(_moveValue);
        }

        private void HandleMoveCanceled(InputAction.CallbackContext context)
        {
            ResetMoveValue();
        }

        private void HandleBodyDashPerformed(InputAction.CallbackContext context)
        {
            if (CurrentMode == OSInputMode.Player)
            {
                BodyDashRequested?.Invoke();
            }
        }

        private void HandleSubmitPerformed(InputAction.CallbackContext context)
        {
            if (CurrentMode == OSInputMode.UI)
            {
                _submitPending = true;
            }
        }

        private void SubscribeAfterInputUpdate()
        {
            if (_afterInputUpdateSubscribed)
            {
                return;
            }

            InputSystem.onAfterUpdate += HandleAfterInputUpdate;
            _afterInputUpdateSubscribed = true;
        }

        private void UnsubscribeAfterInputUpdate()
        {
            if (!_afterInputUpdateSubscribed)
            {
                return;
            }

            InputSystem.onAfterUpdate -= HandleAfterInputUpdate;
            _afterInputUpdateSubscribed = false;
        }

        private void HandleAfterInputUpdate()
        {
            if (!_submitPending)
            {
                return;
            }

            _submitPending = false;
            if (isActiveAndEnabled && CurrentMode == OSInputMode.UI)
            {
                SubmitRequested?.Invoke();
            }
        }

        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            if (CurrentMode == OSInputMode.UI)
            {
                CancelRequested?.Invoke();
            }
        }
    }
}
