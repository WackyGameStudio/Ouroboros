using UnityEngine;

namespace Ouroboros.Runtime
{
    public static class OSBuildInfo
    {
        public const string ExpectedUnityVersion = "6000.5.1f1";

        private static bool s_HasLoggedStartup;

        public static string Label
        {
            get
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                const string configuration = "Development";
#else
                const string configuration = "Release";
#endif
                return $"{Application.productName} v{Application.version} | {configuration} | {Application.platform} | Unity {Application.unityVersion}";
            }
        }

        public static void LogStartupOnce()
        {
            if (s_HasLoggedStartup)
            {
                return;
            }

            s_HasLoggedStartup = true;
            Debug.Log($"[OUROBOROS][BUILD] {Label}");
        }
    }

}
