#if (!IMMUTABLE_CUSTOM_BROWSER || UNITY_STANDALONE_LINUX) || VOLTSTRO_LINUX && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || (UNITY_ANDROID && UNITY_EDITOR_WIN) || (UNITY_IPHONE && UNITY_EDITOR_WIN))

using VoltstroStudios.UnityWebBrowser.Core;

namespace VoltstroStudios.UnityWebBrowser
{
    public class WebBrowserNoUi : BaseUwbClientManager
    {

    }
}

#endif
