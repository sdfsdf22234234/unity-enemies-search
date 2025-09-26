using UnityEditor;

public class InitBatchRendererGroupSettings
{
    [InitializeOnLoadMethod]
    public static void InitSettings()
    {
        if (!PlayerSettings.allowUnsafeCode)
        {
            PlayerSettings.allowUnsafeCode = true;
        }
    }
}
