using TMPro;
using UnityEngine;

namespace Ouroboros.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class OSBuildInfoPresenter : MonoBehaviour
    {
        private void Awake()
        {
            var text = GetComponent<TMP_Text>();
            if (text != null)
            {
                text.text = OSBuildInfo.Label;
            }
        }
    }
}
