using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ouroboros.UI
{
    [DisallowMultipleComponent]
    public sealed class OSSelectionPanelAnimator : MonoBehaviour
    {
        [SerializeField] private Button[] buttons = System.Array.Empty<Button>();
        [SerializeField, Min(0f)] private float pulseAmount = 0.035f;
        [SerializeField, Min(0f)] private float pulseFrequency = 2.5f;

        private Vector3[] _baseScales = System.Array.Empty<Vector3>();
        private TMP_Text[] _labels = System.Array.Empty<TMP_Text>();

        public int UnscaledTickCount { get; private set; }
        public bool UsesUnscaledTime => true;

        private void OnEnable()
        {
            CaptureScales();
        }

        private void Update()
        {
            UnscaledTickCount++;
            var selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            var pulse = 1f + ((Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * pulseFrequency) + 1f) *
                              0.5f * pulseAmount);
            for (var index = 0; index < buttons.Length; index++)
            {
                var button = buttons[index];
                if (button == null)
                {
                    continue;
                }

                var baseScale = index < _baseScales.Length ? _baseScales[index] : Vector3.one;
                button.transform.localScale = selected == button.gameObject ? baseScale * pulse : baseScale;
                var label = index < _labels.Length ? _labels[index] : null;
                if (label != null)
                {
                    label.fontStyle = selected == button.gameObject
                        ? FontStyles.Bold
                        : FontStyles.Normal;
                }
            }
        }

        private void OnDisable()
        {
            ResetScales();
        }

        public void Configure(Button[] selectionButtons)
        {
            ResetScales();
            buttons = selectionButtons ?? System.Array.Empty<Button>();
            CaptureScales();
        }

        private void CaptureScales()
        {
            _baseScales = new Vector3[buttons?.Length ?? 0];
            _labels = new TMP_Text[_baseScales.Length];
            for (var index = 0; index < _baseScales.Length; index++)
            {
                _baseScales[index] = buttons[index] != null
                    ? buttons[index].transform.localScale
                    : Vector3.one;
                _labels[index] = buttons[index] != null
                    ? buttons[index].GetComponentInChildren<TMP_Text>(true)
                    : null;
            }
        }

        private void ResetScales()
        {
            for (var index = 0; index < (buttons?.Length ?? 0); index++)
            {
                if (buttons[index] != null && index < _baseScales.Length)
                {
                    buttons[index].transform.localScale = _baseScales[index];
                }
            }
        }
    }
}
