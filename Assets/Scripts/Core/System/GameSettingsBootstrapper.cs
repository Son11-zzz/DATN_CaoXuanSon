using UnityEngine;

public static class GameSettingsBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureGameSettings()
    {
        if (Object.FindFirstObjectByType<GameSettings>() != null)
            return;

        GameObject bootstrap = new GameObject("GameSettings");
        bootstrap.AddComponent<GameSettings>();
    }
}